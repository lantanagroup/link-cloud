namespace Automation.UI.Services;

public interface IAutomationUiMetrics
{
    void IncrementPollerHttp(string domain, string outcome);
}
