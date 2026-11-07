using System.Net;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;
using Link.AspireAppHost.Security;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;

namespace Link.AspireAppHost.Extensions;

/// <summary>
/// A <b>Create test facility</b> command, on the Admin BFF: creates a facility and configures it in every service, by
/// running the requests in <c>FacilitySetup/steps.json</c> with the payloads beside it.
/// </summary>
/// <remarks>
/// The steps come from the QA Postman collection's facility creation folder. They are read when the command runs,
/// so editing <c>steps.json</c> or a payload needs no rebuild. Each step calls its service directly, with a Link
/// system token, so the command works in both launch profiles. It only creates what is missing, so running it again
/// for the same facility fills in whatever an earlier run did not get to.
/// </remarks>
internal static partial class FacilitySetupExtensions
{
    private const string StepsFile = "steps.json";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true, ReadCommentHandling = JsonCommentHandling.Skip };

    /// <summary>
    /// Adds the command to <paramref name="host"/> (the Admin BFF). The setup spans several services, so it sits on
    /// the front door rather than on any one of them.
    /// </summary>
    public static IResourceBuilder<ProjectResource> WithCreateFacilityCommand(this IResourceBuilder<ProjectResource> host)
    {
        var setupDirectory = Path.Combine(host.ApplicationBuilder.AppHostDirectory, "FacilitySetup");
        return host.WithCommand(
            name: "create-test-facility",
            displayName: "Create test facility",
            executeCommand: context => CreateFacilityAsync(context, setupDirectory),
            commandOptions: new CommandOptions
            {
                Description = "Create a facility and configure it in every service, from FacilitySetup/steps.json",
                UpdateState = context => context.ResourceSnapshot.HealthStatus is HealthStatus.Healthy
                    ? ResourceCommandState.Enabled
                    : ResourceCommandState.Disabled,
                IconName = "BuildingAdd",
                IconVariant = IconVariant.Filled
            });
    }

    private sealed record SetupFile(Dictionary<string, string> Variables, List<Step> Steps, Dictionary<string, Group>? Groups);

    private sealed record Step(
        string Name,
        string Service,
        string Method,
        string Path,
        string? Body,
        string? ExistsCheck,
        int[]? AlreadyExistsStatuses,
        Dictionary<string, string>? Capture,
        string? Group);

    private sealed record Group(string ExistsCheck);

    private enum StepResult { Created, AlreadyThere }

    private static async Task<ExecuteCommandResult> CreateFacilityAsync(ExecuteCommandContext context, string setupDirectory)
    {
        var interaction = context.Services.GetRequiredService<IInteractionService>();
        if (!interaction.IsAvailable)
            return CommandResults.Failure("The dashboard is needed to fill in the form.");
        var logger = context.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Link.FacilitySetup");

        SetupFile setup;
        try
        {
            setup = JsonSerializer.Deserialize<SetupFile>(await File.ReadAllTextAsync(Path.Combine(setupDirectory, StepsFile), context.CancellationToken), JsonOptions)
                ?? throw new JsonException($"{StepsFile} is empty.");
        }
        catch (Exception ex) when (ex is IOException or JsonException)
        {
            logger.LogError("Could not read FacilitySetup/{File}: {Error}", StepsFile, ex.Message);
            return CommandResults.Failure($"Could not read FacilitySetup/{StepsFile}; the AppHost console has the details.");
        }

        var variables = new Dictionary<string, string>(setup.Variables, StringComparer.OrdinalIgnoreCase);
        var answers = await PromptAsync(interaction, variables, context.CancellationToken);
        if (answers is null)
            return CommandResults.Canceled();
        foreach (var (key, value) in answers)
            variables[key] = value;

        return await CommandNotifications.RunWithNotificationsAsync(context, "Create test facility",
            $"Creating and configuring facility {variables["facilityId"]} in every service.",
            () => RunStepsAsync(setup, setupDirectory, variables, logger, context.CancellationToken));
    }

    /// <summary>Runs every step in order, stopping at the first failure. Returns the command's result.</summary>
    private static async Task<ExecuteCommandResult> RunStepsAsync(
        SetupFile setup, string setupDirectory, Dictionary<string, string> variables, ILogger logger, CancellationToken cancellationToken)
    {
        var created = new List<string>();
        var alreadyThere = new List<string>();
        var skippedGroups = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        foreach (var step in setup.Steps)
        {
            try
            {
                if (step.Group is not null && setup.Groups?.TryGetValue(step.Group, out var group) == true)
                {
                    if (!skippedGroups.TryGetValue(step.Group, out var skip))
                        skippedGroups[step.Group] = skip = await ExistsAsync(step.Service, group.ExistsCheck, variables, cancellationToken);
                    if (skip)
                    {
                        alreadyThere.Add(step.Name);
                        continue;
                    }
                }

                var result = await RunStepAsync(step, setupDirectory, variables, logger, cancellationToken);
                (result is StepResult.Created ? created : alreadyThere).Add(step.Name);
            }
            catch (Exception ex) when (ex is HttpRequestException or InvalidOperationException or IOException or JsonException or TaskCanceledException)
            {
                logger.LogError("Facility setup stopped at '{Step}': {Error}", step.Name, ex.Message);
                return CommandResults.Failure(
                    $"Stopped at '{step.Name}'. {Describe(created, alreadyThere)} The AppHost console has the details.");
            }
        }

        logger.LogInformation("Facility {Facility} is set up. Created: {Created}. Already there: {AlreadyThere}",
            variables["facilityId"], string.Join(", ", created), string.Join(", ", alreadyThere));
        return CommandResults.Success($"Facility {variables["facilityId"]} is set up. {Describe(created, alreadyThere)}");
    }

    private static string Describe(List<string> created, List<string> alreadyThere) =>
        $"Created {created.Count} item(s), {alreadyThere.Count} already there.";

    /// <summary>The form. Returns the answers, or null when it was cancelled.</summary>
    private static async Task<Dictionary<string, string>?> PromptAsync(
        IInteractionService interaction, Dictionary<string, string> defaults, CancellationToken cancellationToken)
    {
        string Default(string key) => defaults.TryGetValue(key, out var value) ? value : "";

        // The measures MeasureEval has, so the facility schedules one Tenant will accept.
        var measures = await GetMeasureIdsAsync(cancellationToken);
        var measureInput = measures.Count > 0
            ? new InteractionInput
            {
                Name = "measure",
                Label = "Measure",
                InputType = InputType.Choice,
                Required = true,
                Options = measures.Select(m => KeyValuePair.Create(m, m)).ToList(),
                Value = measures.Contains(Default("measure")) ? Default("measure") : measures[0],
                Description = "Scheduled monthly, and named by the query plans."
            }
            : new InteractionInput
            {
                Name = "measure",
                Label = "Measure",
                InputType = InputType.Text,
                Required = true,
                Value = Default("measure"),
                Description = "MeasureEval lists no measures. Run Seed data on admin-bff first, or the facility will be refused."
            };

        var result = await interaction.PromptInputsAsync(
            "Create test facility",
            "Create a facility and configure it in Tenant, Census, QueryDispatch, DataAcquisition and Normalization.",
            [
                new InteractionInput
                {
                    Name = "facilityId",
                    Label = "Facility id",
                    InputType = InputType.Text,
                    Required = true,
                    Value = Default("facilityId"),
                    Description = "Letters, digits and hyphens. A non-numeric id keeps it out of Automation UI's teardown."
                },
                new InteractionInput { Name = "facilityName", Label = "Facility name", InputType = InputType.Text, Value = Default("facilityName"), Placeholder = "The facility id, if left empty" },
                new InteractionInput { Name = "timeZone", Label = "Time zone", InputType = InputType.Text, Required = true, Value = Default("timeZone"), Description = "An IANA time zone, such as America/Chicago." },
                measureInput,
                new InteractionInput
                {
                    Name = "fhirServerBaseUrl",
                    Label = "FHIR server",
                    InputType = InputType.Text,
                    Required = true,
                    Value = Default("fhirServerBaseUrl"),
                    Description = "The FHIR server the facility's data is acquired from. The query list's patient lists exist on the QA server."
                }
            ],
            new InputsDialogInteractionOptions
            {
                PrimaryButtonText = "Create",
                ValidationCallback = validation =>
                {
                    var id = validation.Inputs["facilityId"].Value?.Trim() ?? "";
                    if (id.Length > 0 && !FacilityIdPattern().IsMatch(id))
                        validation.AddValidationError(validation.Inputs["facilityId"], "Use letters, digits and hyphens only.");
                    var url = validation.Inputs["fhirServerBaseUrl"].Value?.Trim() ?? "";
                    if (url.Length > 0 && !Uri.TryCreate(url, UriKind.Absolute, out _))
                        validation.AddValidationError(validation.Inputs["fhirServerBaseUrl"], "Enter an absolute URL.");
                    return Task.CompletedTask;
                }
            },
            cancellationToken);

        if (result.Canceled)
            return null;

        var answers = result.Data.ToDictionary(i => i.Name, i => i.Value?.Trim() ?? "", StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(answers["facilityName"]))
            answers["facilityName"] = answers["facilityId"];
        return answers;
    }

    /// <summary>The measure ids MeasureEval has, or none when it cannot be asked.</summary>
    private static async Task<IReadOnlyList<string>> GetMeasureIdsAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var http = LinkServiceClient.Create(Ports.MeasureEval);
            using var document = JsonDocument.Parse(await http.GetStringAsync("api/measureeval/measure-definition", cancellationToken));
            return document.RootElement.ValueKind is JsonValueKind.Array
                ? document.RootElement.EnumerateArray()
                    .Select(m => m.TryGetProperty("id", out var id) ? id.GetString() : null)
                    .OfType<string>()
                    .Order(StringComparer.Ordinal)
                    .ToList()
                : [];
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            return [];
        }
    }

    /// <summary>Runs one step: skips it when its existence check finds the thing, otherwise sends the request.</summary>
    private static async Task<StepResult> RunStepAsync(
        Step step, string setupDirectory, Dictionary<string, string> variables, ILogger logger, CancellationToken cancellationToken)
    {
        if (step.ExistsCheck is not null && await ExistsAsync(step.Service, step.ExistsCheck, variables, cancellationToken))
        {
            logger.LogInformation("'{Step}' is already there", step.Name);
            return StepResult.AlreadyThere;
        }

        using var http = Client(step.Service);
        using var request = new HttpRequestMessage(new HttpMethod(step.Method), FillPath(step.Path, variables));
        if (step.Body is not null)
        {
            var body = FillJson(await File.ReadAllTextAsync(Path.Combine(setupDirectory, step.Body), cancellationToken), variables);
            request.Content = new StringContent(body, Encoding.UTF8, "application/json");
        }

        using var response = await http.SendAsync(request, cancellationToken);
        var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

        if (step.AlreadyExistsStatuses?.Contains((int)response.StatusCode) == true)
        {
            logger.LogInformation("'{Step}' is already there ({Status})", step.Name, (int)response.StatusCode);
            return StepResult.AlreadyThere;
        }
        if (!response.IsSuccessStatusCode)
            throw new InvalidOperationException($"{step.Method} {step.Service} {request.RequestUri} answered {(int)response.StatusCode}: {responseBody}");

        if (step.Capture is not null)
        {
            using var document = JsonDocument.Parse(responseBody);
            foreach (var (variable, property) in step.Capture)
                variables[variable] = FindProperty(document.RootElement, property)
                    ?? throw new InvalidOperationException($"The response has no '{property}' to capture as {variable}: {responseBody}");
        }

        logger.LogInformation("Created '{Step}'", step.Name);
        return StepResult.Created;
    }

    /// <summary>
    /// The existence check: a GET answering 200 with a body that holds something means it exists; 204 or 404 means
    /// it does not. Anything else is an error.
    /// </summary>
    private static async Task<bool> ExistsAsync(string service, string path, Dictionary<string, string> variables, CancellationToken cancellationToken)
    {
        using var http = Client(service);
        using var response = await http.GetAsync(FillPath(path, variables), cancellationToken);
        if (response.StatusCode is HttpStatusCode.NoContent or HttpStatusCode.NotFound)
            return false;
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (response.StatusCode is HttpStatusCode.OK)
            return SeedDataExtensions.HasContent(body);
        throw new InvalidOperationException($"Checking GET {service} {path} answered {(int)response.StatusCode}: {body}");
    }

    private static HttpClient Client(string service) =>
        LinkServiceClient.ServicePorts.TryGetValue(service, out var port)
            ? LinkServiceClient.Create(port, TimeSpan.FromMinutes(2))
            : throw new InvalidOperationException($"Unknown service '{service}'. Known: {string.Join(", ", LinkServiceClient.ServicePorts.Keys)}.");

    /// <summary>A top-level property of the response, matched without regard to case, as text.</summary>
    private static string? FindProperty(JsonElement root, string name)
    {
        if (root.ValueKind is not JsonValueKind.Object)
            return null;
        foreach (var property in root.EnumerateObject())
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                return property.Value.ValueKind is JsonValueKind.String ? property.Value.GetString() : property.Value.GetRawText();
        return null;
    }

    /// <summary>Fills {{name}} in a path. Values are escaped for a URL; the path itself starts with a slash.</summary>
    private static string FillPath(string path, Dictionary<string, string> variables) =>
        Fill(path, variables, Uri.EscapeDataString).TrimStart('/');

    /// <summary>Fills {{name}} in a JSON payload. Every placeholder sits inside a JSON string, so values are escaped for one.</summary>
    private static string FillJson(string json, Dictionary<string, string> variables) =>
        Fill(json, variables, value => JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping).ToString());

    private static string Fill(string text, Dictionary<string, string> variables, Func<string, string> encode) =>
        Placeholder().Replace(text, match => variables.TryGetValue(match.Groups[1].Value, out var value)
            ? encode(value)
            : throw new InvalidOperationException($"Nothing provides {{{{{match.Groups[1].Value}}}}}. Add it to 'variables' in {StepsFile} or capture it from an earlier step."));

    [GeneratedRegex(@"\{\{([A-Za-z0-9_]+)\}\}")]
    private static partial Regex Placeholder();

    [GeneratedRegex("^[A-Za-z0-9-]+$")]
    private static partial Regex FacilityIdPattern();
}
