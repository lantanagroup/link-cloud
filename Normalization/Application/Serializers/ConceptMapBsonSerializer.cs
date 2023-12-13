using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Normalization.Application.Models.Exceptions;
using MongoDB.Bson;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using System.Text.Json;

namespace LantanaGroup.Link.Normalization.Application.Serializers;

public class ConceptMapBsonSerializer : IBsonSerializer
{
    public Type ValueType => typeof(object);

    public object Deserialize(BsonDeserializationContext context, BsonDeserializationArgs args)
    {
        var bsonType = context.Reader.GetCurrentBsonType();
        string? jsonStr = bsonType switch
        {
            BsonType.String => GetJsonStringFromBsonString(context.Reader),
            BsonType.Document => GetJsonStringFromBsonDocument(context.Reader),
            _ => throw new DeserializationUnsupportedTypeException($"Concept Map Deserialization does not support {bsonType} ")
        };
        
        var conceptmap = JsonSerializer.Deserialize<JsonElement>(jsonStr, new JsonSerializerOptions());
        return conceptmap;
    }

    public void Serialize(BsonSerializationContext context, BsonSerializationArgs args, object value)
    {
        var jsonEle = JsonSerializer.Serialize(value, new JsonSerializerOptions().ForFhir());

        BsonSerializer.Serialize(context.Writer, jsonEle);
    }

    private string GetJsonStringFromBsonString(IBsonReader reader) => reader.ReadString();

    private string GetJsonStringFromBsonDocument(IBsonReader reader) => BsonSerializer.Deserialize<BsonDocument>(reader).ToJson();

}
