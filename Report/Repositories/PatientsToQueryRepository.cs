using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Report.Repositories
{
    public class PatientsToQueryRepository : MongoDbRepository<PatientsToQueryModel>
    {

        private readonly ILogger<PatientsToQueryRepository> _logger;

        public PatientsToQueryRepository(IOptions<MongoConnection> mongoSettings, ILogger<PatientsToQueryRepository> logger) : base(mongoSettings, logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

    }
}