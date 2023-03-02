using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Azure.CloudEvents.Registry
{
    public partial class RegistryService
    {
        private const string DefinitiongroupsCollection = "groups";

        [Function("getGroups")]
        public async Task<HttpResponseData> GetDefinitionGroups(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix+DefinitiongroupsName)]
            HttpRequestData req,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer(DatabaseId, DefinitiongroupsCollection);
            return await GetGroups<Definitiongroup, Definitiongroups>(req, log, container);
        }

        [Function("postGroups")]
        public async Task<HttpResponseData> PostDefinitionGroups(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = RoutePrefix+DefinitiongroupsName)]
            HttpRequestData req,
            ILogger log)
        {
            Container ctrGroups = this.cosmosClient.GetContainer(DatabaseId, DefinitiongroupsCollection);
            Container ctrDefs = this.cosmosClient.GetContainer(DatabaseId, DefinitionsName);
            return await PostGroups<Definitiongroup, Definitiongroups, Definition, Definitions>(req, log, (g) => g.Definitions, ctrGroups, ctrDefs);
        }

        [Function("deleteGroups")]
        public async Task<HttpResponseData> DeleteDefinitionGroups(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = RoutePrefix+DefinitiongroupsName)]
            HttpRequestData req,
            ILogger log)
        {
            Container ctrGroups = this.cosmosClient.GetContainer(DatabaseId, DefinitiongroupsCollection);
            Container ctrDefs = this.cosmosClient.GetContainer(DatabaseId, DefinitionsName);
            return await DeleteGroups<DefinitiongroupReferences, Definitiongroup, Definition, Definitions>(req, log, (g)=>g.Definitions, ctrGroups, ctrDefs);
        }

        [Function("getGroup")]
        public async Task<HttpResponseData> GetDefinitionGroup(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix+DefinitiongroupsName+"/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            Container ctrGroups = this.cosmosClient.GetContainer(DatabaseId, DefinitiongroupsCollection);
            Container ctrDefs = this.cosmosClient.GetContainer(DatabaseId, DefinitionsName);
            return await GetGroup<Definitiongroup, Definition, Definitions>(req, id, log, (g) => g.Definitions, ctrGroups, ctrDefs);
        }

        [Function("putGroup")]
        public async Task<HttpResponseData> PutDefinitionGroup(
           [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = RoutePrefix+DefinitiongroupsName+"/{id}")]
            HttpRequestData req,
           string id,
           ILogger log)
        {
            Container ctrGroups = this.cosmosClient.GetContainer(DatabaseId, DefinitiongroupsCollection);
            Container ctrDefs = this.cosmosClient.GetContainer(DatabaseId, DefinitionsName);
            return await PutGroup<Definitiongroup, Definition, Definitions>(req, id, log, (g) => g.Definitions, ctrGroups, ctrDefs);
        }

        [Function("deleteGroup")]
        public async Task<HttpResponseData> DeleteDefinitionGroup(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = RoutePrefix+DefinitiongroupsName+"/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            Container ctrGroups = this.cosmosClient.GetContainer(DatabaseId, DefinitiongroupsCollection);
            Container ctrDefs = this.cosmosClient.GetContainer(DatabaseId, DefinitionsName);
            return await DeleteGroup<Definitiongroup, Definition, Definitions>(req, id, log, (g) => g.Definitions, ctrGroups, ctrDefs);
        }


        [Function("getDefinitions")]
        public async Task<HttpResponseData> GetGroupDefinitions(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix+DefinitiongroupsName+"/{groupid}/"+ DefinitionsName)]
            HttpRequestData req,
            string groupid,
            ILogger log)
        {
            Container ctrDefs = this.cosmosClient.GetContainer(DatabaseId, DefinitionsName);
            return await GetResources<Definition, Definitions>(req, groupid, log, ctrDefs);
        }

        [Function("postDefinitions")]
        public async Task<HttpResponseData> PostGroupDefinitions(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = RoutePrefix+DefinitiongroupsName+"/{groupid}/"+ DefinitionsName)]
            HttpRequestData req,
            string groupid,
            ILogger log)
        {
            Container ctrDefs = this.cosmosClient.GetContainer(DatabaseId, DefinitionsName);
            return await PostResources<Definition, Definitions>(req, groupid, log, ctrDefs);
        }

        [Function("deleteDefinitions")]
        public async Task<HttpResponseData> DeleteGroupDefinitions(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = RoutePrefix+DefinitiongroupsName+"/{groupid}/"+ DefinitionsName)]
            HttpRequestData req,
            string groupid,
            ILogger log)
        {
            Container ctrDefs = this.cosmosClient.GetContainer(DatabaseId, DefinitionsName);
            return await DeleteResources<DefinitionReferences, Definition, Definitions>(req, groupid, log, ctrDefs);
        }

        [Function("getDefinition")]
        public async Task<HttpResponseData> GetDefinition(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix+DefinitiongroupsName+"/{groupid}/"+ DefinitionsName+"/{id}")]
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log)
        {
            Container ctrdefs = this.cosmosClient.GetContainer(DatabaseId, DefinitionsName);
            return await GetResource<Definition>(req, groupid, id, log, ctrdefs);
        }

        [Function("putDefinition")]
        public async Task<HttpResponseData> PutDefinition(
           [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = RoutePrefix+DefinitiongroupsName+"/{groupid}/"+ DefinitionsName+"/{id}")]
            HttpRequestData req,
           string groupid,
           string id,
           ILogger log)
        {
            var self = $"groups/{groupid}/"+ DefinitionsName+"/{id}";
            var container = this.cosmosClient.GetContainer(DatabaseId, DefinitionsName);
            return await PutResource<Definition>(req, groupid, id, log, container, self);
        }

        [Function("deleteDefinition")]
        public async Task<HttpResponseData> DeleteDefinition(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = RoutePrefix+DefinitiongroupsName+"/{groupid}/"+ DefinitionsName+"/{id}")]
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer(DatabaseId, DefinitionsName);
            return await DeleteResource<Definition>(req, groupid, id, log, container);

        }
    }
}
