using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Queries
{
    public interface IResourceQueries
    {
        Task<List<ResourceModel>> GetAll();
        Task<ResourceModel?> Get(Guid resourceId);
        Task<ResourceModel?> Get(string resourceName);
        Task<List<ResourceModel>> Search(ResourceSearchModel model);
    }

    public class ResourceQueries : IResourceQueries
    {
        private readonly NormalizationDbContext _context;
        public ResourceQueries(NormalizationDbContext context) 
        {
            _context = context;
        }

        public async Task<ResourceModel?> Get(Guid resourceId)
        {
            return (await Search(new ResourceSearchModel()
            {
                ResourceId = resourceId
            })).SingleOrDefault();
        }

        public async Task<ResourceModel?> Get(string resourceName)
        {
            return (await Search(new ResourceSearchModel()
            {
                Name = resourceName
            })).SingleOrDefault();
        }

        public async Task<List<ResourceModel>> GetAll()
        {
            return await _context.ResourceTypes.Select(r =>new ResourceModel()
            {
                ResourceTypeId = r.Id,
                ResourceName = r.Name
            }).ToListAsync();
        }

        public async Task<List<ResourceModel>> Search(ResourceSearchModel model)
        {
            var query = from r in _context.ResourceTypes
                        select new ResourceModel()
                        {
                            ResourceTypeId = r.Id,
                            ResourceName = r.Name,                            
                        };


            if(model.Names.Any())
            {
                query = query.Where(q => model.Names.Contains(q.ResourceName));
            }
            else if(!string.IsNullOrWhiteSpace(model.Name))
            {
                query = query.Where(q => q.ResourceName == model.Name);
            }

            if(model.ResourceId != null)
            {
                query = query.Where(q => q.ResourceTypeId == model.ResourceId);
            }

            return await query.ToListAsync();
        }
    }
}
