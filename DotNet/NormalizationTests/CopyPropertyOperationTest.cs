using LantanaGroup.Link.Normalization.Application.Operations;

namespace NormalizationOperationTests
{
    public class CopyPropertyOperationTests
    {

        [Fact]
        public void Location_Identifier_To_Type()
        {
            var parser = new FhirJsonParser();
            string location_text = File.ReadAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Resources\Location.txt"));
            var location = parser.Parse<Location>(location_text);

            if (location == null)
            {
                Assert.Fail("No location resource found");
            }

            CopyPropertyOperation copyOperation = new CopyPropertyOperation("Copy Location Identifier to Type", "identifier.value", "type[0].coding.code");

            copyOperation.Execute(location);

            Assert.Equal(location.Identifier[0].Value, location.Type[0].Coding[0].Code);
        }
    }
}