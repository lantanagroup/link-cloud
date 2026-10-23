using System.Globalization;
using System.Text;
using LantanaGroup.Link.PatientFlowSimulator;
using LantanaGroup.Link.PatientFlowSimulator.Generation;
using LantanaGroup.Link.PatientFlowSimulator.Import;
using LantanaGroup.Link.PatientFlowSimulator.Planning;
using LantanaGroup.Link.PatientFlowSimulator.Reporting;
using LantanaGroup.Link.PatientFlowSimulator.Scenario;

const string usage = """
    Patient Flow Simulator - generates a month of synthetic patient flow for three facilities and
    uploads it to a FHIR server one hour at a time.

    usage: PatientFlowSimulator <command> [options]

    commands
      plan      Print the month's roster and expected measure counts. Writes nothing.
      generate  Write the month's hourly files and manifest to the data directory.
      report    Write a Markdown report of the month with a Mermaid chart per facility and per patient
                (generates the month's files first when they do not exist yet).
      import    Upload every hourly file whose hour has elapsed (one scheduled run).
      run       Keep importing on an interval (for docker-compose; use a CronJob in Kubernetes).

    options                               environment variable
      --scenario <path>                   SIM_SCENARIO            scenario file (default: scenarios/default.json)
      --data <dir>                        SIM_DATA_DIR            data directory (default: ./data)
      --month <yyyy-MM>                                           month to plan, generate or report (default: this month)
      --force                                                     regenerate a month that already exists
      --format md|html|both                                       report format (default: md; html renders the charts itself)
      --out <path>                                                report file (default: <data>/<month>/patient-flow-<month>.<format>)
      --fhir <base url>                   SIM_FHIR_BASE_URL       FHIR R4 base URL (import, run)
      --bearer <token>                    SIM_FHIR_BEARER_TOKEN   static bearer token
      --basic <user:password>             SIM_FHIR_BASIC_AUTH     basic credentials
      --now <iso instant>                                         pretend it is this time (import)
      --dry-run                                                   list due files without uploading
      --no-generate                                               do not generate a missing month
      --interval <minutes>                SIM_INTERVAL_MINUTES    minutes between runs (run, default 60)
    """;

try
{
    var arguments = CommandLineArguments.Parse(args);
    if (arguments.Command == null || arguments.Has("help"))
    {
        Console.WriteLine(usage);
        return 0;
    }

    var scenarioPath = arguments.Get("scenario", "SIM_SCENARIO",
                                     Path.Combine(AppContext.BaseDirectory, "scenarios", "default.json"))!;
    var dataDirectory = arguments.Get("data", "SIM_DATA_DIR", Path.Combine(Directory.GetCurrentDirectory(), "data"))!;

    switch (arguments.Command.ToLowerInvariant())
    {
        case "plan":
            return Plan(scenarioPath, ResolveMonth(arguments));
        case "generate":
            return Generate(scenarioPath, dataDirectory, ResolveMonth(arguments), arguments.Has("force"));
        case "report":
            return Report(scenarioPath, dataDirectory, ResolveMonth(arguments), arguments.Get("out"), arguments.Get("format", null, "md")!);
        case "import":
            return await ImportAsync(BuildImportOptions(arguments, scenarioPath, dataDirectory));
        case "run":
            return await RunAsync(arguments, scenarioPath, dataDirectory);
        default:
            Console.Error.WriteLine($"Unknown command '{arguments.Command}'.");
            Console.WriteLine(usage);
            return 2;
    }
}
catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidDataException or InvalidOperationException
                               or FileNotFoundException)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static YearMonth ResolveMonth(CommandLineArguments arguments)
{
    var text = arguments.Get("month");
    return text == null ? YearMonth.Of(DateTimeOffset.UtcNow) : YearMonth.Parse(text);
}

static int Plan(string scenarioPath, YearMonth month)
{
    var scenario = ScenarioDefinition.Load(scenarioPath);
    var output = new HourlyBundleBuilder(scenario).Build(month);

    Console.WriteLine($"Scenario '{scenario.Name}' (seed '{scenario.Seed}'), month {month}");
    Console.WriteLine($"{output.Files.Count} hourly file(s), {output.Manifest.EntryCount} resources, " +
                      $"first {output.Manifest.FirstFile}, last {output.Manifest.LastFile}");

    foreach (var facility in output.Manifest.Facilities)
    {
        Console.WriteLine();
        Console.WriteLine($"== {facility.Name} ({facility.FacilityId}, {facility.TimeZone})");
        Console.WriteLine($"   admitted this month: {facility.PatientsAdmittedThisMonth}, carried over: {facility.PatientsCarriedOver}");

        foreach (var measure in facility.Measures)
        {
            Console.WriteLine($"   {measure}: {facility.ExpectedQualifying[measure]} new qualifying " +
                              $"({facility.ExpectedQualifyingWithCarryOvers[measure]} including carry-overs)");
        }

        Console.WriteLine();
        Console.WriteLine("   patient        name                      cohort                     admit (local)      discharge (local)  cross");
        foreach (var patient in facility.Patients.OrderBy(p => p.AdmitUtc))
        {
            Console.WriteLine($"   {patient.PatientId,-14} {patient.Name,-25} {patient.Cohort,-26} {patient.AdmitLocal,-18} " +
                              $"{patient.DischargeLocal,-18} {(patient.CrossesMonthBoundary ? "yes" : "")}");
        }

        foreach (var patient in facility.CarryOvers.OrderBy(p => p.AdmitUtc))
        {
            Console.WriteLine($"   {patient.PatientId,-14} {patient.Name,-25} {patient.Cohort,-26} {patient.AdmitLocal,-18} " +
                              $"{patient.DischargeLocal,-18} from {patient.PlannedInMonth}");
        }
    }

    return 0;
}

static int Generate(string scenarioPath, string dataDirectory, YearMonth month, bool force)
{
    var scenario = ScenarioDefinition.Load(scenarioPath);
    var output = new HourlyBundleBuilder(scenario).Build(month);
    var directory = MonthWriter.Write(output, dataDirectory, force);

    ConsoleLog.Write($"Wrote month {month}: {output.Files.Count} file(s), {output.Manifest.EntryCount} resources to {directory}");
    return 0;
}

// The report describes the generated data, so a month that has no files yet is generated first.
static int Report(string scenarioPath, string dataDirectory, YearMonth month, string? outputPath, string format)
{
    var formats = format.ToLowerInvariant() switch
    {
        "md" => new[] { "md" },
        "html" => new[] { "html" },
        "both" => new[] { "md", "html" },
        _ => throw new ArgumentException($"--format must be md, html or both (got '{format}').")
    };

    if (outputPath != null && formats.Length > 1)
    {
        throw new ArgumentException("--out names one file; use --format md or --format html with it.");
    }

    var scenario = ScenarioDefinition.Load(scenarioPath);
    var output = new HourlyBundleBuilder(scenario).Build(month);
    var monthDirectory = MonthWriter.MonthDirectory(dataDirectory, month);

    if (!Directory.Exists(monthDirectory))
    {
        MonthWriter.Write(output, dataDirectory, overwrite: false);
        ConsoleLog.Write($"Wrote month {month}: {output.Files.Count} file(s), {output.Manifest.EntryCount} resources to {monthDirectory}");
    }

    var markdown = new PatientFlowReportBuilder(scenario).Build(output);
    foreach (var item in formats)
    {
        var path = outputPath ?? Path.Combine(monthDirectory, $"patient-flow-{month}.{item}");
        var content = item == "html" ? HtmlReportRenderer.Render(markdown, $"Patient flow {month}") : markdown;

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: item == "md"));
        ConsoleLog.Write($"Wrote {item} report for {month} to {Path.GetFullPath(path)}");
    }

    return 0;
}

static ImportOptions BuildImportOptions(CommandLineArguments arguments, string scenarioPath, string dataDirectory)
{
    var nowText = arguments.Get("now");
    var now = nowText == null
        ? DateTimeOffset.UtcNow
        : DateTimeOffset.Parse(nowText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal);

    return new ImportOptions
    {
        ScenarioPath = scenarioPath,
        DataDirectory = dataDirectory,
        FhirBaseUrl = arguments.Require("fhir", "SIM_FHIR_BASE_URL"),
        BearerToken = arguments.Get("bearer", "SIM_FHIR_BEARER_TOKEN"),
        BasicCredentials = arguments.Get("basic", "SIM_FHIR_BASIC_AUTH"),
        Now = now,
        GenerateMissingMonths = !arguments.Has("no-generate"),
        DryRun = arguments.Has("dry-run")
    };
}

static async Task<int> ImportAsync(ImportOptions options)
{
    var result = await new ImportRunner(options, ConsoleLog.Write).RunAsync(CancellationToken.None);
    return result.Succeeded ? 0 : 1;
}

static async Task<int> RunAsync(CommandLineArguments arguments, string scenarioPath, string dataDirectory)
{
    var minutes = int.Parse(arguments.Get("interval", "SIM_INTERVAL_MINUTES", "60")!, CultureInfo.InvariantCulture);
    var interval = TimeSpan.FromMinutes(Math.Max(1, minutes));
    using var shutdown = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) =>
    {
        e.Cancel = true;
        shutdown.Cancel();
    };

    ConsoleLog.Write($"Importing every {interval.TotalMinutes:F0} minute(s). Ctrl+C to stop.");
    while (!shutdown.IsCancellationRequested)
    {
        try
        {
            var options = BuildImportOptions(arguments, scenarioPath, dataDirectory);
            await new ImportRunner(options, ConsoleLog.Write).RunAsync(shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
        catch (Exception ex)
        {
            ConsoleLog.Write($"Run failed: {ex.Message}");
        }

        try
        {
            await Task.Delay(interval, shutdown.Token);
        }
        catch (OperationCanceledException)
        {
            break;
        }
    }

    ConsoleLog.Write("Stopped.");
    return 0;
}
