using Automation.UI.Controllers;
using FluentAssertions;

namespace UnitTests.AutomationUI;

[Trait("Category", "UnitTests")]
public class AcquisitionLogAvailabilityTests
{
    [Fact]
    public void Success_with_no_body_is_a_missing_source()
    {
        AcquisitionLogAvailability.Classify(204, hasBody: false).Unavailable.Should().BeTrue();
        AcquisitionLogAvailability.Classify(200, hasBody: false).Unavailable.Should().BeTrue();
        AcquisitionLogAvailability.Classify(204, hasBody: false).ErrorStatus.Should().BeNull();
    }

    [Fact]
    public void A_body_is_a_normal_page_even_when_the_caller_has_no_rows()
    {
        var read = AcquisitionLogAvailability.Classify(200, hasBody: true);
        read.Unavailable.Should().BeFalse();
        read.ErrorStatus.Should().BeNull();
    }

    [Fact]
    public void A_failed_status_stays_a_load_error()
    {
        AcquisitionLogAvailability.Classify(500, hasBody: false).Unavailable.Should().BeFalse();
        AcquisitionLogAvailability.Classify(500, hasBody: false).ErrorStatus.Should().Be(500);
        AcquisitionLogAvailability.Classify(0, hasBody: false).ErrorStatus.Should().Be(502);
        AcquisitionLogAvailability.Classify(null, hasBody: false).ErrorStatus.Should().Be(502);
    }

    [Fact]
    public void A_missing_log_is_unavailable_and_a_missing_search_is_not()
    {
        AcquisitionLogAvailability.Classify(404, hasBody: false, notFoundIsMissing: true).Unavailable.Should().BeTrue();
        var search = AcquisitionLogAvailability.Classify(404, hasBody: false);
        search.Unavailable.Should().BeFalse();
        search.ErrorStatus.Should().Be(404);
    }
}
