using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace Azure.CloudEvents.Registry
{
    public partial class RegistryService
    {
        [Function("getEndpoints")]
        public async Task<HttpResponseData> GetEndpoints(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix+EndpointsName)]
            HttpRequestData req,
            ILogger log)
        {
            var self = GetSelfReference(new Uri(req.Url.GetLeftPart(UriPartial.Path)));
            return await GetGroups<Endpoint, Endpoints>(req, log, this.cosmosClient.GetContainer(DatabaseId, EndpointsName));
        }

        [Function("postEndpoints")]
        public async Task<HttpResponseData> PostEndpoints(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = RoutePrefix+EndpointsName)]
            HttpRequestData req,
            ILogger log)
        {
            Container ctrEndpoints = this.cosmosClient.GetContainer(DatabaseId, EndpointsName);
            Container ctrdefs = this.cosmosClient.GetContainer(DatabaseId, EndpointDefinitionsCollection);
            return await PostGroups<Endpoint, Endpoints, Definition, Definitions>(req, log, (e)=>e.Definitions, ctrEndpoints, ctrdefs);
        }

        [Function("deleteEndpoints")]
        public async Task<HttpResponseData> DeleteEndpoints(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = RoutePrefix+EndpointsName)]
            HttpRequestData req,
            ILogger log)
        {
            Container ctrEndpoints = this.cosmosClient.GetContainer(DatabaseId, EndpointsName);
            Container ctrdefs = this.cosmosClient.GetContainer(DatabaseId, EndpointDefinitionsCollection);
            return await DeleteGroups<EndpointReferences, Endpoint, Definition, Definitions>(req, log, (e) => e.Definitions, ctrEndpoints, ctrdefs);
        }

        [Function("getEndpoint")]
        public async Task<HttpResponseData> GetEndpoint(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix+EndpointsName+"/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            Container ctrEndpoints = this.cosmosClient.GetContainer(DatabaseId, EndpointsName);
            Container ctrdefs = this.cosmosClient.GetContainer(DatabaseId, EndpointDefinitionsCollection);
            return await GetGroup<Endpoint, Definition, Definitions>(req, id, log, (e)=>e.Definitions, ctrEndpoints, ctrdefs);
        }

        [Function("putEndpoint")]
        public async Task<HttpResponseData> PutEndpoint(
           [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = RoutePrefix+EndpointsName+"/{id}")]
            HttpRequestData req,
           string id,
           ILogger log)
        {
            Container ctrEndpoints = this.cosmosClient.GetContainer(DatabaseId, EndpointsName);
            Container ctrdefs = this.cosmosClient.GetContainer(DatabaseId, EndpointDefinitionsCollection);
            return await PutGroup<Endpoint, Definition, Definitions>(req, id, log, (e) => e.Definitions, ctrEndpoints, ctrdefs);
        }

        [Function("deleteEndpoint")]
        public async Task<HttpResponseData> DeleteEndpoint(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = RoutePrefix+EndpointsName+"/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            Container ctrEndpoints = this.cosmosClient.GetContainer(DatabaseId, EndpointsName);
            Container ctrdefs = this.cosmosClient.GetContainer(DatabaseId, EndpointDefinitionsCollection);
            return await DeleteGroup<Endpoint, Definition, Definitions>(req, id, log, (e) => e.Definitions, ctrEndpoints, ctrdefs);
        }

        [Function("getEndpointDefinitions")]
        public async Task<HttpResponseData> GetEndpointDefinitions(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix+EndpointsName+"/{id}/"+ DefinitionsName)]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            Microsoft.Azure.Cosmos.Container container  = this.cosmosClient.GetContainer(DatabaseId, EndpointDefinitionsCollection);
            return await GetResources<Definition, Definitions>(req, id, log, container);
        }

        [Function("getEndpointDefinition")]
        public async Task<HttpResponseData> GetEndpointDefinition(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix+EndpointsName+"/{id}/"+ DefinitionsName+"/{defid}")]
            HttpRequestData req,
            string id,
            string defid,
            ILogger log)
        {
            Container ctrdefs = this.cosmosClient.GetContainer(DatabaseId, EndpointDefinitionsCollection);
            return await GetResource<Definition>(req, id, defid, log, ctrdefs);
        }
    }
}
