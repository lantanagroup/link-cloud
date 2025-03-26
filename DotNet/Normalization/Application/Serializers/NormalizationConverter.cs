using LantanaGroup.Link.Normalization.Application.Models;
using Newtonsoft.Json;
using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Application.Serializers;

public class NormalizationConverter : System.Text.Json.Serialization.JsonConverter<NormalizationConfigModel>
{
    public override NormalizationConfigModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            JsonElement root = doc.RootElement;
            var model = NormalizationConfigModelDeserializer.Deserialize(root);
            return model;
        }
    }

    public override void Write(Utf8JsonWriter writer, NormalizationConfigModel value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }


    /* public override void Write(Utf8JsonWriter writer, NormalizationConfigModel value, JsonSerializerOptions options)
     {
         //throw new NotImplementedException();
          var jsonSettings = new JsonSerializerSettings
          {
              TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
              TypeNameHandling = TypeNameHandling.Auto
          };

          var str = JsonConvert.SerializeObject(value, jsonSettings);
 

          writer.WriteStringValue(str);
         // writer.WriteStartObject();
     }*/

    /* public override INormalizationOperation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
     {
         using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
         {
             var root = doc.RootElement;

             if (!root.TryGetProperty("$type", out JsonElement typeElement))
                 throw new System.Text.Json.JsonException("Missing $type discriminator.");

             string typeName = typeElement.GetString() ?? throw new System.Text.Json.JsonException("Invalid $type value.");

             // Deserialize based on the short type name
             return typeName switch
             {
                 "ConceptMapOperation" => System.Text.Json.JsonSerializer.Deserialize<ConceptMapOperation>(root.GetRawText(), options),
                 "CopyElementOperation" => System.Text.Json.JsonSerializer.Deserialize<CopyElementOperation>(root.GetRawText(), options),
                 _ => throw new System.Text.Json.JsonException($"Unknown type: {typeName}")
             };
         }
     }

     // Serialize: Write the short class name for $type
     public override void Write(Utf8JsonWriter writer, INormalizationOperation value, JsonSerializerOptions options)
     {
         // Use only the short type name
         var typeName = value.GetType().Name;

         // Serialize the object with the short type name in "$type"
         writer.WriteStartObject();
         writer.WriteString("$type", typeName);

         // Serialize the rest of the properties
         System.Text.Json.JsonSerializer.Serialize(writer, value, value.GetType(), options);

         writer.WriteEndObject();
     }
 */
}

