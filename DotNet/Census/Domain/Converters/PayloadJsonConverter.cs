using System.Numerics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Hl7.Fhir.Model;
using LantanaGroup.Link.Census.Application.Interfaces;
using LantanaGroup.Link.Census.Application.Models.Payloads.Cerner;
using LantanaGroup.Link.Census.Application.Models.Payloads.CernerList;
using LantanaGroup.Link.Census.Application.Models.Payloads.Fhir.List;
using LantanaGroup.Link.Census.Domain.Entities.POI;
//using Newtonsoft.Json;
using static Hl7.Fhir.Model.Encounter;

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
        if (rootElement.TryGetProperty("PayloadType", out var payloadTypeElement) && 
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
                //TODO: Daniel - Can FHIRList cases be simplified similar to CernerListAdmit logic below
                case "FHIRListAdmit":
                    string patientId = null;
                    DateTime admitDate = DateTime.MinValue;
                
                    if (rootElement.TryGetProperty("PatientId", out var patientIdProp))
                        patientId = patientIdProp.GetString();
                    
                    if (rootElement.TryGetProperty("AdmitDate", out var admitDateProp) && 
                        admitDateProp.ValueKind == JsonValueKind.String)
                    {
                        DateTime.TryParse(admitDateProp.GetString(), out admitDate);
                    }
                
                    return new FHIRListAdmitPayload(patientId, admitDate);

            
                case "FHIRListDischarge":
                    patientId = null;
                    DateTime dischargeDate = DateTime.MinValue;
                
                    if (rootElement.TryGetProperty("PatientId", out patientIdProp))
                        patientId = patientIdProp.GetString();
                    
                    if (rootElement.TryGetProperty("DischargeDate", out var dischargeDateProp) && 
                        dischargeDateProp.ValueKind == JsonValueKind.String)
                    {
                        DateTime.TryParse(dischargeDateProp.GetString(), out dischargeDate);
                    }
                
                    return new FHIRListDischargePayload(patientId, dischargeDate);
                case "CernerListAdmit":
                    return JsonSerializer.Deserialize<CernerListAdmitPayload>(document);
                case "CernerListDischarge":
                    return JsonSerializer.Deserialize<CernerListDischargePayload>(document);
                case "CernerListUpdate":
                    return JsonSerializer.Deserialize<CernerListUpdatePayload>(document);
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

        // Handle specific payload types
        if (value is FHIRListAdmitPayload admitPayload)
        {
            //TODO: Daniel - Do we need to build the FHIR List payloads? Or can we do something similar to the CernerListAdmitPayload logic below
            writer.WriteStartObject();
            writer.WriteString("PayloadType", value.GetType().Name.Replace("Payload", ""));
            writer.WriteString("PatientId", admitPayload.PatientId);
            writer.WriteString("AdmitDate", admitPayload.AdmitDate.ToString("o"));
            writer.WriteEndObject();
        }
        else if (value is FHIRListDischargePayload dischargePayload)
        {
            writer.WriteStartObject();
            writer.WriteString("PayloadType", value.GetType().Name.Replace("Payload", ""));
            writer.WriteString("PatientId", dischargePayload.PatientId);
            writer.WriteString("DischargeDate", dischargePayload.DischargeDate.ToString("o"));
            writer.WriteEndObject();
        }
        else if (value is CernerListAdmitPayload cernerAdmitPayload)
        {
            writer.WriteRawValue(JsonSerializer.Serialize(cernerAdmitPayload));
        }
        else if (value is CernerListUpdatePayload cernerUpdatePayload) 
        {
            writer.WriteRawValue(JsonSerializer.Serialize(cernerUpdatePayload));
        }
        else if (value is CernerListDischargePayload cernerDischargePayload)
        {
            writer.WriteRawValue(JsonSerializer.Serialize(cernerDischargePayload));
        }
    }
 }