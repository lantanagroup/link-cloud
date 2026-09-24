using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.SerDes
{
    public static class LinkFhirSerializerOptions
    {
        private static JsonSerializerOptions? _optionsWithValidation;
        private static JsonSerializerOptions? _optionsWithoutValidation;

        public static JsonSerializerOptions ForFhirWithValidation()
        {
            _optionsWithValidation ??= InitializeForFhirJsonSerializerOptions(validateFhir: true, pretty: false);
            return _optionsWithValidation;
        }

        public static JsonSerializerOptions ForFhirWithoutValidation()
        {
            _optionsWithoutValidation ??= InitializeForFhirJsonSerializerOptions(validateFhir: false, pretty: false);
            return _optionsWithoutValidation;
        }

        private static FhirJsonConverterOptions PermissiveConverterOptions { get; } = new()
        {
            Validator = null
        };

        public static readonly JsonSerializerOptions ForFhirLenientSerialization =
            new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector, PermissiveConverterOptions)
                .UsingMode(DeserializationMode.Ostrich);

        /// <summary>
        /// Permissive POCO deserializer. Replaces Firely 5 <c>FhirJsonParser</c> + <c>PermissiveParsing</c>.
        /// </summary>
        public static readonly FhirJsonDeserializer FhirJsonDeserializerPermissive =
            new(new DeserializerSettings().UsingMode(DeserializationMode.Ostrich));

        /// <summary>
        /// Back-compat name for existing call sites. Type is now <see cref="FhirJsonDeserializer"/>.
        /// </summary>
        public static FhirJsonDeserializer FhirJsonParserPermissive => FhirJsonDeserializerPermissive;

        public static JsonSerializerOptions InitializeForFhirJsonSerializerOptions(bool validateFhir = false, bool pretty = false)
        {
            var options = new JsonSerializerOptions
            {
                AllowTrailingCommas = true,
                PropertyNameCaseInsensitive = true,
                WriteIndented = pretty
            };

            if (validateFhir)
            {
                options.ForFhir(ModelInfo.ModelInspector, new FhirJsonConverterOptions());
            }
            else
            {
                options.ForFhir(ModelInfo.ModelInspector, PermissiveConverterOptions);
            }

            return options;
        }

        public static JsonSerializerOptions ActivityTagging { get; } = new()
        {
            ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve,
            WriteIndented = true,
            Converters =
            {
                new System.Text.Json.Serialization.JsonStringEnumConverter()
            }
        };

    }

    public static class FhirJsonDeserializerExtensions
    {
        /// <summary>
        /// Firely 5 <c>FhirJsonParser.Parse&lt;T&gt;</c> equivalent.
        /// </summary>
        public static T Parse<T>(this FhirJsonDeserializer deserializer, string json) where T : Base
            => deserializer.Deserialize<T>(json);
    }
}
