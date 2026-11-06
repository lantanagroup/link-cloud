using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Link.AspireAppHost.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Link.AspireAppHost.Extensions;

/// <summary>
/// A <b>Create user</b> command on the Account service, added only when Keycloak signs people in
/// (Auth:Mode Keycloak, the https launch profile): a dashboard form whose answers become a user in
/// Keycloak, with the password from the form, and in Account. Under anonymous access no one signs in,
/// so there is no user to create.
/// </summary>
/// <remarks>
/// The Account user is created through Account's own <c>POST /api/account/user</c>, with a Link system
/// token (<see cref="SeedDataExtensions.AccountClient"/>), so validation, role assignment and auditing
/// happen as for a user created anywhere else. The Admin BFF finds the Account user of whoever signs in
/// by the email claim, so the email is what joins the Keycloak user to the Account user.
/// </remarks>
internal static class AccountUserExtensions
{
    /// <summary>The outcome of creating the user in one place.</summary>
    private enum Outcome { Created, AlreadyExists, Failed }

    public static IResourceBuilder<ProjectResource> WithCreateUserCommand(this IResourceBuilder<ProjectResource> account, LinkKeycloak keycloak) =>
        account.WithCommand(
            name: "create-user",
            displayName: "Create user",
            executeCommand: context => CreateUserAsync(context, keycloak),
            commandOptions: new CommandOptions
            {
                Description = "Add a user to Keycloak and the Account service",
                // Account must be answering: the form lists its roles and posts the user to it.
                UpdateState = context => context.ResourceSnapshot.HealthStatus is HealthStatus.Healthy
                    ? ResourceCommandState.Enabled
                    : ResourceCommandState.Disabled,
                IconName = "PersonAdd",
                IconVariant = IconVariant.Filled
            });

    private static async Task<ExecuteCommandResult> CreateUserAsync(ExecuteCommandContext context, LinkKeycloak keycloak)
    {
        var interaction = context.Services.GetRequiredService<IInteractionService>();
        if (!interaction.IsAvailable)
            return CommandResults.Failure("The dashboard is needed to fill in the form.");

        var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Link.CreateUser");
        using var http = SeedDataExtensions.AccountClient();

        // The roles come from Account. Seeding normally creates them when Account starts; if that has not
        // happened (it failed, or the database was reset since), it runs now so there is a role to choose.
        var roles = await SeedDataExtensions.GetRoleNamesAsync(http, context.CancellationToken);
        if (roles.Count == 0)
        {
            try
            {
                await SeedDataExtensions.EnsureRolesAsync(http, logger, context.CancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                logger.LogError("Seeding the Account roles failed: {Error}", ex.Message);
                return CommandResults.Failure("Account has no roles, and seeding them failed. The AppHost console has the details.");
            }
            roles = await SeedDataExtensions.GetRoleNamesAsync(http, context.CancellationToken);
            if (roles.Count == 0)
                return CommandResults.Failure("Account still lists no roles after seeding.");
        }

        List<InteractionInput> inputs =
        [
            new() { Name = "firstName", Label = "First name", InputType = InputType.Text, Required = true },
            new() { Name = "lastName", Label = "Last name", InputType = InputType.Text, Required = true },
            new()
            {
                Name = "email",
                Label = "Email",
                InputType = InputType.Text,
                Required = true,
                Description = "Admin BFF finds the user by this address when they sign in."
            },
            new() { Name = "username", Label = "Username", InputType = InputType.Text, Placeholder = "The email address, if left empty" },
            new()
            {
                Name = "role",
                Label = "Role",
                InputType = InputType.Choice,
                Required = true,
                Options = roles.Select(name => KeyValuePair.Create(name, name)).ToList(),
                Value = roles[0]
            },
            new()
            {
                Name = "password",
                Label = "Password",
                InputType = InputType.SecretText,
                Required = true,
                Description = "The Keycloak password to sign in with. It does not need changing on first sign-in."
            }
        ];

        var result = await interaction.PromptInputsAsync(
            "Create user",
            "Add a user to Keycloak, to sign in with, and to the Account service.",
            inputs,
            new InputsDialogInteractionOptions
            {
                PrimaryButtonText = "Create",
                ValidationCallback = validation =>
                {
                    var address = validation.Inputs["email"].Value?.Trim();
                    if (!string.IsNullOrEmpty(address) && !System.Net.Mail.MailAddress.TryCreate(address, out _))
                        validation.AddValidationError(validation.Inputs["email"], "Enter an email address.");
                    return Task.CompletedTask;
                }
            },
            context.CancellationToken);

        if (result.Canceled)
            return CommandResults.Canceled();

        var answers = result.Data;
        var user = new NewUser(
            FirstName: answers["firstName"].Value!.Trim(),
            LastName: answers["lastName"].Value!.Trim(),
            Email: answers["email"].Value!.Trim(),
            Username: string.IsNullOrWhiteSpace(answers["username"].Value) ? answers["email"].Value!.Trim() : answers["username"].Value!.Trim(),
            Role: answers["role"].Value!);

        // Each place is tried whatever happened in the other, so a user missing from only one of them
        // (after Keycloak's or SQL Server's data was reset, say) can be added there by running this again.
        var (accountOutcome, accountMessage) = await CreateAccountUserAsync(http, user, logger, context.CancellationToken);

        var (keycloakOutcome, keycloakMessage) =
            await CreateKeycloakUserAsync(keycloak, user, answers["password"].Value!, logger, context.CancellationToken);
        var message = $"{accountMessage} {keycloakMessage}";
        return accountOutcome is not Outcome.Failed && keycloakOutcome is not Outcome.Failed
               && (accountOutcome is Outcome.Created || keycloakOutcome is Outcome.Created)
            ? CommandResults.Success(message)
            : CommandResults.Failure(message);
    }

    private sealed record NewUser(string FirstName, string LastName, string Email, string Username, string Role);

    private static async Task<(Outcome, string)> CreateAccountUserAsync(HttpClient http, NewUser user, ILogger logger, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsJsonAsync("api/account/user", new
        {
            firstName = user.FirstName,
            lastName = user.LastName,
            email = user.Email,
            username = user.Username,
            roles = new[] { user.Role },
            userClaims = Array.Empty<string>(),
            isActive = true
        }, cancellationToken);

        switch (response.StatusCode)
        {
            case HttpStatusCode.Created:
                // The response repeats the roles that were asked for, whether or not they were assigned: Account
                // skips a role it cannot find and carries on. So read the user back to see what it really has.
                var created = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
                var stored = await http.GetFromJsonAsync<JsonElement>($"api/account/user/{created.GetProperty("id").GetString()}", cancellationToken);
                var assigned = stored.TryGetProperty("roles", out var roles) && roles.ValueKind is JsonValueKind.Array
                    && roles.EnumerateArray().Any(r => r.GetString() == user.Role);
                if (!assigned)
                {
                    logger.LogWarning("Created the Account user {Email}, but Account did not assign the role {Role}", user.Email, user.Role);
                    return (Outcome.Failed, $"Created the Account user {user.Email}, but Account did not give them the {user.Role} role.");
                }
                logger.LogInformation("Created the Account user {Email} with the role {Role}", user.Email, user.Role);
                return (Outcome.Created, $"Created the Account user {user.Email} with the {user.Role} role.");
            case HttpStatusCode.Conflict:
                return (Outcome.AlreadyExists, $"Account already has a user with the email {user.Email}.");
            default:
                // The full response goes to the log. The dashboard refuses text that looks like markup.
                logger.LogError("Account refused the user {Email} with {Status}: {Body}",
                    user.Email, (int)response.StatusCode, await response.Content.ReadAsStringAsync(cancellationToken));
                return (Outcome.Failed, $"Account refused the user ({(int)response.StatusCode}); the AppHost console has the details.");
        }
    }

    private static async Task<(Outcome, string)> CreateKeycloakUserAsync(
        LinkKeycloak keycloak, NewUser user, string password, ILogger logger, CancellationToken cancellationToken)
    {
        try
        {
            var (status, body) = await keycloak.CreateUserAsync(user.FirstName, user.LastName, user.Email, user.Username, password, cancellationToken);
            switch (status)
            {
                case HttpStatusCode.Created:
                    logger.LogInformation("Created the Keycloak user {Username} ({Email}) in the {Realm} realm", user.Username, user.Email, LinkKeycloak.Realm);
                    return (Outcome.Created, $"Created the Keycloak user {user.Username}; sign in with that username or the email.");
                case HttpStatusCode.Conflict:
                    return (Outcome.AlreadyExists, $"Keycloak already has a user with the username {user.Username} or the email {user.Email}.");
                default:
                    logger.LogError("Keycloak refused the user {Username} with {Status}: {Body}", user.Username, (int)status, body);
                    return (Outcome.Failed, $"Keycloak refused the user ({(int)status}); the AppHost console has the details.");
            }
        }
        catch (HttpRequestException ex)
        {
            logger.LogError("Could not reach Keycloak's admin API: {Error}", ex.Message);
            return (Outcome.Failed, "Could not reach Keycloak; the AppHost console has the details.");
        }
    }
}
