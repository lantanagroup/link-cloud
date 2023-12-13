
using Hl7.Fhir.Model;
using Hl7.Fhir.Serialization;
using LantanaGroup.Link.MeasureEval.Entities;
using LantanaGroup.Link.MeasureEval.Models;
using LantanaGroup.Link.MeasureEval.Repository;
using System.Text.Json;

namespace LantanaGroup.Link.MeasureEval.Services
{
    public class MeasureDefinitionService
    {
        private readonly ILogger<MeasureDefinitionService> _logger;
        private readonly MeasureEvalConfig measureEvalConfig;
        private readonly HttpClient httpClient;
        MeasureDefinitionRepo _measureDefRepo;

        public MeasureDefinitionService(MeasureDefinitionRepo measureDefRepo, ILogger<MeasureDefinitionService> logger, MeasureEvalConfig measureEvalConfig, HttpClient httpClient)
        {
            _logger = logger;
            _measureDefRepo = measureDefRepo;
            this.measureEvalConfig = measureEvalConfig ?? throw new ArgumentNullException(nameof(MeasureEvalConfig));
            this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(HttpClient));
        }

        public async Task<Hl7.Fhir.Model.Bundle> getBundleFromUrl(String url)
        {
            Uri uri = new Uri(url);

            var response = await httpClient.GetAsync(uri);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError($"Retrieval error:  {response.StatusCode}");
                return null;
            }
            string measureBundleString = await response.Content.ReadAsStringAsync();

            var options = new JsonSerializerOptions().ForFhir(ModelInfo.ModelInspector);
            Hl7.Fhir.Model.Bundle bundle = JsonSerializer.Deserialize<Bundle>(measureBundleString, options);

            return bundle;
        }


        public async System.Threading.Tasks.Task CreateMeasureDefinition(MeasureDefinition measureDefinition, CancellationToken cancellationToken)
        {
            if (measureDefinition.bundle == null)
            {
                _logger.LogError("Bundle is not provided and/or cannot be retrieved");
                throw new ApplicationException("Bundle is not provided and/or cannot be retrieved");
            }
            if (measureDefinition.bundle.Id == null)
            {
                _logger.LogError($"Bundle must specify an id.");
                throw new ApplicationException("Bundle must specify an id.");
            }

            if (measureDefinition.measureDefinitionId != measureDefinition.bundle.Id)
            {
                _logger.LogError($"The bundle Id in the bundle: {measureDefinition.bundle.Id} is different than the one provided by the user: {measureDefinition.measureDefinitionId}");
                throw new ApplicationException($"The measure Definition Id of the bundle: {measureDefinition.bundle.Id} is different than the one provided by the user: {measureDefinition.measureDefinitionId}");
            }

            if (await _measureDefRepo.ExistsAsync(measureDefinition.measureDefinitionId, cancellationToken))
            {
                _logger.LogError("The measure Definition Id/ Bundle Id already exists.");
                throw new ApplicationException("The measure Definition Id/Bundle Id already exists.");
            }

            await _measureDefRepo.CreateAsync(measureDefinition, cancellationToken);
        }

        public async System.Threading.Tasks.Task UpdateMeasureDefinition(string measureDefId, MeasureDefinition measureDefinition, CancellationToken cancellationToken)
        {
            if (measureDefinition.bundle == null)
            {
                _logger.LogError("Bundle is not provided and/or cannot be retrieved");
                throw new ApplicationException("Bundle is not provided and/or cannot be retrieved");
            }
            if (measureDefinition.bundle.Id == null)
            {
                _logger.LogError($"Bundle must specify an id");
                throw new ApplicationException("Bundle must specify an id");
            }

            if (measureDefinition.measureDefinitionId != measureDefinition.bundle.Id)
            {
                _logger.LogError($"The bundle Id in the bundle: {measureDefinition.bundle.Id} is different than the one provided by the user: {measureDefinition.measureDefinitionId}");
                throw new ApplicationException($"The bundle Id of the bundle: {measureDefinition.bundle.Id} is different than the one provided by the user: {measureDefinition.measureDefinitionId}");
            }

            if (!(await _measureDefRepo.ExistsAsync(measureDefId, cancellationToken)))
            {
                _logger.LogError("The measure Definition Id/Bundle Id does not exist.");
                throw new ApplicationException("The measure Definition Id/Bundle Id does not exist.");
            }

            await _measureDefRepo.UpdateAsync(measureDefId, measureDefinition, cancellationToken);
        }

        public async Task<MeasureDefinition> GetMeasureDefinition(string measureDefinitionId, CancellationToken cancellationToken)
        {
            return await _measureDefRepo.GetAsync(measureDefinitionId, cancellationToken);
        }

        public async System.Threading.Tasks.Task DeleteMeasureDefinition(string measureDefinitionId, CancellationToken cancellationToken)
        {
            await _measureDefRepo.DeleteAsync(measureDefinitionId, cancellationToken);
        }

    }
}
