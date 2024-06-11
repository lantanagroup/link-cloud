using LantanaGroup.Link.DataAcquisition.Application.Interfaces;
using LantanaGroup.Link.DataAcquisition.Application.Models;
using Newtonsoft.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace LantanaGroup.Link.DataAcquisition.Application.Serializers;

public class QueryPlanResultConverter : System.Text.Json.Serialization.JsonConverter<IQueryPlan>
{
    public override IQueryPlan Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
            IQueryPlan queryPlan = null;

            try
            {
                queryPlan = JsonConvert.DeserializeObject<QueryPlanResult>(root.ToString(), jsonSettings);
            }
            catch (Exception) { }

            if (queryPlan != null) return queryPlan;

            try
            {
                queryPlan = JsonConvert.DeserializeObject<InitialQueryResult>(root.ToString(), jsonSettings);
            }
            catch (Exception) { }

            if(queryPlan != null) return queryPlan;

            try
            {
                queryPlan = JsonConvert.DeserializeObject<SupplementalQueryResult>(root.ToString(), jsonSettings);
            }
            catch (Exception) { }

            return queryPlan;
        }
    }

    public override void Write(Utf8JsonWriter writer, IQueryPlan value, JsonSerializerOptions options)
    {
        throw new NotImplementedException();
    }
}


