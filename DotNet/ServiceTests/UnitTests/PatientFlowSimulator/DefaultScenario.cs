using LantanaGroup.Link.PatientFlowSimulator.Scenario;

namespace UnitTests.PatientFlowSimulator;

/// <summary>
/// The scenario shipped with the simulator, as copied to the test output by the project reference.
/// </summary>
internal static class DefaultScenario
{
    public static string Path => System.IO.Path.Combine(AppContext.BaseDirectory, "scenarios", "default.json");

    public static ScenarioDefinition Load() => ScenarioDefinition.Load(Path);
}
