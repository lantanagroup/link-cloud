using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using System.Text.Json;

namespace LantanaGroup.Link.Report.Services
{
    public static class SerializerOptions
    {
        public static readonly JsonSerializerOptions DefaultForFhir = new JsonSerializerOptions().ForFhir();

        public static readonly JsonSerializerOptions ForFhirWithModelInspector = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);

        public static readonly JsonSerializerOptions ForFhirWithModelInspectorNoValidator = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector,
                        new FhirJsonPocoDeserializerSettings { Validator = null });

        public static readonly JsonSerializerOptions ForFhirLenientDeserialization =
            new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector).UsingMode(DeserializerModes.Ostrich);
    }
}
