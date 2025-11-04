using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Domain.Entities.POI;

public class PayloadJsonConverter : JsonConverter<IPayload>
{
    private readonly ILogger<PayloadJsonConverter> _logger;
    private static readonly JsonSerializerOptions _defaultOptions = new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public PayloadJsonConverter(ILogger<PayloadJsonConverter> logger = null)
    {
        _logger = logger;
    }

    public override bool HandleNull => true;

    public override IPayload Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        using JsonDocument document = JsonDocument.ParseValue(ref reader);
        var rootElement = document.RootElement;
        string jsonString = rootElement.GetRawText();

        // Try to extract payload type for discriminator-based deserialization
        if (rootElement.TryGetProperty("payloadType", out var payloadTypeElement) && 
            payloadTypeElement.ValueKind == JsonValueKind.String)
        {
            string payloadType = payloadTypeElement.GetString();
        
            // IMPORTANT: Create a new options instance without this converter to avoid infinite recursion
            var newOptions = new JsonSerializerOptions(options);
            // We need to clear the converters list and add all except this one
            var convertersToKeep = options.Converters.Where(c => !(c is PayloadJsonConverter)).ToList();
            newOptions.Converters.Clear();
            foreach (var converter in convertersToKeep)
            {
                newOptions.Converters.Add(converter);
            }
        
            // Now deserialize with the appropriate type based on payloadType
            switch (payloadType)
            {
                case "FHIRListAdmit":
                    string patientId = null;
                    DateTime admitDate = DateTime.MinValue;
                
                    if (rootElement.TryGetProperty("patientId", out var patientIdProp))
                        patientId = patientIdProp.GetString();
                    
                    if (rootElement.TryGetProperty("admitDate", out var admitDateProp) && 
                        admitDateProp.ValueKind == JsonValueKind.String)
                    {
                        DateTime.TryParse(admitDateProp.GetString(), out admitDate);
                    }
                
                    return new FHIRListAdmitPayload(patientId, admitDate);

            
                case "FHIRListDischarge":
                    patientId = null;
                    DateTime dischargeDate = DateTime.MinValue;
                
                    if (rootElement.TryGetProperty("patientId", out patientIdProp))
                        patientId = patientIdProp.GetString();
                    
                    if (rootElement.TryGetProperty("admitDate", out var dischargeDateProp) && 
                        dischargeDateProp.ValueKind == JsonValueKind.String)
                    {
                        DateTime.TryParse(dischargeDateProp.GetString(), out dischargeDate);
                    }
                
                    return new FHIRListDischargePayload(patientId, dischargeDate);
                default:
                    return null;
            }
        }
    
        return null;
    }





    public override void Write(Utf8JsonWriter writer, IPayload value, JsonSerializerOptions options)
    {
        if (value == null)
        {
            writer.WriteNullValue();
            return;
        }
    
        // Start a new JSON object
        writer.WriteStartObject();
    
        // Write the payload type
        writer.WriteString("payloadType", value.GetType().Name.Replace("Payload", ""));
    
        // Handle specific payload types
        if (value is FHIRListAdmitPayload admitPayload)
        {
            writer.WriteString("patientId", admitPayload.PatientId);
            writer.WriteString("admitDate", admitPayload.AdmitDate.ToString("o"));
        }
        else if (value is FHIRListDischargePayload dischargePayload)
        {
            writer.WriteString("patientId", dischargePayload.PatientId);
            writer.WriteString("dischargeDate", dischargePayload.DischargeDate.ToString("o"));
        }
    
        // End the JSON object
        writer.WriteEndObject();
    }
 }