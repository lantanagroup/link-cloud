using FluentAssertions;
using LantanaGroup.Automation.Generation;

namespace UnitTests.Automation;

[Trait("Category", "UnitTests")]
public class FhirGenerationPipelineLiveAppendTests
{
    [Fact]
    public void TryInferRunTag_reads_generated_patient_id_shape()
    {
        FhirGenerationPipeline.TryInferRunTag(["import-1", "Patient-abcd1234-002", "Patient-nothex!!-001"])
            .Should().Be("abcd1234");
        FhirGenerationPipeline.TryInferRunTag(["upload-1"]).Should().BeNull();
    }

    [Fact]
    public void NextGeneratedPatientIndex_continues_after_highest_ordinal()
    {
        FhirGenerationPipeline.NextGeneratedPatientIndex(
                ["Patient-abcd1234-001", "import-9", "Patient-abcd1234-003"],
                "abcd1234")
            .Should().Be(3);
        FhirGenerationPipeline.NextGeneratedPatientIndex([], "abcd1234").Should().Be(0);
    }
}
