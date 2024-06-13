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
            _optionsWithValidation ??= InitializeForFhirJsonSerializerOptions(true, false);
            return _optionsWithValidation;
        }

        public static JsonSerializerOptions ForFhirWithoutValidation()
        {
            _optionsWithoutValidation ??= InitializeForFhirJsonSerializerOptions(false, false);
            return _optionsWithoutValidation;        
        }
     
        public static JsonSerializerOptions InitializeForFhirJsonSerializerOptions(bool validateFhir = false, bool pretty = false)
        {
            switch(validateFhir)
            {
                case true:
                    { 
                        var options = new JsonSerializerOptions();
                        options.ForFhir(ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings()
                        {
                            DisableBase64Decoding = false,
                            Validator = null                            
                        });
                        options.AllowTrailingCommas = true;
                        options.PropertyNameCaseInsensitive = true;
                        options.WriteIndented = pretty;

                        return options;
                    }
                case false:
                    { 
                        var options = new JsonSerializerOptions();
                        options.ForFhir(ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings()
                        {
                            DisableBase64Decoding = false                            
                        });
                        options.AllowTrailingCommas = true;
                        options.PropertyNameCaseInsensitive = true;
                        options.WriteIndented = pretty;

                        return options;                    
                    }                                  
            } 
        }
    }
}
