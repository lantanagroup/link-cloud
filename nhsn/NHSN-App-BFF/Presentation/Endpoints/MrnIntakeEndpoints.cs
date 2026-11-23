using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Application.Models.Onboarding;

namespace LantanaGroup.Link.Nhsn.App.Bff.Presentation.Endpoints;

// The MRN Identifier Intake step's own answers/rules and the patients its rule builder works
// against — separate from /onboarding because they're normalized server-side (see
// IMrnIntakeService), not part of the onboarding draft.
public class MrnIntakeEndpoints : IApi
{
    public void RegisterEndpoints(WebApplication app)
    {
        var group = app.MapGroup("/api/nhsn-app-bff/mrn-intake")
            .WithTags("MrnIntake")
            .RequireAuthorization("AuthenticatedUser");

        group.MapGet("/", async (IMrnIntakeService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetAsync(cancellationToken)))
            .WithName("GetMrnIntake")
            .Produces<MrnIntakeResponse?>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("The facility's saved MRN Identifier Intake answers and rules.")
            .WithDescription("Null until the facility has saved one.");

        group.MapPut("/", async (
                MrnIntakeResponse intake,
                IMrnIntakeService service,
                CancellationToken cancellationToken) =>
            {
                await service.SaveAsync(intake, cancellationToken);
                return Results.Accepted();
            })
            .WithName("SaveMrnIntake")
            .Produces(StatusCodes.Status202Accepted)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Save the facility's MRN Identifier Intake answers and rules.")
            .WithDescription("Replaces the whole saved object — not a partial patch.");

        group.MapGet("/patient-identifiers", async (IMrnIntakeService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetPatientIdentifiersAsync(cancellationToken)))
            .WithName("GetMrnIntakePatientIdentifiers")
            .Produces<IReadOnlyList<PatientIdentifierResponse>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Patients and their Patient.identifier arrays for the rule builder.")
            .WithDescription("Patient ids come from the facility's generated reports; identifier elements are simulated until a LinkSdk method exists for those — see IPatientIdentifierGateway.");

        group.MapGet("/options", async (IMrnIntakeService service, CancellationToken cancellationToken) =>
                Results.Ok(await service.GetOptionsAsync(cancellationToken)))
            .WithName("GetMrnIntakeOptions")
            .Produces<MrnIntakeOptionsResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("The checkbox option sets for the step's three \"select all that apply\" questions.")
            .WithDescription("Same for every facility — seeded reference data. Each option's labelKey resolves through /localization, same as every other UI string.");
    }
}
