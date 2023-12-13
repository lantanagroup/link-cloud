using Census.Domain.Entities;
using LantanaGroup.Link.Census.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;

namespace Census.Services
{
    public class CensusConfigService
    {
        private readonly ILogger<CensusConfigService> _logger;
        private readonly IBaseRepository<CensusConfigEntity> _datastore;

        public CensusConfigService(ILogger<CensusConfigService> logger, IBaseRepository<CensusConfigEntity> datastore)
        {
            _logger = logger;
            _datastore = datastore;
        }

        public virtual async Task<HttpResponseMessage> Create(string configString)
        {
            CensusConfigEntity? model = JsonConvert.DeserializeObject<CensusConfigEntity>(configString);
            if (model != null)
            {
                var response = await _datastore.AddAsync(model);
                if (response)
                {
                    var message = new HttpResponseMessage();
                    var content = new StringContent(JsonConvert.SerializeObject(model));
                    message.Content = content;
                    message.StatusCode = System.Net.HttpStatusCode.OK;
                    return message;
                }
                else
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
                }
            }
            else return null;
        }

        public virtual async Task<CensusConfigEntity> Get(string guid)
        {
            return await _datastore.GetAsync(new Guid(guid));
        }
        public virtual async Task<HttpResponseMessage> Put(string guid, String configString)
        {
            CensusConfigEntity? model = JsonConvert.DeserializeObject<CensusConfigEntity>(configString);
            if (model != null)
            {

                var response = await _datastore.UpdateAsync(new Guid(guid), model);
                if (response)
                {
                    var message = new HttpResponseMessage();
                    var content = new StringContent(JsonConvert.SerializeObject(model));
                    message.Content = content;
                    message.StatusCode = System.Net.HttpStatusCode.OK;
                    return message;
                }
                else
                {
                    return new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
                }
            }
            else return null;
        }
        public virtual async Task<HttpResponseMessage> Delete(string guid)
        {
            return await _datastore.DeleteAsync(new Guid(guid)) ?
                new HttpResponseMessage(System.Net.HttpStatusCode.OK) : new HttpResponseMessage(System.Net.HttpStatusCode.NotFound);
        }
    }
}
