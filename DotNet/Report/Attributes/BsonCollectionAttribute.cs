
namespace LantanaGroup.Link.Report.Attributes
{
    [AttributeUsage(AttributeTargets.Class)]
    public class BsonCollectionAttribute : Attribute
    {

        public string Name { get; } = string.Empty;

        /// <summary>
        /// Allows a class to specify its mongo collection name
        /// </summary>
        /// <param name="name">The name of the MongoDB collection for the decorated class</param>
        public BsonCollectionAttribute(string name)
        {
            Name = name;
        }

    }
}
