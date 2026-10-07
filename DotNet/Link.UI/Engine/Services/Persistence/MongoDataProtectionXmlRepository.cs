using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace Automation.UI.Services.Persistence;

public sealed class MongoDataProtectionOptions
{
    public string ApplicationName { get; init; } = string.Empty;
    public string KeyCollectionName { get; init; } = "automation_data_protection_keys";
}

public sealed class MongoDataProtectionXmlRepository : IXmlRepository
{
    private readonly IMongoCollection<DataProtectionKeyDocument> _collection;
    private readonly MongoDataProtectionOptions _options;

    public MongoDataProtectionXmlRepository(IMongoDatabase database, MongoDataProtectionOptions options)
    {
        _options = options;
        _collection = database.GetCollection<DataProtectionKeyDocument>(_options.KeyCollectionName);
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        return _collection
            .Find(x => x.ApplicationName == _options.ApplicationName)
            .ToList()
            .Select(x => XElement.Parse(x.Xml))
            .ToList();
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        var now = DateTime.UtcNow;
        var filter = Builders<DataProtectionKeyDocument>.Filter.Eq(x => x.Id, BuildId(friendlyName));
        var update = Builders<DataProtectionKeyDocument>.Update
            .SetOnInsert(x => x.Id, BuildId(friendlyName))
            .SetOnInsert(x => x.CreatedAt, now)
            .Set(x => x.ApplicationName, _options.ApplicationName)
            .Set(x => x.FriendlyName, friendlyName)
            .Set(x => x.Xml, element.ToString(SaveOptions.DisableFormatting))
            .Set(x => x.UpdatedAt, now);

        _collection.UpdateOne(filter, update, new UpdateOptions { IsUpsert = true });
    }

    private string BuildId(string friendlyName) => $"{_options.ApplicationName}:{friendlyName}";

    private sealed class DataProtectionKeyDocument
    {
        [BsonId]
        public string Id { get; set; } = string.Empty;

        public string ApplicationName { get; set; } = string.Empty;
        public string FriendlyName { get; set; } = string.Empty;
        public string Xml { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
