using System.Globalization;
using System.Text;
using LantanaGroup.Link.PatientFlowSimulator.Generation;
using LantanaGroup.Link.PatientFlowSimulator.Planning;
using LantanaGroup.Link.PatientFlowSimulator.Scenario;

namespace LantanaGroup.Link.PatientFlowSimulator.Reporting;

/// <summary>
/// Renders a month as a Markdown document with Mermaid Gantt charts: every stay per facility, the
/// census lists day by day, and one journey per patient. Built from the same plan the files come from,
/// so it describes the generated data exactly without reading the files back.
/// </summary>
public sealed class PatientFlowReportBuilder
{
    private const string LocalFormat = "yyyy-MM-dd HH:mm";

    private readonly ScenarioDefinition _scenario;

    public PatientFlowReportBuilder(ScenarioDefinition scenario)
    {
        _scenario = scenario;
    }

    /// <summary>
    /// The report for one month.
    /// </summary>
    public string Build(YearMonth month) => Build(new HourlyBundleBuilder(_scenario).Build(month));

    /// <summary>
    /// The report for a month that has already been built, so the files and the report come from one plan.
    /// </summary>
    public string Build(MonthOutput output)
    {
        var month = output.Month;
        var sb = new StringBuilder();

        WriteHeader(sb, output);
        WriteSummary(sb, output);
        WriteLegend(sb);

        foreach (var facility in _scenario.Facilities)
        {
            var zone = LocalTime.Resolve(facility.TimeZone);
            var manifest = output.Manifest.Facilities.First(f => f.FacilityId == facility.FacilityId);
            var stays = output.Plan.StaysFor(facility).OrderBy(s => s.AdmitUtc).ToList();

            WriteFacilityHeader(sb, facility, manifest);
            WriteStaysChart(sb, facility, zone, output.Plan, month);
            WriteCensusByDay(sb, facility, zone, stays, month);

            sb.AppendLine("### Patients");
            sb.AppendLine();
            foreach (var stay in stays)
            {
                WritePatient(sb, stay, zone, month);
            }
        }

        return sb.ToString();
    }

    private void WriteHeader(StringBuilder sb, MonthOutput output)
    {
        var month = output.Month;
        sb.AppendLine($"# Patient flow, {MonthName(month)}");
        sb.AppendLine();
        sb.AppendLine($"Scenario `{_scenario.Name}` (seed `{_scenario.Seed}`). " +
                      $"{output.Plan.Stays.Count} patients admitted this month, {output.Plan.CarryOvers.Count} carried over " +
                      $"from earlier months. {output.Files.Count} hourly files, {output.Manifest.EntryCount:N0} resources, " +
                      $"from `{output.Manifest.FirstFile}` to `{output.Manifest.LastFile}`.");
        sb.AppendLine();
    }

    private static void WriteSummary(StringBuilder sb, MonthOutput output)
    {
        sb.AppendLine("| Facility | Time zone | New patients | Carried over | Cross-month | Qualifying per measure (new / including carry-overs) |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var facility in output.Manifest.Facilities)
        {
            var crossMonth = facility.Patients.Count(p => p.CrossesMonthBoundary);
            var measures = string.Join("<br>", facility.Measures.Select(m =>
                $"{m}: {facility.ExpectedQualifying[m]} / {facility.ExpectedQualifyingWithCarryOvers[m]}"));
            sb.AppendLine($"| {facility.Name} (`{facility.FacilityId}`) | {facility.TimeZone} | {facility.PatientsAdmittedThisMonth} " +
                          $"| {facility.PatientsCarriedOver} | {crossMonth} | {measures} |");
        }

        sb.AppendLine();
    }

    private static void WriteLegend(StringBuilder sb)
    {
        sb.AppendLine("## How to read this");
        sb.AppendLine();
        sb.AppendLine("- Times are the facility's local time. Each facility's first chart shows every stay as a bar from " +
                      "admission to discharge; red bars cross the month boundary, grey bars were admitted in an earlier month.");
        sb.AppendLine("- Link reads six census lists per facility. A patient sits on one Admit list from admission " +
                      "(under 24 hours, 24 to 48 hours, over 48 hours since admission) and then on one Discharge list " +
                      "for 72 hours after discharge, then drops off. Each patient's chart shows which list they are on when.");
        sb.AppendLine("- The hourly file named for a patient is the one that creates them; everything later for that " +
                      "patient lands in the file of the hour it happens in.");
        sb.AppendLine();
        sb.AppendLine("An example: a stay of two days and six hours, and the lists it passes through.");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("gantt");
        sb.AppendLine("    title Example stay and its census lists");
        sb.AppendLine("    dateFormat YYYY-MM-DD HH:mm");
        sb.AppendLine("    axisFormat day %d %H:%M");
        sb.AppendLine("    todayMarker off");
        sb.AppendLine("    section Stay");
        sb.AppendLine($"    In the facility :active, {ExampleTime(0)}, {ExampleTime(54)}");
        sb.AppendLine("    section Census lists");
        foreach (var phase in CensusPhase.For(ExampleStay()))
        {
            sb.AppendLine($"    {phase.Label} :{Example(phase.StartUtc)}, {Example(phase.EndUtc)}");
        }

        sb.AppendLine("```");
        sb.AppendLine();
    }

    // A stay of 54 hours starting on an arbitrary day, used only to draw the legend.
    private static readonly DateTimeOffset ExampleAdmit = new(2026, 1, 1, 9, 0, 0, TimeSpan.Zero);

    private static PatientStay ExampleStay() => new()
    {
        Facility = new FacilityDefinition { FacilityId = "example", Name = "Example", PatientIdPrefix = "EX" },
        Cohort = new CohortDefinition { Name = "example", Template = "example", Count = 1 },
        Template = new ClinicalTemplate(),
        PlanMonth = YearMonth.Of(ExampleAdmit),
        Index = 0,
        Seed = "example",
        Identity = new PatientIdentity("EX0000000000", "Example", "Patient", "female", new DateOnly(1960, 1, 1)),
        AdmitUtc = ExampleAdmit,
        DischargeUtc = ExampleAdmit.AddHours(54),
        ForcedCrossMonth = false,
        CrossesMonthBoundary = false
    };

    private static string ExampleTime(int hoursAfterAdmit) => Example(ExampleAdmit.AddHours(hoursAfterAdmit));

    private static string Example(DateTimeOffset utc) => utc.ToString(LocalFormat, CultureInfo.InvariantCulture);

    private static void WriteFacilityHeader(StringBuilder sb, FacilityDefinition facility, FacilityManifest manifest)
    {
        sb.AppendLine($"## {facility.Name} (`{facility.FacilityId}`)");
        sb.AppendLine();
        sb.AppendLine($"Measures: {string.Join(", ", facility.Measures)}. " +
                      $"{manifest.PatientsAdmittedThisMonth} patients admitted this month, {manifest.PatientsCarriedOver} carried over. " +
                      $"Month runs {manifest.MonthStartUtc} to {manifest.MonthEndUtc} (UTC).");
        sb.AppendLine();
        sb.AppendLine("| Cohort | Template | Patients | Cross-month | Qualifies for |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var cohort in facility.Cohorts)
        {
            var qualifies = cohort.QualifiesFor.Count == 0 ? "nothing" : string.Join(", ", cohort.QualifiesFor);
            sb.AppendLine($"| {cohort.Name} | `{cohort.Template}` | {cohort.Count} | {cohort.CrossMonthCount} | {qualifies} |");
        }

        sb.AppendLine();
    }

    private static void WriteStaysChart(StringBuilder sb, FacilityDefinition facility, TimeZoneInfo zone, MonthPlan plan, YearMonth month)
    {
        sb.AppendLine("### Stays");
        sb.AppendLine();
        sb.AppendLine("```mermaid");
        sb.AppendLine("gantt");
        sb.AppendLine($"    title {Mermaid(facility.Name)}, {MonthName(month)} (local time)");
        sb.AppendLine("    dateFormat YYYY-MM-DD HH:mm");
        sb.AppendLine("    axisFormat %d");
        sb.AppendLine("    todayMarker off");

        foreach (var cohort in facility.Cohorts)
        {
            var stays = plan.Stays
                .Where(s => SameFacility(s, facility) && s.Cohort.Name == cohort.Name)
                .OrderBy(s => s.AdmitUtc)
                .ToList();
            if (stays.Count == 0)
            {
                continue;
            }

            sb.AppendLine($"    section {Mermaid(cohort.Name)}");
            foreach (var stay in stays)
            {
                var tag = stay.CrossesMonthBoundary ? "crit, " : "";
                sb.AppendLine($"    {TaskName(stay)} :{tag}{Local(stay.AdmitUtc, zone)}, {Local(stay.DischargeUtc, zone)}");
            }
        }

        var carried = plan.CarryOvers.Where(s => SameFacility(s, facility)).OrderBy(s => s.AdmitUtc).ToList();
        if (carried.Count > 0)
        {
            sb.AppendLine("    section Carried over");
            foreach (var stay in carried)
            {
                sb.AppendLine($"    {TaskName(stay)} :done, {Local(stay.AdmitUtc, zone)}, {Local(stay.DischargeUtc, zone)}");
            }
        }

        sb.AppendLine("```");
        sb.AppendLine();
    }

    // Counts at the end of each local day, which is what the lists look like after that day's last upload.
    private static void WriteCensusByDay(StringBuilder sb, FacilityDefinition facility, TimeZoneInfo zone, IReadOnlyList<PatientStay> stays, YearMonth month)
    {
        sb.AppendLine("### Census lists at the end of each day");
        sb.AppendLine();
        sb.AppendLine("| Day | Admit under 24h | Admit 24 to 48h | Admit over 48h | Discharge under 24h | Discharge 24 to 48h | Discharge over 48h | In house |");
        sb.AppendLine("|---|---|---|---|---|---|---|---|");

        for (var day = 1; day <= month.Days; day++)
        {
            var endOfDay = LocalTime.ToUtc(month.LocalStart.AddDays(day), zone).AddTicks(-1);
            var lists = CensusListBuilder.Build(facility, stays, endOfDay);
            var counts = lists.Select(l => l.Entries.Count).ToList();
            var inHouse = counts[0] + counts[1] + counts[2];
            sb.AppendLine($"| {month.Year:D4}-{month.Month:D2}-{day:D2} | {string.Join(" | ", counts)} | {inHouse} |");
        }

        sb.AppendLine();
    }

    private static void WritePatient(StringBuilder sb, PatientStay stay, TimeZoneInfo zone, YearMonth month)
    {
        var identity = stay.Identity;
        var template = stay.Template;
        var carriedOver = stay.PlanMonth != month;
        var admitLocal = LocalTime.ToLocal(stay.AdmitUtc, zone);
        var dischargeLocal = LocalTime.ToLocal(stay.DischargeUtc, zone);
        var lengthOfStay = stay.DischargeUtc - stay.AdmitUtc;
        var events = ClinicalEventPlanner.EventsFor(stay);
        var conditions = FhirResourceFactory.BuildConditions(stay);
        var qualifies = stay.Cohort.QualifiesFor.Count == 0 ? "no measure" : string.Join(", ", stay.Cohort.QualifiesFor);
        var age = AgeAt(identity.BirthDate, DateOnly.FromDateTime(admitLocal.DateTime));

        sb.AppendLine($"#### {identity.PatientId}, {identity.DisplayName}");
        sb.AppendLine();
        sb.AppendLine($"- Cohort `{stay.Cohort.Name}`, template `{stay.Cohort.Template}`, qualifies for {qualifies}" +
                      (carriedOver ? $"; admitted in {MonthName(stay.PlanMonth)}" : "") +
                      (stay.CrossesMonthBoundary && !carriedOver ? $"; crosses into {MonthName(month.AddMonths(1))}" : ""));
        sb.AppendLine($"- {identity.Gender}, born {identity.BirthDate:yyyy-MM-dd} ({age} at admission)");
        sb.AppendLine($"- Admitted {admitLocal.ToString("ddd yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)}, " +
                      $"discharged {dischargeLocal.ToString("ddd yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)} " +
                      $"({Duration(lengthOfStay)}); class {template.EncounterClass}" +
                      (template.Location != null ? $", {template.Location.Name} (HSLOC {template.Location.Hsloc})" : ", no location") +
                      $"; discharge disposition {template.DischargeDisposition.Display}");
        sb.AppendLine($"- Diagnoses: {string.Join("; ", conditions.Select(c => c.Code?.Text ?? "unknown"))}");
        sb.AppendLine($"- Clinical data: {ClinicalSummary(stay, events)}");
        sb.AppendLine($"- Files: {FileSummary(stay, zone, month)}");
        sb.AppendLine();

        sb.AppendLine("```mermaid");
        sb.AppendLine("gantt");
        sb.AppendLine($"    title {Mermaid(identity.DisplayName)} ({identity.PatientId})");
        sb.AppendLine("    dateFormat YYYY-MM-DD HH:mm");
        sb.AppendLine("    axisFormat %m-%d %H:%M");
        sb.AppendLine("    todayMarker off");
        sb.AppendLine("    section Stay");
        var stayLabel = template.Location != null ? $"{template.Location.Name} (class {template.EncounterClass})" : $"Class {template.EncounterClass}";
        sb.AppendLine($"    {Mermaid(stayLabel)} :active, {Local(stay.AdmitUtc, zone)}, {Local(stay.DischargeUtc, zone)}");
        sb.AppendLine("    section Census lists");
        foreach (var phase in CensusPhase.For(stay))
        {
            sb.AppendLine($"    {phase.Label} :{Local(phase.StartUtc, zone)}, {Local(phase.EndUtc, zone)}");
        }

        var observations = events.Where(e => e.Kind == StayEventKind.Observation).ToList();
        var administrations = events.Where(e => e.Kind == StayEventKind.MedicationAdministration).ToList();
        if (observations.Count > 0 || administrations.Count > 0)
        {
            sb.AppendLine("    section Clinical data");
            AppendActivity(sb, $"{observations.Count} observations", observations, zone);
            AppendActivity(sb, $"{administrations.Count} medication administrations", administrations, zone);
        }

        sb.AppendLine("```");
        sb.AppendLine();
    }

    private static void AppendActivity(StringBuilder sb, string label, IReadOnlyList<StayEvent> events, TimeZoneInfo zone)
    {
        if (events.Count == 0)
        {
            return;
        }

        var first = events.Min(e => e.WhenUtc);
        var last = events.Max(e => e.WhenUtc);
        if (last < first.AddHours(1))
        {
            last = first.AddHours(1);
        }

        sb.AppendLine($"    {label} :{Local(first, zone)}, {Local(last, zone)}");
    }

    private static string ClinicalSummary(PatientStay stay, IReadOnlyList<StayEvent> events)
    {
        var parts = new List<string>();
        foreach (var spec in stay.Template.Observations)
        {
            var count = events.Count(e => e.Kind == StayEventKind.Observation && e.Observation == spec);
            parts.Add($"{count} x {spec.Display}");
        }

        foreach (var spec in stay.Template.Medications)
        {
            var count = events.Count(e => e.Kind == StayEventKind.MedicationAdministration && e.Medication == spec);
            parts.Add($"{count} x {spec.Display} ({spec.DoseValue.ToString(CultureInfo.InvariantCulture)} {spec.DoseUnit} every {spec.EveryHours}h)");
        }

        return parts.Count == 0 ? "none" : string.Join(", ", parts);
    }

    private static string FileSummary(PatientStay stay, TimeZoneInfo zone, YearMonth month)
    {
        var startUtc = LocalTime.MonthStartUtc(month, zone);
        var endUtc = LocalTime.MonthEndUtc(month, zone);
        var admitFile = MonthWriter.FileName(LocalTime.FloorToHour(stay.AdmitUtc));
        var dischargeFile = MonthWriter.FileName(LocalTime.FloorToHour(stay.DischargeUtc));

        var admitted = stay.AdmitUtc < startUtc
            ? $"admitted in `{admitFile}` ({MonthName(stay.PlanMonth)}), re-sent in `{MonthWriter.FileName(LocalTime.FloorToHour(startUtc))}`"
            : $"admitted in `{admitFile}`";
        var discharged = stay.DischargeUtc >= endUtc || stay.DischargeUtc < startUtc
            ? $"discharged in `{dischargeFile}` ({MonthName(YearMonth.Of(LocalTime.ToLocal(stay.DischargeUtc, zone)))})"
            : $"discharged in `{dischargeFile}`";

        return $"{admitted}, {discharged}";
    }

    private static string TaskName(PatientStay stay) => Mermaid($"{stay.Identity.DisplayName} ({stay.PatientId})");

    // Gantt task names end at the first colon and their data is comma-separated, so those and the
    // comment marker are kept out of generated names.
    private static string Mermaid(string text)
        => text.Replace(':', '-').Replace(',', ' ').Replace(';', ' ').Replace('#', ' ').Replace("%%", "").Trim();

    private static string Local(DateTimeOffset utc, TimeZoneInfo zone)
        => LocalTime.ToLocal(utc, zone).ToString(LocalFormat, CultureInfo.InvariantCulture);

    private static string MonthName(YearMonth month)
        => month.LocalStart.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    private static string Duration(TimeSpan span)
    {
        if (span.TotalHours < 24)
        {
            return $"{span.TotalHours:F0} h";
        }

        return $"{(int)span.TotalDays} d {span.Hours} h";
    }

    private static int AgeAt(DateOnly birthDate, DateOnly date)
    {
        var age = date.Year - birthDate.Year;
        if (date < birthDate.AddYears(age))
        {
            age--;
        }

        return age;
    }

    private static bool SameFacility(PatientStay stay, FacilityDefinition facility)
        => string.Equals(stay.Facility.FacilityId, facility.FacilityId, StringComparison.OrdinalIgnoreCase);
}
