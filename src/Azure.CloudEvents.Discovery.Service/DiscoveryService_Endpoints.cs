using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using System.Net;

namespace Azure.CloudEvents.Discovery
{
    public partial class DiscoveryService
    {
        [Function("getEndpoints")]
        public async Task<HttpResponseData> GetEndpoints(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "endpoints")]
            HttpRequestData req,
            ILogger log)
        {
            var self = GetSelfReference(new Uri(req.Url.GetLeftPart(UriPartial.Path)));
            return await GetGroups<Endpoint, Endpoints>(req, log, this.cosmosClient.GetContainer("discovery", "endpoints"));
        }

        [Function("postEndpoints")]
        public async Task<HttpResponseData> PostEndpoints(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "endpoints")]
            HttpRequestData req,
            ILogger log)
        {
            return await PostGroups<Endpoint, Endpoints>(req, log, this.cosmosClient.GetContainer("discovery", "endpoints"));
        }

        [Function("deleteEndpoints")]
        public async Task<HttpResponseData> DeleteEndpoints(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "endpoints")]
            HttpRequestData req,
            ILogger log)
        {
            return await DeleteGroups<Reference, Endpoint, Endpoints>(req, log, this.cosmosClient.GetContainer("discovery", "endpoints"));
        }

        [Function("getEndpoint")]
        public async Task<HttpResponseData> GetEndpoint(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "endpoints/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            return await GetGroup<Endpoint>(req, id, log, this.cosmosClient.GetContainer("discovery", "endpoints"));
        }

        [Function("putEndpoint")]
        public async Task<HttpResponseData> PutEndpoint(
           [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "endpoints/{id}")]
            HttpRequestData req,
           string id,
           ILogger log)
        {
            return await PutGroup<Endpoint>(req, id, log, this.cosmosClient.GetContainer("discovery", "endpoints"));
        }

        [Function("deleteEndpoint")]
        public async Task<HttpResponseData> DeleteEndpoint(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "endpoints/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            return await DeleteGroup<Endpoint>(req, id, log, this.cosmosClient.GetContainer("discovery", "endpoints"));
        }

        [Function("getEndpointDefinitions")]
        public async Task<HttpResponseData> GetEndpointDefinitions(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "endpoints/{id}/definitions")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            Microsoft.Azure.Cosmos.Container container  = this.cosmosClient.GetContainer("discovery", "endpoints");
            try
            {
                var existingItem = await container.ReadItemAsync<Endpoint>(id, new PartitionKey(id));
                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(existingItem.Resource.Definitions);
                return res;
            }
            catch (CosmosException ce)
            {
                if (ce.StatusCode == HttpStatusCode.NotFound)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }

        [Function("getEndpointDefinition")]
        public async Task<HttpResponseData> GetEndpointDefinition(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "endpoints/{id}/definitions/{defid}")]
            HttpRequestData req,
            string id,
            string defid,
            ILogger log)
        {
            Container container = this.cosmosClient.GetContainer("discovery", "endpoints");
            try
            {
                var existingItem = await container.ReadItemAsync<Endpoint>(id, new PartitionKey(id));
                if (existingItem.Resource.Definitions != null &&
                     existingItem.Resource.Definitions.ContainsKey(defid))
                {
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    await res.WriteAsJsonAsync(existingItem.Resource.Definitions[defid]);
                    return res;
                }
                else
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }
            }
            catch (CosmosException ce)
            {
                if (ce.StatusCode == HttpStatusCode.NotFound)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }
                return req.CreateResponse(HttpStatusCode.InternalServerError);
            }
        }
    }
}
