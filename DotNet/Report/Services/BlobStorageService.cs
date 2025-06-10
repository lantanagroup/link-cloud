using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Specialized;
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.Report.Application.Options;
using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Task = System.Threading.Tasks.Task;

namespace LantanaGroup.Link.Report.Services
{
    public class BlobStorageService
    {
        private readonly BlobStorageSettings _settings;
        private readonly BlobContainerClient? _containerClient;

        public BlobStorageService(IOptions<BlobStorageSettings> settings)
        {
            _settings = settings.Value;
            if (_settings.ConnectionString != null)
            {
                _containerClient = new BlobContainerClient(_settings.ConnectionString, _settings.BlobContainerName);
            }
        }

        public string GetReportName(string facilityId, List<string> reportTypes, DateTime reportStartDate)
        {
            return string.Join('_', [
                facilityId,
                string.Join('+', reportTypes),
                reportStartDate.ToString("yyyyMMdd")
            ]);
        }

        public string GetReportName(ReportScheduleModel reportSchedule)
        {
            return GetReportName(
                reportSchedule.FacilityId,
                reportSchedule.ReportTypes,
                reportSchedule.ReportStartDate);
        }

        private string GetBlobName(params string[] segments)
        {
            IEnumerable<string> enumerable = segments;
            if (!string.IsNullOrEmpty(_settings.BlobRoot))
            {
                enumerable = enumerable.Prepend(_settings.BlobRoot);
            }
            return string.Join('/', enumerable.Select(component => component.Trim('/')));
        }

        public Uri? GetUri(params string[] segments)
        {
            if (_containerClient == null)
            {
                return null;
            }
            BlobUriBuilder uriBuilder = new(_containerClient.Uri)
            {
                BlobName = GetBlobName(segments)
            };
            return uriBuilder.ToUri();
        }

        public async Task<Uri> UploadAsync(
            ReportScheduleModel reportSchedule,
            PatientSubmissionModel patientSubmission,
            CancellationToken cancellationToken = default)
        {
            if (_containerClient == null)
            {
                return null;
            }
            string reportName = GetReportName(reportSchedule);
            string bundleName = $"{reportName}_{patientSubmission.PatientId}.ndjson";
            string blobName = GetBlobName(reportName, bundleName);
            BlockBlobClient blobClient = _containerClient.GetBlockBlobClient(blobName);
            using Stream stream = await blobClient.OpenWriteAsync(true, cancellationToken: cancellationToken);
            JsonSerializerOptions options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
            byte lineFeed = 0x0a;

            async Task SerializeAsync(string resources)
            {
                Bundle? bundle = JsonSerializer.Deserialize<Bundle>(resources, options);
                if (bundle == null)
                {
                    return;
                }
                foreach (Bundle.EntryComponent entry in bundle.Entry)
                {
                    await JsonSerializer.SerializeAsync(stream, entry.Resource, options, cancellationToken);
                    stream.WriteByte(lineFeed);
                }
            }

            await SerializeAsync(patientSubmission.PatientResources);
            await SerializeAsync(patientSubmission.OtherResources);
            return blobClient.Uri;
        }
    }
}
