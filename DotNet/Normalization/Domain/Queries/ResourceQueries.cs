﻿using LantanaGroup.Link.Normalization.Application.Models.Operations.Business;
using LantanaGroup.Link.Normalization.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace LantanaGroup.Link.Normalization.Domain.Queries
{
    public interface IResourceQueries
    {
        Task<List<ResourceModel>> GetAll(CancellationToken cancellationToken = default);
        Task<ResourceModel?> Get(Guid resourceId);
        Task<ResourceModel?> Get(string resourceName, CancellationToken cancellationToken = default);
        Task<List<ResourceModel>> Search(ResourceSearchModel model, CancellationToken cancellationToken = default);
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

        public async Task<ResourceModel?> Get(string resourceName, CancellationToken cancellationToken = default)
        {
            return (await Search(new ResourceSearchModel()
            {
                Name = resourceName
            }, cancellationToken)).FirstOrDefault();
        }

        public async Task<List<ResourceModel>> GetAll(CancellationToken cancellationToken = default)
        {
            return await Search(new ResourceSearchModel(), cancellationToken);
        }

        public async Task<List<ResourceModel>> Search(ResourceSearchModel model, CancellationToken cancellationToken = default)
        {
            var query = from r in _context.ResourceTypes
                        select new ResourceModel()
                        {
                            ResourceTypeId = r.Id,
                            ResourceName = r.Name,
                        };


            if (model.Names.Any())
            {
                query = query.Where(q => model.Names.Contains(q.ResourceName));
            }
            else if (!string.IsNullOrWhiteSpace(model.Name))
            {
                query = query.Where(q => q.ResourceName == model.Name);
            }

            if (model.ResourceId != null)
            {
                query = query.Where(q => q.ResourceTypeId == model.ResourceId);
            }

            return await query.OrderBy(q => q.ResourceName).ToListAsync(cancellationToken);
        }
    }
}