﻿using Confluent.Kafka;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.PatientsToQuery.Application.Models;
using Streamiz.Kafka.Net.SerDes;
using System.Text;
using System.Text.Json;

namespace LantanaGroup.Link.PatientsToQuery.Serializers
{
    public class PatientIdsAcquiredSerializer<T> : ISerDes<T>
    {
        public T Deserialize(byte[] data, SerializationContext context)
        {
            throw new NotImplementedException();
        }

        public object DeserializeObject(byte[] data, SerializationContext context)
        {
            string jsonContent = Encoding.Default.GetString(data.ToArray());            
            var patientIdsAcquiredMessage = new PatientIdsAcquiredValue();

            jsonContent = jsonContent.Replace("\t", string.Empty);
            jsonContent = jsonContent.Replace("\n", string.Empty);

            byte[] byteArray = Encoding.UTF8.GetBytes(jsonContent);
            MemoryStream stream = new MemoryStream(byteArray);
            var doc = JsonDocument.Parse(stream);

            //TODO: See if there are other ways to validate without having to pull out the patientid's property
            doc.RootElement.TryGetProperty("PatientIds", out var patientids);
            var patientidsStr = patientids.ToString();

            patientIdsAcquiredMessage.PatientIds = new FhirJsonParser().Parse<List>(patientidsStr);

            return patientIdsAcquiredMessage.PatientIds;

        }

        public void Initialize(SerDesContext context)
        {
            //throw new NotImplementedException();
        }

        public byte[] Serialize(T data, SerializationContext context)
        {
            var options = new JsonSerializerOptions();
            return JsonSerializer.SerializeToUtf8Bytes(data, options);
        }

        public byte[] SerializeObject(object data, SerializationContext context)
        {
            throw new NotImplementedException();
        }
    }
}
