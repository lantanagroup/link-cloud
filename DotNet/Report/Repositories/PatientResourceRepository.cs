using LantanaGroup.Link.Report.Entities;
using LantanaGroup.Link.Shared.Application.Models.Configs;
using Microsoft.Extensions.Options;

namespace LantanaGroup.Link.Report.Repositories
{
    public class PatientResourceRepository : MongoDbRepository<PatientResourceModel>
    {
        public PatientResourceRepository(IOptions<MongoConnection> mongoSettings, ILogger<PatientResourceRepository> logger) : base(mongoSettings, logger) 
        { 
            
        }
    }
}
