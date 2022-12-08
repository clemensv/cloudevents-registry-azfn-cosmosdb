using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Azure.CloudEvents.Discovery
{
    public partial class DiscoveryService
    {
        [Function("getGroups")]
        public async Task<HttpResponseData> GetDefinitionGroups(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "registry/groups")]
            HttpRequestData req,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "groups");
            return await GetGroups<Group, Groups>(req, log, container);
        }

        [Function("postGroups")]
        public async Task<HttpResponseData> PostDefinitionGroups(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "registry/groups")]
            HttpRequestData req,
            ILogger log)
        {
            Container ctrGroups = this.cosmosClient.GetContainer("discovery", "groups");
            Container ctrDefs = this.cosmosClient.GetContainer("discovery", "definitions");
            return await PostGroups<Group, Groups, Definition, Definitions>(req, log, (g) => g.Definitions, ctrGroups, ctrDefs);
        }

        [Function("deleteGroups")]
        public async Task<HttpResponseData> DeleteDefinitionGroups(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "registry/groups")]
            HttpRequestData req,
            ILogger log)
        {
            Container ctrGroups = this.cosmosClient.GetContainer("discovery", "groups");
            Container ctrDefs = this.cosmosClient.GetContainer("discovery", "definitions");
            return await DeleteGroups<Reference, Group, Groups>(req, log, ctrGroups);
        }

        [Function("getGroup")]
        public async Task<HttpResponseData> GetDefinitionGroup(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "registry/groups/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            Container ctrGroups = this.cosmosClient.GetContainer("discovery", "groups");
            Container ctrDefs = this.cosmosClient.GetContainer("discovery", "definitions");
            return await GetGroup<Group, Definition, Definitions>(req, id, log, (g) => g.Definitions, ctrGroups, ctrDefs);
        }

        [Function("putGroup")]
        public async Task<HttpResponseData> PutDefinitionGroup(
           [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "registry/groups/{id}")]
            HttpRequestData req,
           string id,
           ILogger log)
        {
            Container ctrGroups = this.cosmosClient.GetContainer("discovery", "groups");
            Container ctrDefs = this.cosmosClient.GetContainer("discovery", "definitions");
            return await PutGroup<Group, Definition, Definitions>(req, id, log, (g) => g.Definitions, ctrGroups, ctrDefs);
        }

        [Function("deleteGroup")]
        public async Task<HttpResponseData> DeleteDefinitionGroup(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "registry/groups/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            Container ctrGroups = this.cosmosClient.GetContainer("discovery", "groups");
            Container ctrDefs = this.cosmosClient.GetContainer("discovery", "definitions");
            return await DeleteGroup<Group, Definition, Definitions>(req, id, log, (g) => g.Definitions, ctrGroups, ctrDefs);
        }


        [Function("getDefinitions")]
        public async Task<HttpResponseData> GetGroupDefinitions(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "registry/groups/{groupid}/definitions")]
            HttpRequestData req,
            string groupid,
            ILogger log)
        {
            Container ctrDefs = this.cosmosClient.GetContainer("discovery", "definitions");
            return await GetResources<Definition, Definitions>(req, groupid, log, ctrDefs);
        }

        [Function("postDefinitions")]
        public async Task<HttpResponseData> PostGroupDefinitions(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "registry/groups/{groupid}/definitions")]
            HttpRequestData req,
            string groupid,
            ILogger log)
        {
            Container ctrDefs = this.cosmosClient.GetContainer("discovery", "definitions");
            return await PostResources<Definition, Definitions>(req, groupid, log, ctrDefs);
        }

        [Function("deleteDefinitions")]
        public async Task<HttpResponseData> DeleteGroupDefinitions(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "registry/groups/{groupid}/definitions")]
            HttpRequestData req,
            string groupid,
            ILogger log)
        {
            Container ctrDefs = this.cosmosClient.GetContainer("discovery", "definitions");
            return await DeleteResources<DefinitionReferences, Definition, Definitions>(req, groupid, log, ctrDefs);
        }

        [Function("getDefinition")]
        public async Task<HttpResponseData> GetDefinition(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "registry/groups/{groupid}/definitions/{id}")]
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log)
        {
            Container ctrdefs = this.cosmosClient.GetContainer("discovery", "definitions");
            return await GetResource<Definition>(req, groupid, id, log, ctrdefs);
        }

        [Function("putDefinition")]
        public async Task<HttpResponseData> PutDefinition(
           [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "registry/groups/{groupid}/definitions/{id}")]
            HttpRequestData req,
           string groupid,
           string id,
           ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "definitions");
            return await PutResource<Definition>(req, groupid, id, log, container);
        }

        [Function("deleteDefinition")]
        public async Task<HttpResponseData> DeleteDefinition(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "registry/groups/{groupid}/definitions/{id}")]
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "definitions");
            return await DeleteResource<Definition>(req, groupid, id, log, container);

        }
    }
}
