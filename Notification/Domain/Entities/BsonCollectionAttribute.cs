namespace LantanaGroup.Link.Notification.Domain.Entities
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class BsonCollectionAttribute : Attribute
    {
        public string CollectionName { get; } = string.Empty;

        public BsonCollectionAttribute(string collectionName)
        {
            CollectionName = collectionName;
        }

    }
}
