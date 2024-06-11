using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System.Text;
using System.Text.Json;

namespace LantanaGroup.Link.Shared.Application.SerDes
{
    public class MongoFhirBaseSerDes<T> : SerializerBase<T> where T : Base
    {
        public Type ValueType
        {
            get { return typeof(T); }
        }

        public override T Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
        {
            BsonDocument bsonDoc = BsonSerializer.Deserialize<BsonDocument>(context.Reader);
            var dotNetObj = BsonTypeMapper.MapToDotNetValue(bsonDoc);
            var jsonString = JsonSerializer.Serialize(dotNetObj);

            //var options = new JsonSerializerOptions();

            //options.ForFhir(ModelInfo.ModelInspector, new FhirJsonPocoDeserializerSettings()
            //{
            //    DisableBase64Decoding = false,
            //    Validator = null
            //});

            //options.AllowTrailingCommas = true;
            //options.PropertyNameCaseInsensitive = true;

            //return JsonSerializer.Deserialize<T>(jsonString, options);

            var bytes = Encoding.UTF8.GetBytes(jsonString);
            Utf8JsonReader reader = new Utf8JsonReader(bytes);

            return new FhirJsonPocoDeserializer().DeserializeObject<T>(ref reader);
        }

        public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, T resource)
        {
            if (resource == null)
            {
                return;
            }

            //var options = new JsonSerializerOptions().ForFhir(typeof(T).Assembly);
            //string jsonString = JsonSerializer.Serialize(resource, options);

            string jsonString = new FhirJsonSerializer().SerializeToString(resource);
            BsonDocument document = BsonSerializer.Deserialize<BsonDocument>(jsonString);
            BsonSerializer.Serialize(context.Writer, document);
        }
    }
}
