using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Http;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Entities;
using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Domain.Application.Serializers;

public class QueryPlanConverter : System.Text.Json.Serialization.JsonConverter<QueryPlan>
{
    public override QueryPlan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
                TypeNameHandling = TypeNameHandling.Auto
            };

            var jsonOptions = new JsonSerializerOptions();
            jsonOptions.Converters.Add(new JsonStringEnumConverter());

            JsonElement root = doc.RootElement;
            QueryPlan queryPlan = JsonConvert.DeserializeObject<QueryPlan>(root.ToString(), jsonSettings);

            return queryPlan;
        }
    }

    public override void Write(Utf8JsonWriter writer, QueryPlan value, JsonSerializerOptions options)
    {
        var jsonSettings = new JsonSerializerSettings
        {
            TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
            TypeNameHandling = TypeNameHandling.Auto
        };

        var str = JsonConvert.SerializeObject(value, jsonSettings);

        writer.WriteRawValue(str);
    }
}

public class QueryPlanPutModelConverter : System.Text.Json.Serialization.JsonConverter<QueryPlanPutModel>
{
    public override QueryPlanPutModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
                TypeNameHandling = TypeNameHandling.Auto
            };

            var jsonOptions = new JsonSerializerOptions();
            jsonOptions.Converters.Add(new JsonStringEnumConverter());

            JsonElement root = doc.RootElement;
            QueryPlanPutModel queryPlan = JsonConvert.DeserializeObject<QueryPlanPutModel>(root.ToString(), jsonSettings);

            return queryPlan;
        }
    }

    public override void Write(Utf8JsonWriter writer, QueryPlanPutModel value, JsonSerializerOptions options)
    {
        var jsonSettings = new JsonSerializerSettings
        {
            TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
            TypeNameHandling = TypeNameHandling.Auto
        };

        var str = JsonConvert.SerializeObject(value, jsonSettings);

        writer.WriteRawValue(str);
    }
}

public class QueryPlanPostModelConverter : System.Text.Json.Serialization.JsonConverter<QueryPlanPostModel>
{
    public override QueryPlanPostModel Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using (JsonDocument doc = JsonDocument.ParseValue(ref reader))
        {
            var jsonSettings = new JsonSerializerSettings
            {
                TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
                TypeNameHandling = TypeNameHandling.Auto
            };

            var jsonOptions = new JsonSerializerOptions();
            jsonOptions.Converters.Add(new JsonStringEnumConverter());

            JsonElement root = doc.RootElement;
            QueryPlanPostModel queryPlan = JsonConvert.DeserializeObject<QueryPlanPostModel>(root.ToString(), jsonSettings);

            return queryPlan;
        }
    }

    public override void Write(Utf8JsonWriter writer, QueryPlanPostModel value, JsonSerializerOptions options)
    {
        var jsonSettings = new JsonSerializerSettings
        {
            TypeNameAssemblyFormat = System.Runtime.Serialization.Formatters.FormatterAssemblyStyle.Simple,
            TypeNameHandling = TypeNameHandling.Auto
        };

        var str = JsonConvert.SerializeObject(value, jsonSettings);

        writer.WriteRawValue(str);
    }
}