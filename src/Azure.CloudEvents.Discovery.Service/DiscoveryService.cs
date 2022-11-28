using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;
using Container = Microsoft.Azure.Cosmos.Container;
using System.Threading;
using Microsoft.Azure.Cosmos.Serialization.HybridRow;
using System.ComponentModel;

namespace Azure.CloudEvents.Discovery
{

    public class DiscoveryService
    {
        private const string DeletedEventType = "io.cloudevents.discovery.deleted";
        private const string ChangedEventType = "io.cloudevents.discovery.changed";
        private const string CreatedEventType = "io.cloudevents.discovery.created";

        private CosmosClient cosmosClient;
        private readonly EventGridPublisherClient eventGridClient;
        private BlobContainerClient schemasBlobClient;

        public DiscoveryService(CosmosClient cosmosClient, EventGridPublisherClient eventGridClient, BlobServiceClient blobClient)
        {
            this.cosmosClient = cosmosClient;
            this.eventGridClient = eventGridClient;
            this.schemasBlobClient = blobClient.GetBlobContainerClient("schemas");
        }


        [Function("getManifest")]
        public async Task<HttpResponseData> GetManifest(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "/")]
            HttpRequestData req,
            ILogger log)
        {
            Manifest manifest = new Manifest
            {
                EndpointsUrl = new Uri(req.Url, "/endpoints"),
                GroupsUrl = new Uri(req.Url, "/groups"),
                SchemaGroupsUrl = new Uri(req.Url, "/schemagroups")
            };
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(manifest);
            return response;
        }


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


        [Function("getGroups")]
        public async Task<HttpResponseData> GetDefinitionGroups(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "groups")]
            HttpRequestData req,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "groups");
            return await GetGroups<Group, Groups>(req, log, container);
        }

        [Function("postGroups")]
        public async Task<HttpResponseData> PostDefinitionGroups(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "groups")]
            HttpRequestData req,
            ILogger log)
        {
            return await PostGroups<Group, Groups>(req, log, this.cosmosClient.GetContainer("discovery", "groups"));
        }

        [Function("deleteGroups")]
        public async Task<HttpResponseData> DeleteDefinitionGroups(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "groups")]
            HttpRequestData req,
            ILogger log)
        {
            return await DeleteGroups<Reference, Group, Groups>(req, log, this.cosmosClient.GetContainer("discovery", "groups"));
        }

        [Function("getGroup")]
        public async Task<HttpResponseData> GetDefinitionGroup(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "groups/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            return await GetGroup<Group>(req, id, log, this.cosmosClient.GetContainer("discovery", "groups"));
        }

        [Function("putGroup")]
        public async Task<HttpResponseData> PutDefinitionGroup(
           [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "groups/{id}")]
            HttpRequestData req,
           string id,
           ILogger log)
        {
            return await PutGroup<Group>(req, id, log, this.cosmosClient.GetContainer("discovery", "groups"));
        }

        [Function("deleteGroup")]
        public async Task<HttpResponseData> DeleteDefinitionGroup(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "groups/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            return await DeleteGroup<Group>(req, id, log, this.cosmosClient.GetContainer("discovery", "groups"));
        }


        [Function("getDefinitions")]
        public async Task<HttpResponseData> GetGroupDefinitions(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "groups/{groupid}/definitions")]
            HttpRequestData req,
            string groupid,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "groups");
            try
            {
                var existingItem = await container.ReadItemAsync<Group>(groupid, new PartitionKey(groupid));
                existingItem.Resource.Self = new Uri(req.Url, existingItem.Resource.Id);
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

        [Function("postDefinitions")]
        public async Task<HttpResponseData> PostGroupDefinitions(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "groups/{groupid}/definitions")]
            HttpRequestData req,
            string groupid,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "groups");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Definitions groups = JsonConvert.DeserializeObject<Definitions>(requestBody);
            if (groups == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            try
            {
                var existingItem = await container.ReadItemAsync<Group>(groupid, new PartitionKey(groupid));
                existingItem.Resource.Self = new Uri(req.Url, existingItem.Resource.Id);
                foreach (var definition in groups)
                {
                    if (existingItem.Resource.Definitions.ContainsKey(definition.Key))
                    {
                        existingItem.Resource.Definitions[definition.Key] = definition.Value;
                    }
                    else
                    {
                        existingItem.Resource.Definitions.Add(definition.Key, definition.Value);
                    }
                }
                await container.UpsertItemAsync<Group>(existingItem, new PartitionKey(groupid));
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

        [Function("deleteDefinitions")]
        public async Task<HttpResponseData> DeleteGroupDefinitions(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "groups/{groupid}/definitions")]
            HttpRequestData req,
            string groupid,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "groups");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Definitions groups = JsonConvert.DeserializeObject<Definitions>(requestBody);
            if (groups == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            try
            {
                var existingItem = await container.ReadItemAsync<Group>(groupid, new PartitionKey(groupid));
                existingItem.Resource.Self = new Uri(req.Url, existingItem.Resource.Id);
                existingItem.Resource.Definitions.Clear();
                await container.UpsertItemAsync<Group>(existingItem, new PartitionKey(groupid));
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

        [Function("getDefinition")]
        public async Task<HttpResponseData> GetDefinition(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "groups/{groupid}/definitions/{id}")]
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "groups");
            try
            {
                var existingItem = await container.ReadItemAsync<Group>(groupid, new PartitionKey(groupid));
                existingItem.Resource.Self = new Uri(req.Url, existingItem.Resource.Id);
                if (existingItem.Resource.Definitions.ContainsKey(id))
                {
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    await res.WriteAsJsonAsync(existingItem.Resource.Definitions[id]);
                    return res;
                }
                else
                {
                    var res = req.CreateResponse(HttpStatusCode.NotFound);
                    return res;
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

        [Function("putDefinition")]
        public async Task<HttpResponseData> PutDefinition(
           [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "groups/{groupid}/definitions/{id}")]
            HttpRequestData req,
           string groupid,
           string id,
           ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "groups");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Definition definition = JsonConvert.DeserializeObject<Definition>(requestBody);
            if (definition == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            try
            {
                var existingItem = await container.ReadItemAsync<Group>(groupid, new PartitionKey(groupid));
                existingItem.Resource.Self = new Uri(req.Url, existingItem.Resource.Id);
                if (existingItem.Resource.Definitions.ContainsKey(id))
                {
                    existingItem.Resource.Definitions[id] = definition;
                }
                else
                {
                    existingItem.Resource.Definitions.Add(id, definition);
                }

                await container.UpsertItemAsync<Group>(existingItem, new PartitionKey(groupid));
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

        [Function("deleteDefinition")]
        public async Task<HttpResponseData> DeleteDefinition(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "groups/{groupid}/definitions/{id}")]
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "groups");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Definition definition = JsonConvert.DeserializeObject<Definition>(requestBody);
            if (definition == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            try
            {
                var existingItem = await container.ReadItemAsync<Group>(groupid, new PartitionKey(groupid));
                existingItem.Resource.Self = new Uri(req.Url, existingItem.Resource.Id);
                if (existingItem.Resource.Definitions.ContainsKey(id))
                {
                    existingItem.Resource.Definitions.Remove(id);
                    await container.UpsertItemAsync<Group>(existingItem, new PartitionKey(groupid));
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    return res;
                }
                else
                {
                    existingItem.Resource.Definitions.Add(id, definition);
                    var res = req.CreateResponse(HttpStatusCode.NotFound);
                    return res;
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

        [Function("getSchemaGroups")]
        public async Task<HttpResponseData> GetSchemaGroups(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "schemagroups")]
            HttpRequestData req,
            ILogger log)
        {
            return await GetGroups<SchemaGroup, SchemaGroups>(req, log, this.cosmosClient.GetContainer("discovery", "schemagroups"));
        }

        [Function("postSchemaGroups")]
        public async Task<HttpResponseData> PostSchemaGroups(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "schemagroups")]
            HttpRequestData req,
            ILogger log)
        {
            return await PostGroups<SchemaGroup, SchemaGroups>(req, log, this.cosmosClient.GetContainer("discovery", "schemagroups"));
        }

        [Function("deleteSchemaGroups")]
        public async Task<HttpResponseData> DeleteSchemaGroups(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "schemagroups")]
            HttpRequestData req,
            ILogger log)
        {
            return await DeleteGroups<Reference, SchemaGroup, SchemaGroups>(req, log, this.cosmosClient.GetContainer("discovery", "schemagroups"));
        }

        [Function("getSchemaGroup")]
        public async Task<HttpResponseData> GetSchemaGroup(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "schemaGroups/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            return await GetGroup<SchemaGroup>(req, id, log, this.cosmosClient.GetContainer("discovery", "schemagroups"));
        }

        [Function("putSchemaGroup")]
        public async Task<HttpResponseData> PutSchemaGroup(
           [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "schemaGroups/{id}")]
            HttpRequestData req,
           string id,
           ILogger log)
        {
            return await PutGroup<SchemaGroup>(req, id, log, this.cosmosClient.GetContainer("discovery", "schemagroups"));
        }

        [Function("deleteSchemaGroup")]
        public async Task<HttpResponseData> DeleteSchemaGroup(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "schemaGroups/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            return await DeleteGroup<SchemaGroup>(req, id, log, this.cosmosClient.GetContainer("discovery", "schemagroups"));
        }


        [Function("getSchemas")]
        public async Task<HttpResponseData> GetSchemaGroupSchemas(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "schemaGroups/{schemaGroupid}/schemas")]
            HttpRequestData req,
            string schemaGroupid,
            ILogger log)
        {
            var self = GetSelfReference(new Uri(req.Url.GetLeftPart(UriPartial.Path)));
            return await GetResources<Schema, Schemas>(req, schemaGroupid, log, this.cosmosClient.GetContainer("discovery", "schemas"));
        }

        [Function("postSchemas")]
        public async Task<HttpResponseData> PostSchemaGroupSchemas(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "schemaGroups/{schemaGroupid}/schemas")]
            HttpRequestData req,
            string schemaGroupid,
            ILogger log)
        {
            return await PostResources<Schema, Schemas>(req, schemaGroupid, log, this.cosmosClient.GetContainer("discovery", "schemas"));
        }

        [Function("deleteSchemas")]
        public async Task<HttpResponseData> DeleteSchemaGroupSchemas(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "schemaGroups/{schemaGroupid}/schemas")]
            HttpRequestData req,
            string schemaGroupid,
            ILogger log)
        {
            return await DeleteResources<Reference, Schema, Schemas>(req, schemaGroupid, log, this.cosmosClient.GetContainer("discovery", "schemas"));
        }

        [Function("getSchema")]
        public async Task<HttpResponseData> GetSchema(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "schemaGroups/{schemaGroupid}/schemas/{id}")]
            HttpRequestData req,
            string schemaGroupid,
            string id,
            ILogger log)
        {
            return await GetResource<Schema>(req, schemaGroupid, id, log, this.cosmosClient.GetContainer("discovery", "schemas"));
        }

        [Function("putSchema")]
        public async Task<HttpResponseData> PutSchema(
           [HttpTrigger(AuthorizationLevel.Function, "put", Route = "schemaGroups/{schemaGroupid}/schemas/{id}")]
            HttpRequestData req,
           string schemaGroupid,
           string id,
           ILogger log)
        {
            return await PutResource<Schema>(req, schemaGroupid, id, log, this.cosmosClient.GetContainer("discovery", "schemas"));
        }

        [Function("postSchemaVersion")]
        public async Task<HttpResponseData> PostSchemaVersion(
           [HttpTrigger(AuthorizationLevel.Function, "post", Route = "schemaGroups/{schemaGroupid}/schemas/{id}")]
            HttpRequestData req,
           string schemaGroupid,
           string id,
           ILogger log)
        {
            var self = $"schemagroups/{schemaGroupid}/schemas/{id}";
            var container = this.cosmosClient.GetContainer("discovery", "schemas");
            var contentType = req.Headers.GetValues("Content-Type").First();

            if (contentType != "application/json")
            {
                Schemaversion schemaversion = new()
                {
                    Id = id
                };

                try
                {
                    var existingContainer = await container.ReadItemAsync<Schema>(id, new PartitionKey(self));
                    var nextVersion = existingContainer.Resource.Versions.Values.Max(v => int.Parse(v.Version)) + 1;
                    schemaversion.Version = nextVersion.ToString();
                    existingContainer.Resource.Versions.Add(schemaversion.Version, schemaversion);

                    var path = $"{self}/versions/{schemaversion.Version}";
                    var blobClient = this.schemasBlobClient.GetBlobClient(path);
                    await blobClient.UploadAsync(req.Body, new BlobUploadOptions() { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, CancellationToken.None);

                    var result1 = await container.UpsertItemAsync<Schema>(existingContainer.Resource, new PartitionKey(self));
                    if (eventGridClient != null)
                    {
                        var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, schemaversion);
                        await this.eventGridClient.SendEventAsync(createdEvent);
                    }

                    var response = req.CreateResponse(HttpStatusCode.Created);
                    response.Headers.Add("Location", new Uri(req.Url, "versions/" + schemaversion.Version).ToString());
                    await response.WriteAsJsonAsync(result1.Resource);

                    return response;
                }
                catch (CosmosException ce)
                {
                    if (ce.StatusCode != HttpStatusCode.NotFound)
                    {
                        throw;
                    }
                }

                try
                {


                    Schema schema = new Schema();
                    schema.Id = id;
                    schema.Self = new Uri(self, UriKind.Relative);
                    schema.Versions = new Dictionary<string, Schemaversion>();
                    schema.Versions.Add("1", schemaversion);
                    schemaversion.Version = "1";

                    var path = $"{self}/versions/{schemaversion.Version}";
                    var blobClient = this.schemasBlobClient.GetBlobClient(path);
                    await blobClient.UploadAsync(req.Body, new BlobUploadOptions() { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, CancellationToken.None);

                    var result = await container.CreateItemAsync<Schema>(schema, new PartitionKey(self));

                    if (eventGridClient != null)
                    {
                        var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, schemaversion);
                        await this.eventGridClient.SendEventAsync(createdEvent);
                    }

                    var response = req.CreateResponse(HttpStatusCode.Created);
                    response.Headers.Add("Location", new Uri(req.Url, "/" + path).ToString());
                    await response.WriteAsJsonAsync(result.Resource);
                    return response;
                }
                catch (CosmosException ce)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }
                catch (Exception ex)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

            }
            else
            {
                return await PostResourceVersion<Schemaversion, Schema>(req, schemaGroupid, id, log, container);
            }
        }

        [Function("deleteSchema")]
        public async Task<HttpResponseData> DeleteSchema(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "schemaGroups/{schemaGroupid}/schemas/{id}")]
            HttpRequestData req,
            string schemaGroupid,
            string id,
            ILogger log)
        {
            return await DeleteResource<Schema>(req, schemaGroupid, id, log, this.cosmosClient.GetContainer("discovery", "schemas"));
        }

        [Function("getSchemaVersion")]
        public async Task<HttpResponseData> GetSchemaVersion(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "schemaGroups/{schemaGroupid}/schemas/{id}/versions/{versionid}")]
            HttpRequestData req,
            string schemaGroupid,
            string id,
            string versionid,
            ILogger log)
        {
            var queryDictionary = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(req.Url.Query);
            if (queryDictionary.ContainsKey("metadata"))
            {
                if (queryDictionary["metadata"].First().ToLower() != "false")
                {
                    return await GetResourceVersion<Schemaversion, Schema>(req, schemaGroupid, id, versionid, log, this.cosmosClient.GetContainer("discovery", "schemas"));
                }
            }
            else
            {
                var blobClient = schemasBlobClient.GetBlobClient(req.Url.AbsolutePath);
                if (!await blobClient.ExistsAsync())
                {
                    var res = req.CreateResponse(HttpStatusCode.NotFound);
                    return res;
                }
                else
                {
                    var props = await blobClient.GetPropertiesAsync();
                    var contentType = props.Value.ContentType;
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    res.Headers.Add("Content-Type", contentType);
                    res.Body = blobClient.OpenRead();
                    return res;

                }
            }
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        // --------------------------------------------------------------------------------------------------

        public async Task<HttpResponseData> GetGroups<T, TDict>(
            HttpRequestData req,
            ILogger log,
            Container container)
                where T : Resource, new()
                where TDict : IDictionary<string, T>, new()
        {
            TDict groupDict = new TDict();

            using (FeedIterator<T> resultSet = container.GetItemQueryIterator<T>())
            {
                while (resultSet.HasMoreResults)
                {
                    foreach (var group in await resultSet.ReadNextAsync())
                    {
                        group.Self = new Uri(req.Url, group.Id);
                        groupDict.Add(group.Id, group);
                    }
                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(groupDict);
            return res;
        }

        public async Task<HttpResponseData> PostGroups<T, TDict>(
            HttpRequestData req,
            ILogger log,
            Container container) where T : Resource, new() where TDict : IDictionary<string, T>, new()
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            TDict groups = JsonConvert.DeserializeObject<TDict>(requestBody);
            TDict responseGroups = new TDict();
            if (groups == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }


            foreach (var group in groups.Values)
            {

                try
                {
                    var existingItem = await container.ReadItemAsync<T>(group.Id, new PartitionKey(group.Id));
                    if (existingItem.StatusCode == HttpStatusCode.OK)
                    {
                        if (group.Version <= existingItem.Resource.Version)
                        {
                            // define code & response
                            return req.CreateResponse(HttpStatusCode.Conflict);
                        }
                        var result = await container.UpsertItemAsync<T>(group);
                        responseGroups.Add(result.Resource.Id, result.Resource);

                        if (this.eventGridClient != null)
                        {
                            var changedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), ChangedEventType, group);
                            await this.eventGridClient.SendEventAsync(changedEvent);
                        }
                    }
                    else
                    {

                        var result = await container.CreateItemAsync<T>(group);
                        responseGroups.Add(result.Resource.Id, result.Resource);

                        if (this.eventGridClient != null)
                        {
                            var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, group);
                            await this.eventGridClient.SendEventAsync(createdEvent);
                        }
                    }
                }
                catch (CosmosException)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);

                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(responseGroups);
            return res;

        }

        public async Task<HttpResponseData> DeleteGroups<TRef, T, TDict>(
            HttpRequestData req,
            ILogger log,
            Container container) where T : Resource, new()
                                where TDict : IDictionary<string, T>, new()
        {

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            List<Reference> references = JsonConvert.DeserializeObject<List<Reference>>(requestBody);
            TDict responseGroups = new TDict();
            if (references == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            foreach (var reference in references)
            {

                try
                {
                    var existingItem = await container.ReadItemAsync<T>(reference.Id, new PartitionKey(reference.Id));
                    if (existingItem.StatusCode == HttpStatusCode.OK)
                    {
                        var result = await container.DeleteItemAsync<T>(reference.Id, new PartitionKey(reference.Id));
                        responseGroups.Add(existingItem.Resource.Id, existingItem.Resource);

                        if (this.eventGridClient != null)
                        {
                            var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, existingItem);
                            await this.eventGridClient.SendEventAsync(deletedEvent);
                        }
                    }

                }
                catch (CosmosException)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);

                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(responseGroups);
            return res;

        }

        public async Task<HttpResponseData> GetGroup<T>(
            HttpRequestData req,
            string id,
            ILogger log,
            Container container) where T : Resource, new()
        {
            try
            {

                var existingItem = await container.ReadItemAsync<T>(id, new PartitionKey(id));
                existingItem.Resource.Self = new Uri(req.Url, existingItem.Resource.Id);
                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(existingItem.Resource);
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


        public async Task<HttpResponseData> PutGroup<T>(
            HttpRequestData req,
            string id,
            ILogger log,
            Container container) where T : Resource, new()
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            T resource = JsonConvert.DeserializeObject<T>(requestBody);
            try
            {
                var existingResource = await container.ReadItemAsync<T>(resource.Id, new PartitionKey(resource.Id));
                if (resource.Version <= existingResource.Resource.Version)
                {
                    // define code & response
                    return req.CreateResponse(HttpStatusCode.Conflict);
                }
                var result1 = await container.UpsertItemAsync<T>(resource);

                if (eventGridClient != null)
                {
                    var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, resource);
                    await this.eventGridClient.SendEventAsync(createdEvent);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(result1.Resource);
                return response;
            }
            catch (CosmosException ce)
            {
                if (ce.StatusCode != HttpStatusCode.NotFound)
                {
                    throw;
                }
            }

            try
            {
                var result = await container.CreateItemAsync<T>(resource, new PartitionKey(resource.Id));

                if (eventGridClient != null)
                {
                    var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resource);
                    await this.eventGridClient.SendEventAsync(createdEvent);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(result.Resource);
                return response;
            }
            catch (CosmosException ce)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

        }
        public async Task<HttpResponseData> DeleteGroup<T>(
            HttpRequestData req,
            string id,
            ILogger log,
            Container container) where T : Resource
        {

            try
            {
                var existingResource = await container.ReadItemAsync<Reference>(id, new PartitionKey(id));
                var result = await container.DeleteItemAsync<T>(id, new PartitionKey(id));

                if (this.eventGridClient != null)
                {
                    var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, existingResource.Resource);
                    await this.eventGridClient.SendEventAsync(deletedEvent);
                }

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(existingResource.Resource);
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

        public async Task<HttpResponseData> GetResources<T, TDict>(
            HttpRequestData req,
            string groupid,
            ILogger log,
            Container container) where T : Resource, new() where TDict : IDictionary<string, T>, new()
        {
            TDict groupDict = new TDict();

            using (FeedIterator<T> resultSet = container.GetItemQueryIterator<T>(default(string), null, new QueryRequestOptions { PartitionKey = new PartitionKey(groupid) }))
            {
                while (resultSet.HasMoreResults)
                {
                    foreach (var group in await resultSet.ReadNextAsync())
                    {
                        group.Self = new Uri(req.Url, group.Id);
                        groupDict.Add(group.Id, group);
                    }
                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(groupDict);
            return res;
        }

        public async Task<HttpResponseData> PostResources<T, TDict>(
            HttpRequestData req,
            string groupid,
            ILogger log,
            Container container) where T : Resource, new()
                                where TDict : IDictionary<string, T>, new()
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            TDict items = JsonConvert.DeserializeObject<TDict>(requestBody);
            TDict responseItems = new TDict();
            if (items == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }


            foreach (var item in items.Values)
            {

                try
                {
                    var existingItem = await container.ReadItemAsync<T>(item.Id, new PartitionKey(groupid));
                    if (existingItem.StatusCode == HttpStatusCode.OK)
                    {
                        if (item.Version <= existingItem.Resource.Version)
                        {
                            // define code & response
                            return req.CreateResponse(HttpStatusCode.Conflict);
                        }
                        var result = await container.UpsertItemAsync<T>(item, new PartitionKey(groupid));
                        responseItems.Add(result.Resource.Id, result.Resource);

                        if (this.eventGridClient != null)
                        {
                            var changedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), ChangedEventType, item);
                            await this.eventGridClient.SendEventAsync(changedEvent);
                        }
                    }
                    else
                    {

                        var result = await container.CreateItemAsync<T>(item, new PartitionKey(groupid));
                        responseItems.Add(result.Resource.Id, result.Resource);

                        if (this.eventGridClient != null)
                        {
                            var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, item);
                            await this.eventGridClient.SendEventAsync(createdEvent);
                        }
                    }
                }
                catch (CosmosException)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);

                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(responseItems);
            return res;

        }

        public async Task<HttpResponseData> DeleteResources<TRef, T, TDict>(
            HttpRequestData req,
            string groupid,
            ILogger log,
            Container container) where T : Resource, new()
                                where TDict : IDictionary<string, T>, new()
        {

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            List<Reference> references = JsonConvert.DeserializeObject<List<Reference>>(requestBody);
            TDict responseItems = new TDict();
            if (references == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            foreach (var reference in references)
            {

                try
                {
                    var existingItem = await container.ReadItemAsync<T>(reference.Id, new PartitionKey(groupid));
                    if (existingItem.StatusCode == HttpStatusCode.OK)
                    {
                        var result = await container.DeleteItemAsync<T>(reference.Id, new PartitionKey(groupid));
                        responseItems.Add(existingItem.Resource.Id, existingItem.Resource);

                        if (this.eventGridClient != null)
                        {
                            var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, existingItem);
                            await this.eventGridClient.SendEventAsync(deletedEvent);
                        }
                    }

                }
                catch (CosmosException)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);

                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(responseItems);
            return res;

        }

        public async Task<HttpResponseData> GetResource<T>(
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log,
            Container container) where T : Resource, new()
        {
            try
            {

                var existingItem = await container.ReadItemAsync<T>(id, new PartitionKey(groupid));
                existingItem.Resource.Self = new Uri(req.Url, existingItem.Resource.Id);
                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(existingItem.Resource);
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


        public async Task<HttpResponseData> PutResource<T>(
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log,
            Container container) where T : Resource, new()
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            T resource = JsonConvert.DeserializeObject<T>(requestBody);
            try
            {
                var existingItem = await container.ReadItemAsync<T>(resource.Id, new PartitionKey(groupid));
                if (resource.Version <= existingItem.Resource.Version)
                {
                    // define code & response
                    return req.CreateResponse(HttpStatusCode.Conflict);
                }
                var result1 = await container.UpsertItemAsync<T>(resource, new PartitionKey(groupid));

                if (eventGridClient != null)
                {
                    var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, resource);
                    await this.eventGridClient.SendEventAsync(createdEvent);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(result1.Resource);
                return response;
            }
            catch (CosmosException ce)
            {
                if (ce.StatusCode != HttpStatusCode.NotFound)
                {
                    throw;
                }
            }

            try
            {
                var result = await container.CreateItemAsync<T>(resource, new PartitionKey(groupid));

                if (eventGridClient != null)
                {
                    var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resource);
                    await this.eventGridClient.SendEventAsync(createdEvent);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(result.Resource);
                return response;
            }
            catch (CosmosException ce)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

        }


        public async Task<HttpResponseData> GetResourceVersion<T, TV>(
            HttpRequestData req,
            string groupid,
            string id,
            string versionid,
            ILogger log,
            Container container) where T : ResourceWithVersion
                                 where TV : ResourceWithVersionedResources, new()
        {
            try
            {
                var existingContainer = await container.ReadItemAsync<TV>(id, new PartitionKey(groupid));
                if (existingContainer.Resource.Versions.ContainsKey(versionid))
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(existingContainer.Resource.Versions[versionid]);
                    return response;
                }
            }
            catch (CosmosException ce)
            {
                if (ce.StatusCode != HttpStatusCode.NotFound)
                {
                    throw;
                }
            }
            return req.CreateResponse(HttpStatusCode.NotFound);
        }

        public async Task<HttpResponseData> PostResourceVersion<T, TV>(
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log,
            Container container) where T : ResourceWithVersion
                                 where TV : ResourceWithVersionedResources, new()
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            T resource = JsonConvert.DeserializeObject<T>(requestBody);
            try
            {
                var existingContainer = await container.ReadItemAsync<TV>(resource.Id, new PartitionKey(groupid));
                var nextVersion = existingContainer.Resource.Versions.Values.Max(v => int.Parse(v.Version)) + 1;
                resource.Version = nextVersion.ToString();
                existingContainer.Resource.Versions.Add(resource.Version, resource);
                var result1 = await container.UpsertItemAsync<TV>(existingContainer.Resource, new PartitionKey(groupid));
                if (eventGridClient != null)
                {
                    var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resource);
                    await this.eventGridClient.SendEventAsync(createdEvent);
                }

                var response = req.CreateResponse(HttpStatusCode.Created);
                response.Headers.Add("Location", new Uri(req.Url, "versions/" + resource.Version).ToString());
                await response.WriteAsJsonAsync(result1.Resource);

                return response;

            }
            catch (CosmosException ce)
            {
                if (ce.StatusCode != HttpStatusCode.NotFound)
                {
                    throw;
                }
            }

            try
            {
                TV newContainer = new TV();
                newContainer.Id = id;
                newContainer.Versions.Add("1", resource);
                var result = await container.CreateItemAsync<TV>(newContainer, new PartitionKey(groupid));

                if (eventGridClient != null)
                {
                    var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resource);
                    await this.eventGridClient.SendEventAsync(createdEvent);
                }

                var response = req.CreateResponse(HttpStatusCode.Created);
                response.Headers.Add("Location", new Uri(req.Url, "versions/" + resource.Version).ToString());
                await response.WriteAsJsonAsync(result.Resource);
                return response;
            }
            catch (CosmosException ce)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

        }

        public async Task<HttpResponseData> DeleteResource<T>(
        HttpRequestData req,
        string groupid,
        string id,
        ILogger log,
        Container container) where T : Resource
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Reference reference = JsonConvert.DeserializeObject<Reference>(requestBody);
            try
            {
                var existingItem = await container.ReadItemAsync<Reference>(reference.Id, new PartitionKey(groupid));
                var result = await container.DeleteItemAsync<T>(reference.Id, new PartitionKey(groupid));

                if (this.eventGridClient != null)
                {
                    var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, reference);
                    await this.eventGridClient.SendEventAsync(deletedEvent);
                }

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(existingItem.Resource);
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




        Endpoint GetSelfReference(Uri baseUri)
        {
            var svc = new Endpoint()
            {
                Origin = baseUri.AbsoluteUri,
                Authscope = baseUri.AbsoluteUri,
                Usage = EndpointUsage.Subscriber,
                Config = new EndpointConfigSubscriber
                {
                    Protocol = "HTTP",
                    Endpoints = new[] { new Uri(baseUri, "subscriptions") },
                },
                Description = "Discovery Endpoint",
                Version = 0,
                Id = "self",
                Definitions = new Definitions
                {
                    { CreatedEventType,  new CloudEventDefinition()
                    {
                        Metadata = new CloudEventMetadata {
                            Attributes = new Attributes{
                                    Type = new MetadataPropertyString {
                                Value = CreatedEventType,
                                Required = true
                            } }
                        },
                        Description = "Discovery Endpoint Entry Created",
                    } },
                    { ChangedEventType, new CloudEventDefinition()
                    {
                        Metadata = new CloudEventMetadata {
                            Attributes = new Attributes
                                {
                                    Type = new MetadataPropertyString {
                                    Value = ChangedEventType,
                                    Required = true
                                } 
                            }
                        },
                        Description = "Discovery Endpoint Entry Changed"
                    } },
                    { DeletedEventType, new CloudEventDefinition()
                    {
                        Metadata = new CloudEventMetadata {
                            Attributes = new Attributes {
                                Type = new MetadataPropertyString { Value = DeletedEventType, Required = true }
                            }
                        },
                        Description = "Discovery Endpoint Entry Deleted"
                    } }
                }
            };
            return svc;
        }
    }
}
