using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;
using System.Text.Json;

namespace LantanaGroup.Link.MeasureEval.Serializers;

public class BundleSerDes : SerializerBase<Bundle>
{
    public Type ValueType
    {
        get { return typeof(Bundle); }
    }

    public override Bundle Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        BsonDocument bsonDoc = BsonSerializer.Deserialize<BsonDocument>(context.Reader);
        var dotNetObj = BsonTypeMapper.MapToDotNetValue(bsonDoc);
        var jsonString = System.Text.Json.JsonSerializer.Serialize(dotNetObj);
        var bundle = new FhirJsonParser(new ParserSettings { AcceptUnknownMembers = true, PermissiveParsing = true }).Parse<Bundle>(jsonString);
        return bundle;
    }

    public override void Serialize(BsonSerializationContext context, BsonSerializationArgs args, Bundle bundle)
    {
        var options = new JsonSerializerOptions().ForFhir(typeof(Bundle).Assembly);
        string jsonString = JsonSerializer.Serialize(bundle, options);
        MongoDB.Bson.BsonDocument document = BsonSerializer.Deserialize<BsonDocument>(jsonString);
        BsonSerializer.Serialize(context.Writer, document);
    }
}
