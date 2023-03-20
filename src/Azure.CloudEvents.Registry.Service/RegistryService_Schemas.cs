
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xRegistry.Types.SchemaRegistry;

namespace Azure.CloudEvents.Registry
{
    public partial class RegistryService
    {
        [Function("getSchemaGroups")]
        public async Task<HttpResponseData> GetSchemaGroups(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix+SchemaGroupsName)]
        HttpRequestData req,
            ILogger log)
        {
            return await GetGroups<SchemaGroup>(req, log, this.cosmosClient.GetContainer(DatabaseId, SchemaGroupsName));
        }

        [Function("postSchemaGroups")]
        public async Task<HttpResponseData> PostSchemaGroups(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = RoutePrefix+SchemaGroupsName)]
        HttpRequestData req,
            ILogger log)
        {
            var ctrGroups = this.cosmosClient.GetContainer(DatabaseId, SchemaGroupsName);
            var ctrSchemas = this.cosmosClient.GetContainer(DatabaseId, SchemasName);

            return await PostGroups<SchemaGroup, Schema>(req, log, (g) => g.Schemas, ctrGroups, ctrSchemas);
        }

        [Function("getSchemaGroup")]
        public async Task<HttpResponseData> GetSchemaGroup(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix+SchemaGroupsName+"/{id}")]
        HttpRequestData req,
            string id,
            ILogger log)
        {
            var ctrGroups = this.cosmosClient.GetContainer(DatabaseId, SchemaGroupsName);
            var ctrSchemas = this.cosmosClient.GetContainer(DatabaseId, SchemasName);
            return await GetGroup<SchemaGroup, Schema>(req, id, log, (g) => g.Schemas, ctrGroups, ctrSchemas);
        }

        [Function("putSchemaGroup")]
        public async Task<HttpResponseData> PutSchemaGroup(
           [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = RoutePrefix+SchemaGroupsName+"/{id}")]
        HttpRequestData req,
           string id,
           ILogger log)
        {
            var ctrGroups = this.cosmosClient.GetContainer(DatabaseId, SchemaGroupsName);
            var ctrSchemas = this.cosmosClient.GetContainer(DatabaseId, SchemasName);
            return await PutGroup<SchemaGroup, Schema>(req, id, log, (g) => g.Schemas, ctrGroups, ctrSchemas);
        }

        [Function("deleteSchemaGroup")]
        public async Task<HttpResponseData> DeleteSchemaGroup(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = RoutePrefix+SchemaGroupsName+"/{id}")]
        HttpRequestData req,
            string id,
            ILogger log)
        {
            var ctrGroups = this.cosmosClient.GetContainer(DatabaseId, SchemaGroupsName);
            var ctrSchemas = this.cosmosClient.GetContainer(DatabaseId, SchemasName);
            return await DeleteGroup<SchemaGroup, Schema>(req, id, log, (g) => g.Schemas, ctrGroups, ctrSchemas);
        }


        [Function("getSchemas")]
        public async Task<HttpResponseData> GetSchemaGroupSchemas(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix+SchemaGroupsName+"/{schemaGroupid}/"+SchemasName)]
        HttpRequestData req,
            string schemaGroupid,
            ILogger log)
        {
            var self = GetSelfReference(new Uri(req.Url.GetLeftPart(UriPartial.Path)));
            return await GetResources<Schema>(req, schemaGroupid, log, this.cosmosClient.GetContainer(DatabaseId, SchemasName));
        }

        [Function("postSchemas")]
        public async Task<HttpResponseData> PostSchemaGroupSchemas(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = RoutePrefix+SchemaGroupsName+"/{schemaGroupid}/"+SchemasName)]
        HttpRequestData req,
            string schemaGroupid,
            ILogger log)
        {
            return await PostResources<Schema>(req, schemaGroupid, log, this.cosmosClient.GetContainer(DatabaseId, SchemasName));
        }


        [Function("getLatestSchema")]
        public async Task<HttpResponseData> getLatestSchema(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix+SchemaGroupsName+"/{schemaGroupid}/" + SchemasName+"/{id}")]
        HttpRequestData req,
            string schemaGroupid,
            string id,
            ILogger log)
        {
            Microsoft.Azure.Cosmos.Container container = this.cosmosClient.GetContainer(DatabaseId, SchemasName);
            var self = SchemaGroupsName+$"/{schemaGroupid}/"+ SchemasName+$"/{id}";

            return await GetLatestResourceVersion<SchemaVersion, Schema>(req, schemaGroupid, id, log, container, this.schemasBlobClient, self,
                (v) => v.SchemaUrl, (q) => q.Schema, (v) => v.Versions);
        }

        

        [Function("putSchema")]
        public async Task<HttpResponseData> PutSchema(
           [HttpTrigger(AuthorizationLevel.Function, "put", Route = RoutePrefix+SchemaGroupsName+"/{schemaGroupid}/"+ SchemasName+"/{id}")]
        HttpRequestData req,
           string schemaGroupid,
           string id,
           ILogger log)
        {
            var self = SchemaGroupsName+$"/{schemaGroupid}/"+ SchemasName+$"/{id}";
            return await PutResource<Schema>(req, schemaGroupid, id, log, this.cosmosClient.GetContainer(DatabaseId, SchemasName), self);
        }

        [Function("postSchemaVersion")]
        public async Task<HttpResponseData> PostSchemaVersion(
           [HttpTrigger(AuthorizationLevel.Function, "post", Route = RoutePrefix+SchemaGroupsName+"/{schemaGroupid}/"+ SchemasName+"/{id}")]
        HttpRequestData req,
           string schemaGroupid,
           string id,
           ILogger log)
        {
            var self = SchemaGroupsName+$"/{schemaGroupid}/"+ SchemasName+$"/{id}";
            var container = this.cosmosClient.GetContainer(DatabaseId, SchemasName);
            return await PostResourceVersion<SchemaVersion, Schema>(req, schemaGroupid, id, log, (s) => { s.Versions ??= new Dictionary<string, SchemaVersion>(); return s.Versions; }, container, this.schemasBlobClient, self);
        }

        [Function("deleteSchema")]
        public async Task<HttpResponseData> DeleteSchema(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = RoutePrefix+SchemaGroupsName+"/{schemaGroupid}/"+ SchemasName+"/{id}")]
        HttpRequestData req,
            string schemaGroupid,
            string id,
            ILogger log)
        {
            return await DeleteResource<Schema>(req, schemaGroupid, id, log, this.cosmosClient.GetContainer(DatabaseId, SchemasName));
        }

        [Function("getSchemaVersion")]
        public async Task<HttpResponseData> GetSchemaVersion(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix+SchemaGroupsName+"/{schemaGroupid}/"+ SchemasName+"/{id}/versions/{versionid}")]
        HttpRequestData req,
            string schemaGroupid,
            string id,
            string versionid,
            ILogger log)
        {

            var container = this.cosmosClient.GetContainer(DatabaseId, SchemasName);
            return await GetResourceVersion<SchemaVersion, Schema>(req, schemaGroupid, id, versionid, log, (s) => { s.Versions ??= new Dictionary<string, SchemaVersion>(); return s.Versions; }, container, this.schemasBlobClient);
        }
    }
}
