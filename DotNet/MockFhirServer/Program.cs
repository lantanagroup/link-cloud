using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.MockFhirServer.Services;
using LantanaGroup.Link.MockFhirServer.Settings;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<MockFhirServerSettings>(
    builder.Configuration.GetSection(MockFhirServerSettings.ConfigSectionName));

builder.Services.AddSingleton<PatientDataStore>();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Link Mock FHIR Server", Version = "v1" });
});

var app = builder.Build();

// Warm up the data store eagerly so the first request is not slow.
_ = app.Services.GetRequiredService<PatientDataStore>();

if (app.Environment.IsDevelopment() || app.Configuration.GetValue<bool>("EnableSwagger"))
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// ------------------------------------------------------------------ //
//  Health                                                              //
// ------------------------------------------------------------------ //

app.MapGet("/health", () => Results.Ok(new { status = "Healthy" }))
    .WithName("Health")
    .ExcludeFromDescription();

// ------------------------------------------------------------------ //
//  Pre-generated patient IDs                                          //
// ------------------------------------------------------------------ //

app.MapGet("/api/mock-fhir/patients", (PatientDataStore store) =>
    Results.Content(string.Join('\n', store.PreGeneratedPatientIds), "text/plain"))
    .WithName("GetPreGeneratedPatients")
    .WithSummary("Returns the IDs of the patients pre-generated at startup, one per line.")
    .Produces<string>(contentType: "text/plain");

// ------------------------------------------------------------------ //
//  FHIR metadata (CapabilityStatement)                               //
// ------------------------------------------------------------------ //

app.MapGet("/fhir/metadata", () =>
{
    var cs = new CapabilityStatement
    {
        Status = PublicationStatus.Active,
        FhirVersion = FHIRVersion.N4_0_1,
        Format = ["json"],
        Kind = CapabilityStatementKind.Instance,
        Software = new CapabilityStatement.SoftwareComponent
        {
            Name = "Link Mock FHIR Server",
            Version = "1.0"
        },
        Rest =
        [
            new CapabilityStatement.RestComponent
            {
                Mode = CapabilityStatement.RestfulCapabilityMode.Server,
            }
        ]
    };

    return WriteFhirResponse(cs);
})
.WithName("FhirMetadata")
.WithSummary("FHIR R4 CapabilityStatement")
.ExcludeFromDescription();

// ------------------------------------------------------------------ //
//  FHIR Read: GET /fhir/{resourceType}/{id}                          //
// ------------------------------------------------------------------ //

app.MapGet("/fhir/{resourceType}/{id}", async (
    string resourceType,
    string id,
    PatientDataStore store,
    CancellationToken ct) =>
{
    // For Patient reads, generate on demand so DataAcquisition can query
    // any arbitrary patient ID and always receive valid data back.
    if (string.Equals(resourceType, "Patient", StringComparison.OrdinalIgnoreCase))
    {
        await store.EnsurePatientGeneratedAsync(id, ct);
    }

    var resource = store.GetById(resourceType, id);
    if (resource == null)
        return Results.NotFound(FhirNotFound(resourceType, id));

    return WriteFhirResponse(resource);
})
.WithName("FhirRead")
.WithSummary("FHIR R4 read — returns a single resource by type and ID.")
.Produces(200)
.Produces(404);

// ------------------------------------------------------------------ //
//  FHIR Search: GET /fhir/{resourceType}?...                         //
// ------------------------------------------------------------------ //

app.MapGet("/fhir/{resourceType}", async (
    string resourceType,
    [FromQuery(Name = "subject")] string? subject,
    [FromQuery(Name = "patient")] string? patient,
    [FromQuery(Name = "_id")] string? idParam,
    PatientDataStore store,
    CancellationToken ct) =>
{
    var patientId = ExtractPatientId(subject ?? patient);
    return await ExecuteFhirSearch(resourceType, patientId, idParam, store, ct);
})
.WithName("FhirSearch")
.WithSummary("FHIR R4 GET search — returns a Bundle of matching resources.")
.Produces(200);

// ------------------------------------------------------------------ //
//  FHIR Search (POST): POST /fhir/{resourceType}/_search             //
//  The Firely SDK sends SearchUsingPostAsync as                       //
//  application/x-www-form-urlencoded to this endpoint.               //
// ------------------------------------------------------------------ //

app.MapPost("/fhir/{resourceType}/_search", async (
    string resourceType,
    HttpRequest request,
    PatientDataStore store,
    CancellationToken ct) =>
{
    // Read form body regardless of exact Content-Type header casing/charset.
    IFormCollection form;
    try
    {
        form = await request.ReadFormAsync(ct);
    }
    catch
    {
        form = FormCollection.Empty;
    }

    string? subject  = form["subject"].FirstOrDefault() ?? form["subject:Patient"].FirstOrDefault();
    string? patient  = form["patient"].FirstOrDefault();
    string? idParam  = form["_id"].FirstOrDefault();

    var patientId = ExtractPatientId(subject ?? patient);
    return await ExecuteFhirSearch(resourceType, patientId, idParam, store, ct);
})
.WithName("FhirSearchPost")
.WithSummary("FHIR R4 POST search — accepts form-encoded params, returns a Bundle.")
.Produces(200)
.Accepts<IFormCollection>("application/x-www-form-urlencoded");

app.Run();

// ------------------------------------------------------------------ //
//  Helpers                                                            //
// ------------------------------------------------------------------ //

static async Task<IResult> ExecuteFhirSearch(
    string resourceType,
    string? patientId,
    string? idParam,
    PatientDataStore store,
    CancellationToken ct)
{
    var resources = await store.SearchAsync(resourceType, patientId, idParam, ct);

    var bundle = new Bundle
    {
        Type = Bundle.BundleType.Searchset,
        Total = resources.Count,
        Entry = resources
            .Select(r => new Bundle.EntryComponent
            {
                Resource = r,
                Search = new Bundle.SearchComponent
                {
                    Mode = Bundle.SearchEntryMode.Match
                }
            })
            .ToList()
    };

    return WriteFhirResponse(bundle);
}

static IResult WriteFhirResponse(Base resource)
{
    var serializer = new FhirJsonSerializer(new SerializerSettings { Pretty = false });
    var json = serializer.SerializeToString(resource);
    return Results.Content(json, "application/fhir+json; charset=utf-8");
}

static string? ExtractPatientId(string? reference)
{
    if (string.IsNullOrWhiteSpace(reference))
        return null;

    // Handle "Patient/{id}" or "{id}"
    const string prefix = "Patient/";
    if (reference.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        return reference[prefix.Length..];

    return reference;
}

static OperationOutcome FhirNotFound(string resourceType, string id) =>
    new()
    {
        Issue =
        [
            new OperationOutcome.IssueComponent
            {
                Severity = OperationOutcome.IssueSeverity.Error,
                Code = OperationOutcome.IssueType.NotFound,
                Diagnostics = $"{resourceType}/{id} not found."
            }
        ]
    };
