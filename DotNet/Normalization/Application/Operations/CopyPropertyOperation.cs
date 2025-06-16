using Hl7.FhirPath;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.Normalization.Application.Operations
{

    /// <summary>
    /// Defines a copy operation to transfer a value from a source FHIRPath to a target FHIRPath.
    /// </summary>
    public class CopyPropertyOperation : IOperation
    {
        public OperationType OperationType => OperationType.CopyProperty;

     //   [JsonPropertyName("name")]
        public string Name { get; set; }

      //  [JsonPropertyName("description")]
        public string Description { get; set; }
     //   [JsonPropertyName("sourceFhirPath")]
        public string SourceFhirPath { get; set; }
     //   [JsonPropertyName("targetFhirPath")]
        public string TargetFhirPath { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="CopyOperation"/> class.
        /// </summary>
        /// <param name="name">The name of the operation.</param>
        /// <param name="sourceFhirPath">The source FHIRPath expression.</param>
        /// <param name="targetFhirPath">The target FHIRPath expression.</param>
        public CopyPropertyOperation(string name, string sourceFhirPath, string targetFhirPath, string description = "")
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Name must not be null or whitespace.", nameof(name));
            }
            if (string.IsNullOrWhiteSpace(sourceFhirPath))
            {
                throw new ArgumentException("SourceFhirPath must not be null or whitespace.", nameof(sourceFhirPath));
            }
            if (string.IsNullOrWhiteSpace(targetFhirPath))
            {
                throw new ArgumentException("TargetFhirPath must not be null or whitespace.", nameof(targetFhirPath));
            }

            // Validate FHIRPath syntax
            try
            {
                var parser = new FhirPathCompiler();
                parser.Compile(sourceFhirPath);
                parser.Compile(targetFhirPath);
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            {
                throw new ArgumentException($"Invalid FHIRPath syntax: {ex.Message}", ex);
            }

            Name = name;
            SourceFhirPath = sourceFhirPath;
            TargetFhirPath = targetFhirPath;
            Description = description;
        }
    }
}