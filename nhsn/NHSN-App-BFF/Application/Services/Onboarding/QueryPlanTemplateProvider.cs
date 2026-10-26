using LantanaGroup.Link.Nhsn.App.Bff.Application.Interfaces.Services;
using LantanaGroup.Link.Nhsn.App.Bff.Domain.Enums;
using LantanaGroup.Link.Shared.Application.Models.Integration.DataAcquisition;

namespace LantanaGroup.Link.Nhsn.App.Bff.Application.Services.Onboarding;

public sealed class QueryPlanTemplateProvider : IQueryPlanTemplateProvider
{
    private const string StaticAssetsDirectory = "StaticAssets";

    private static readonly Dictionary<EhrVendor, string> TemplateFilesByVendor = new()
    {
        [EhrVendor.Epic] = "query-plans-templates/epic-query-plan-ach-monthly.json",
        [EhrVendor.Cerner] = "query-plans-templates/cerner-query-plan-ach-monthly.json"
    };

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<QueryPlanTemplateProvider> _logger;

    public QueryPlanTemplateProvider(IWebHostEnvironment environment, ILogger<QueryPlanTemplateProvider> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<CreateQueryPlanRequestApiModel?> GetTemplateAsync(EhrVendor vendor, CancellationToken cancellationToken = default)
    {
        var relativePath = TemplateFilesByVendor[vendor];
        var root = Path.GetFullPath(Path.Combine(_environment.ContentRootPath, StaticAssetsDirectory));
        var filePath = Path.GetFullPath(Path.Combine(root, relativePath));

        if (!File.Exists(filePath))
        {
            _logger.LogError(
                "QueryPlan auto-seed template is missing for vendor {Vendor}. Path={FilePath}",
                vendor,
                filePath);
            return null;
        }

        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return Newtonsoft.Json.JsonConvert.DeserializeObject<CreateQueryPlanRequestApiModel>(json);
    }
}
