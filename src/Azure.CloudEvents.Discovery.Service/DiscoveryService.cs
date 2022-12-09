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
using System.Threading;
using System.Threading.Tasks;
using Container = Microsoft.Azure.Cosmos.Container;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace Azure.CloudEvents.Discovery
{

    public partial class DiscoveryService
    {
        private const string DeletedEventType = "io.cloudevents.resource.deleted";
        private const string ChangedEventType = "io.cloudevents.resource.changed";
        private const string CreatedEventType = "io.cloudevents.resource.created";
        private const string ContentTypeHeader = "Content-Type";
        private const string ResourceIdHeader = "resource-id";
        private const string ResourceDescriptionHeader = "resource-description";
        private const string ResourceDocsHeader = "resource-docs";
        private const string ResourceNameHeader = "resource-name";
        private const string ResourceOriginHeader = "resource-origin";
        private const string ResourceVersionHeader = "resource-version";
        private const string ResourceCreatedOnHeader = "resource-createdon";
        private const string ResourceCreatedByHeader = "resource-createdby";
        private const string ResourceModifiedOnHeader = "resource-modifiedon";
        private const string ResourceModifiedByHeader = "resource-modifiedby";
        private const string ApplicationJsonMediaType = "application/json";
        private const string LocationHeader = "Location";
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
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "registry/")]
            HttpRequestData req,
            ILogger log)
        {
            if (req.Method.Equals("post", StringComparison.InvariantCultureIgnoreCase))
            {
                // the dispatcher doesn't seem to work for POST at the root
                return await UploadDoc(req, log);
            }

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

        [Function("uploadDoc")]
        public async Task<HttpResponseData> UploadDoc(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "registry/")]
            HttpRequestData req,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Manifest manifest = JsonConvert.DeserializeObject<Manifest>(requestBody);
            if (manifest.SchemaGroups != null)
            {
                var ctrGroups = this.cosmosClient.GetContainer("discovery", "schemagroups");
                var ctrSchemas = this.cosmosClient.GetContainer("discovery", "schemas");
                var res = await PutGroupsHandler<SchemaGroup, SchemaGroups, Schema, Schemas>(
                    req, (g) => g.Schemas, ctrGroups, ctrSchemas, manifest.SchemaGroups);
                if (res.StatusCode != HttpStatusCode.OK)
                    return res;
            }
            if (manifest.Groups != null)
            {
                Container ctrGroups = this.cosmosClient.GetContainer("discovery", "groups");
                Container ctrDefs = this.cosmosClient.GetContainer("discovery", "definitions");
                var res = await PutGroupsHandler<Group, Groups, Definition, Definitions>(
                    req, (g) => g.Definitions, ctrGroups, ctrDefs, manifest.Groups);
                if (res.StatusCode != HttpStatusCode.OK)
                    return res;
            }
            if (manifest.Endpoints != null)
            {
                Container ctrEndpoints = this.cosmosClient.GetContainer("discovery", "endpoints");
                Container ctrdefs = this.cosmosClient.GetContainer("discovery", "epdefinitions");
                var res = await PutGroupsHandler<Endpoint, Endpoints, Definition, Definitions>(req, (e) => e.Definitions, ctrEndpoints, ctrdefs, manifest.Endpoints);
                if (res.StatusCode != HttpStatusCode.OK)
                    return res;
            }
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(manifest);
            return response;
        }

        private void StripCosmosProperties(Resource manifest)
        {
            var keys = manifest.AdditionalProperties.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i].StartsWith("_"))
                {
                    manifest.AdditionalProperties.Remove(keys[i]);
                }
            }
        }

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

        public async Task<HttpResponseData> PutGroups<TGroup, TGroupDict, TResource, TResourceDict>(
            HttpRequestData req,
            ILogger log,
            Func<TGroup, TResourceDict> itemResolver,
            Container ctrGroups,
            Container ctrResource) where TGroup : Resource, new()
                                   where TGroupDict : IDictionary<string, TGroup>, new()
                                   where TResource : Resource, new()
                                   where TResourceDict : IDictionary<string, TResource>, new()
        {

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            TGroupDict groups = JsonConvert.DeserializeObject<TGroupDict>(requestBody);
            return await PutGroupsHandler<TGroup, TGroupDict, TResource, TResourceDict>(req, itemResolver, ctrGroups, ctrResource, groups);
        }

        private async Task<HttpResponseData> PutGroupsHandler<TGroup, TGroupDict, TResource, TResourceDict>(HttpRequestData req, Func<TGroup, TResourceDict> itemResolver, Container ctrGroups, Container ctrResource, TGroupDict groups)
            where TGroup : Resource, new()
            where TGroupDict : IDictionary<string, TGroup>, new()
            where TResource : Resource, new()
            where TResourceDict : IDictionary<string, TResource>, new()
        {
            foreach (var group in groups.Values)
            {
                return await PutGroupHandler<TGroup, TResource, TResourceDict>(req, group.Id, itemResolver, ctrGroups, ctrResource, group);
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            return res;
        }

        public async Task<HttpResponseData> PostGroups<TGroup, TGroupDict, TResource, TResourceDict>(
            HttpRequestData req,
            ILogger log,
            Func<TGroup, TResourceDict> itemResolver,
            Container ctrGroups,
            Container ctrResource) where TGroup : Resource, new()
                                   where TGroupDict : IDictionary<string, TGroup>, new()
                                   where TResource : Resource, new()
                                   where TResourceDict : IDictionary<string, TResource>, new()
        {

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            TGroupDict groups = JsonConvert.DeserializeObject<TGroupDict>(requestBody);
            return await PostGroupsHandler<TGroup, TGroupDict, TResource, TResourceDict>(req, itemResolver, ctrGroups, ctrResource, groups);
        }

        private async Task<HttpResponseData> PostGroupsHandler<TGroup, TGroupDict, TResource, TResourceDict>(
            HttpRequestData req,
            Func<TGroup, TResourceDict> itemResolver,
            Container ctrGroups,
            Container ctrResource,
            TGroupDict groups)
            where TGroup : Resource, new()
            where TGroupDict : IDictionary<string, TGroup>, new()
            where TResource : Resource, new()
            where TResourceDict : IDictionary<string, TResource>, new()
        {
            TGroupDict responseGroups = new TGroupDict();
            if (groups == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.WriteString("No groups provided");
                return errorResponse;
            }

            foreach (var group in groups.Values)
            {
                var result = await PutGroupHandler<TGroup, TResource, TResourceDict>(req, group.Id, itemResolver, ctrGroups, ctrResource, group);
                if (result.StatusCode != HttpStatusCode.OK)
                {
                    return result;
                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(responseGroups);
            return res;
        }

        public async Task<HttpResponseData> DeleteGroups<TDict, TGroup, TResource, TResourceDict>(
           HttpRequestData req,
           ILogger log,
           Func<TGroup, TResourceDict> itemResolver,
           Container ctrGroups,
           Container ctrResource) where TGroup : Resource, new()
                                  where TResource : Resource, new()
                                  where TResourceDict : IDictionary<string, TResource>, new()
                                  where TDict : System.Collections.ObjectModel.Collection<Reference>, new()
        {

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            List<Reference> references = JsonConvert.DeserializeObject<List<Reference>>(requestBody);
            TDict responseGroups = new TDict();
            if (references == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.WriteString("No references provided");
                return errorResponse;
            }

            foreach (var reference in references)
            {
                var result = await DeleteGroup<TGroup, TResource, TResourceDict>(req, reference.Id, log, itemResolver, ctrGroups, ctrResource);
                if (result.StatusCode != HttpStatusCode.OK)
                {
                    return result;
                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(responseGroups);
            return res;

        }

        public async Task<HttpResponseData> GetGroup<TGroup, TResource, TResourceDict>(
            HttpRequestData req,
            string id,
            ILogger log,
            Func<TGroup, TResourceDict> itemResolver,
            Container ctrGroups,
            Container ctrResource) where TGroup : Resource, new()
                                   where TResource : Resource, new()
                                   where TResourceDict : IDictionary<string, TResource>, new()
        {
            try
            {

                var existingItem = await ctrGroups.ReadItemAsync<TGroup>(id, new PartitionKey(id));
                existingItem.Resource.Self = new Uri(req.Url, existingItem.Resource.Id);
                var itemCollection = itemResolver(existingItem.Resource);
                if (itemCollection != null)
                {
                    itemCollection.Clear();
                }
                else
                {
                    itemCollection = new TResourceDict();
                }
                using (FeedIterator<TResource> resultSet = ctrResource.GetItemQueryIterator<TResource>(default(string), null, new QueryRequestOptions { PartitionKey = new PartitionKey(id) }))
                {
                    while (resultSet.HasMoreResults)
                    {
                        foreach (var group in await resultSet.ReadNextAsync())
                        {
                            group.Self = new Uri(req.Url, group.Id);
                            itemCollection.Add(group.Id, group);
                        }
                    }
                }
                var res = req.CreateResponse(HttpStatusCode.OK);
                StripCosmosProperties(existingItem.Resource);
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


        public async Task<HttpResponseData> PutGroup<TGroup, TResource, TResourceDict>(
            HttpRequestData req,
            string id,
            ILogger log,
            Func<TGroup, TResourceDict> itemResolver,
            Container ctrGroups,
            Container ctrResource) where TGroup : Resource, new()
                                   where TResource : Resource, new()
                                   where TResourceDict : IDictionary<string, TResource>, new()
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            TGroup resource = JsonConvert.DeserializeObject<TGroup>(requestBody);
            return await PutGroupHandler<TGroup, TResource, TResourceDict>(req, id, itemResolver, ctrGroups, ctrResource, resource);
        }

        private async Task<HttpResponseData> PutGroupHandler<TGroup, TResource, TResourceDict>(
            HttpRequestData req, string id, Func<TGroup, TResourceDict> itemResolver, Container ctrGroups, Container ctrResource, TGroup resource)
            where TGroup : Resource, new()
            where TResource : Resource, new()
            where TResourceDict : IDictionary<string, TResource>, new()
        {
            try
            {
                var existingResource = await ctrGroups.ReadItemAsync<TGroup>(resource.Id, new PartitionKey(id));
                if (resource.Version < existingResource.Resource.Version)
                {
                    // define code & response
                    var errorResponse = req.CreateResponse(HttpStatusCode.Conflict);
                    errorResponse.WriteString($"Resource version conflict on {resource.Id}, resource version {resource.Version} is lower than existing version {existingResource.Resource.Version}");
                    return errorResponse;
                }

                var itemCollection = itemResolver(resource);
                if (itemCollection != null)
                {
                    foreach (var item in itemCollection.Values)
                    {
                        var result = await PutResourceHandler(req, id, ctrResource, item);
                        if (result.StatusCode != HttpStatusCode.OK)
                        {
                            return result;
                        }
                    }
                }

                if (resource.Version == existingResource.Resource.Version)
                {
                    var skipResponse = req.CreateResponse(HttpStatusCode.OK);
                    StripCosmosProperties(existingResource.Resource);
                    await skipResponse.WriteAsJsonAsync(existingResource.Resource);
                    return skipResponse;
                }
                resource.ModifiedOn = DateTime.UtcNow;
                var result1 = await ctrGroups.UpsertItemAsync<TGroup>(resource, new PartitionKey(id));

                if (eventGridClient != null)
                {
                    var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, resource);
                    await this.eventGridClient.SendEventAsync(createdEvent);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                StripCosmosProperties(result1.Resource);
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
                var result = await ctrGroups.CreateItemAsync<TGroup>(resource, new PartitionKey(id));
                var itemCollection = itemResolver(resource);
                if (itemCollection != null)
                {
                    foreach (var item in itemCollection.Values)
                    {
                        try
                        {
                            item.GroupId = id;
                            await ctrResource.CreateItemAsync<TResource>(item, new PartitionKey(id));
                        }
                        catch (CosmosException ex)
                        {
                            var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                            errorResponse.WriteString(ex.Message);
                            return errorResponse;
                        }
                    }
                }

                if (eventGridClient != null)
                {
                    var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resource);
                    await this.eventGridClient.SendEventAsync(createdEvent);
                }


                var response = req.CreateResponse(HttpStatusCode.OK);
                StripCosmosProperties(result.Resource);
                await response.WriteAsJsonAsync(result.Resource);
                return response;
            }
            catch (CosmosException ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.WriteString(ex.Message);
                return errorResponse;
            }
        }

        public async Task<HttpResponseData> DeleteGroup<TGroup, TResource, TResourceDict>(
            HttpRequestData req,
            string id,
            ILogger log,
            Func<TGroup, TResourceDict> itemResolver,
            Container ctrGroups,
            Container ctrResource) where TGroup : Resource, new()
                                   where TResource : Resource, new()
                                   where TResourceDict : IDictionary<string, TResource>, new()
        {
            try
            {
                PartitionKey partitionKey = new PartitionKey(id);
                var existingResource = await ctrGroups.ReadItemAsync<TGroup>(id, partitionKey);
                var result = await ctrGroups.DeleteItemAsync<TGroup>(id, new PartitionKey(id));
                using (FeedIterator<TResource> resultSet = ctrResource.GetItemQueryIterator<TResource>(default(string), null, new QueryRequestOptions { PartitionKey = new PartitionKey(id) }))
                {
                    while (resultSet.HasMoreResults)
                    {
                        foreach (var group in await resultSet.ReadNextAsync())
                        {
                            await ctrResource.DeleteItemAsync<TResource>(group.Id, partitionKey);
                        }
                    }
                }

                if (this.eventGridClient != null)
                {
                    var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, existingResource.Resource);
                    await this.eventGridClient.SendEventAsync(deletedEvent);
                }

                var res = req.CreateResponse(HttpStatusCode.OK);
                StripCosmosProperties(existingResource.Resource);
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
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.WriteString("No items provided");
                return errorResponse;
            }


            foreach (var item in items.Values)
            {
                var result = await PutResourceHandler(req, groupid, container, item);
                if (result.StatusCode != HttpStatusCode.OK)
                {
                    return result;
                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(responseItems);
            return res;

        }

        public async Task<HttpResponseData> DeleteResources<TRef, TResource, TResourceDict>(
            HttpRequestData req,
            string groupid,
            ILogger log,
            Container container) where TResource : Resource, new()
                                where TResourceDict : IDictionary<string, TResource>, new()
        {

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            List<Reference> references = JsonConvert.DeserializeObject<List<Reference>>(requestBody);
            TResourceDict responseItems = new TResourceDict();
            if (references == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.WriteString("No references provided");
                return errorResponse;
            }

            foreach (var reference in references)
            {

                try
                {
                    var existingItem = await container.ReadItemAsync<TResource>(reference.Id, new PartitionKey(groupid));
                    if (existingItem.StatusCode == HttpStatusCode.OK)
                    {
                        var result = await container.DeleteItemAsync<TResource>(reference.Id, new PartitionKey(groupid));
                        responseItems.Add(existingItem.Resource.Id, existingItem.Resource);

                        if (this.eventGridClient != null)
                        {
                            var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, existingItem);
                            await this.eventGridClient.SendEventAsync(deletedEvent);
                        }
                    }

                }
                catch (CosmosException ex)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    errorResponse.WriteString(ex.Message);
                    return errorResponse;

                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(responseItems);
            return res;

        }

        public async Task<HttpResponseData> GetResource<TResource>(
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log,
            Container container) where TResource : Resource, new()
        {
            try
            {

                var existingItem = await container.ReadItemAsync<TResource>(id, new PartitionKey(groupid));
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


        public async Task<HttpResponseData> PutResource<TResource>(
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log,
            Container container,
            string self) where TResource : Resource, new()
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            TResource resource = JsonConvert.DeserializeObject<TResource>(requestBody);
            resource.Self = new Uri(self, UriKind.Relative);
            return await PutResourceHandler(req, groupid, container, resource);
        }

        private async Task<HttpResponseData> PutResourceHandler<TResource>(HttpRequestData req, string groupid, Container container, TResource resource) where TResource : Resource, new()
        {
            try
            {
                var existingResource = await container.ReadItemAsync<TResource>(resource.Id, new PartitionKey(groupid));
                if (resource.Version < existingResource.Resource.Version)
                {
                    // define code & response
                    var errorResponse = req.CreateResponse(HttpStatusCode.Conflict);
                    errorResponse.WriteString($"Resource version conflict on {resource.Id}, resource version {resource.Version} is lower than existing version {existingResource.Resource.Version}");
                    return errorResponse;
                }
                if (resource.Version == existingResource.Resource.Version)
                {
                    var skipResponse = req.CreateResponse(HttpStatusCode.OK);
                    await skipResponse.WriteAsJsonAsync(existingResource.Resource);
                    return skipResponse;
                }
                resource.GroupId = groupid;
                resource.ModifiedOn = DateTime.UtcNow;
                var result1 = await container.UpsertItemAsync<TResource>(resource, new PartitionKey(groupid));

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
                resource.GroupId = groupid;
                var result = await container.CreateItemAsync<TResource>(resource, new PartitionKey(groupid));

                if (eventGridClient != null)
                {
                    var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resource);
                    await this.eventGridClient.SendEventAsync(createdEvent);
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(result.Resource);
                return response;
            }
            catch (CosmosException ex)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.WriteString(ex.Message);
                return errorResponse;
            }
        }

        private static void SetResourceHeaders<TResource, TResourceVersion>(TResource schema, TResourceVersion latestVersion, HttpResponseData res)
            where TResourceVersion : Resource
            where TResource : Resource, new()
        {
            res.Headers.Add(ResourceIdHeader, latestVersion.Id);
            if (!string.IsNullOrEmpty(latestVersion.Description))
            {
                res.Headers.Add(ResourceDescriptionHeader, latestVersion.Description);
            }
            else if (!string.IsNullOrEmpty(schema.Description))
            {
                res.Headers.Add(ResourceDescriptionHeader, schema.Description);
            }
            if (latestVersion.Docs != null)
            {
                res.Headers.Add(ResourceDocsHeader, latestVersion.Docs.ToString());
            }
            else if (schema.Docs != null)
            {
                res.Headers.Add(ResourceDocsHeader, schema.Docs.ToString());
            }
            if (!string.IsNullOrEmpty(latestVersion.Name))
            {
                res.Headers.Add(ResourceNameHeader, latestVersion.Name);
            }
            else if (!string.IsNullOrEmpty(schema.Name))
            {
                res.Headers.Add(ResourceNameHeader, schema.Name);
            }
            if (!string.IsNullOrEmpty(latestVersion.Origin))
            {
                res.Headers.Add(ResourceOriginHeader, latestVersion.Origin);
            }
            else if (!string.IsNullOrEmpty(schema.Origin))
            {
                res.Headers.Add(ResourceNameHeader, schema.Origin);
            }
            res.Headers.Add(ResourceVersionHeader, latestVersion.Version.ToString());
            res.Headers.Add(ResourceCreatedOnHeader, latestVersion.CreatedOn.ToString("o"));
            if (!string.IsNullOrEmpty(latestVersion.CreatedBy))
            {
                res.Headers.Add(ResourceCreatedByHeader, latestVersion.CreatedBy);
            }
            res.Headers.Add(ResourceModifiedOnHeader, latestVersion.ModifiedOn.ToString("o"));
            if (!string.IsNullOrEmpty(latestVersion.ModifiedBy))
            {
                res.Headers.Add(ResourceModifiedByHeader, latestVersion.ModifiedBy);
            }
        }

        public async Task<HttpResponseData> GetResourceVersion<TResourceVersion, TResource>(
            HttpRequestData req,
            string groupid,
            string id,
            string versionid,
            ILogger log,
            Func<TResource, IDictionary<string, TResourceVersion>> versionsResolver,
            Container container,
            BlobContainerClient blobContainerClient) where TResourceVersion : Resource
                                 where TResource : Resource, new()
        {
            var queryDictionary = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(req.Url.Query);
            if (queryDictionary.ContainsKey("meta") || blobContainerClient == null)
            {
                if (queryDictionary["meta"].First().ToLower() != "false")
                {
                    var existingItem = await container.ReadItemAsync<TResource>(id, new PartitionKey(groupid));
                    var latestVersion = versionsResolver(existingItem.Resource)[versionid];
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    await res.WriteAsJsonAsync(latestVersion);
                    return res;
                }
            }
            else
            {
                var existingItem = await container.ReadItemAsync<TResource>(id, new PartitionKey(groupid));
                var latestVersion = versionsResolver(existingItem.Resource)[versionid];
                var blobClient = blobContainerClient.GetBlobClient(req.Url.AbsolutePath);
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
                    SetResourceHeaders(existingItem.Resource, latestVersion, res);
                    res.Headers.Add(ContentTypeHeader, contentType);
                    res.Body = blobClient.DownloadStreaming().Value.Content;
                    return res;

                }
            }
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }

        public async Task<HttpResponseData> GetLatestResourceVersion<TResourceVersion, TResource>(
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log,
            Container container,
            BlobContainerClient blobContainerClient,
            string self,
            Func<TResourceVersion, Uri> redirectResolver = null,
            Func<TResourceVersion, object> objectResolver = null,
            Func<TResource, IDictionary<string, TResourceVersion>> versionsResolver = null) where TResourceVersion : Resource
                                 where TResource : Resource, new()
        {
            try
            {
                var existingItem = await container.ReadItemAsync<TResource>(id, new PartitionKey(groupid));
                var latest = versionsResolver(existingItem.Resource).Max((x) => x.Key);
                var latestVersion = (TResourceVersion)versionsResolver(existingItem.Resource)[latest];

                if (req.Url.Query.Contains("meta"))
                {
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    res.Headers.Add(ContentTypeHeader, ApplicationJsonMediaType);
                    await res.WriteAsJsonAsync(latestVersion);
                    return res;
                }
                else if (redirectResolver != null && redirectResolver(latestVersion) != null)
                {
                    var res = req.CreateResponse(HttpStatusCode.TemporaryRedirect);
                    res.Headers.Add(LocationHeader, redirectResolver(latestVersion).ToString());
                    SetResourceHeaders(existingItem.Resource, latestVersion, res);
                    return res;
                }
                else if (objectResolver != null && objectResolver(latestVersion) != null)
                {
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    res.Headers.Add(ContentTypeHeader, ApplicationJsonMediaType);
                    await res.WriteAsJsonAsync(objectResolver(latestVersion));
                    SetResourceHeaders(existingItem.Resource, latestVersion, res);
                    return res;
                }
                else
                {
                    var path = $"{self}/versions/{latest}";
                    var blobClient = blobContainerClient.GetBlobClient(path);
                    var download = await blobClient.DownloadStreamingAsync();
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    res.Body = download.Value.Content;
                    res.Headers.Add(ContentTypeHeader, download.Value.Details.ContentType);
                    SetResourceHeaders(existingItem.Resource, latestVersion, res);
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

        public async Task<HttpResponseData> PostResourceVersion<TResourceVersion, TResource>(
                    HttpRequestData req,
                    string groupid,
                    string id,
                    ILogger log,
                    Func<TResource, IDictionary<string, TResourceVersion>> versionsResolver,
                    Container container,
                    BlobContainerClient blobContainerClient,
                    string self) where TResourceVersion : Resource, new()
                                 where TResource : Resource, new()
        {
            var contentType = req.Headers.GetValues(ContentTypeHeader).First();
            if (contentType != ApplicationJsonMediaType || blobContainerClient == null)
            {
                TResourceVersion resourceVersion = new()
                {
                    Id = id,
                    Description = req.Headers.Contains(ResourceDescriptionHeader) ? req.Headers.GetValues(ResourceDescriptionHeader).FirstOrDefault() : null,
                    Name = req.Headers.Contains(ResourceNameHeader) ? req.Headers.GetValues(ResourceNameHeader).FirstOrDefault() : null,
                    Docs = req.Headers.Contains(ResourceDocsHeader) ? new Uri(req.Headers.GetValues(ResourceDocsHeader).FirstOrDefault()) : null,
                    CreatedOn = DateTime.UtcNow,
                    ModifiedOn = DateTime.UtcNow,
                };

                try
                {
                    var existingContainer = await container.ReadItemAsync<TResource>(id, new PartitionKey(self));
                    long nextVersion = 1;
                    var dictionary = versionsResolver(existingContainer.Resource);
                    nextVersion = dictionary.Keys.Max(v => int.Parse(v)) + 1;
                    resourceVersion.Version = nextVersion;
                    dictionary.Add(resourceVersion.Version.ToString(), resourceVersion);

                    var path = $"{self}/versions/{resourceVersion.Version}";
                    var blobClient = blobContainerClient.GetBlobClient(path);
                    await blobClient.UploadAsync(req.Body, new BlobUploadOptions()
                    {
                        HttpHeaders = new BlobHttpHeaders { 
                            ContentType = contentType 
                        },
                        Tags = resourceVersion.Tags?.ToDictionary((x) => x.Name, (x) => x.Value)
                    }, CancellationToken.None);

                    existingContainer.Resource.ModifiedOn = DateTime.UtcNow;
                    var result1 = await container.UpsertItemAsync<TResource>(existingContainer.Resource, new PartitionKey(self));
                    if (eventGridClient != null)
                    {
                        var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resourceVersion);
                        await this.eventGridClient.SendEventAsync(createdEvent);
                    }

                    var response = req.CreateResponse(HttpStatusCode.Created);
                    response.Headers.Add(LocationHeader, new Uri(req.Url, "versions/" + resourceVersion.Version).ToString());
                    await response.WriteAsJsonAsync(result1.Resource);
                    response.StatusCode = HttpStatusCode.Created;
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
                    TResource resource = new TResource();
                    resource.ModifiedOn = resource.CreatedOn = DateTime.UtcNow;
                    resource.Description = req.Headers.Contains(ResourceDescriptionHeader) ? req.Headers.GetValues(ResourceDescriptionHeader).FirstOrDefault() : null;
                    resource.Name = req.Headers.Contains(ResourceNameHeader) ? req.Headers.GetValues(ResourceNameHeader).FirstOrDefault() : null;
                    resource.Docs = req.Headers.Contains(ResourceDocsHeader) ? new Uri(req.Headers.GetValues(ResourceDocsHeader).FirstOrDefault()) : null;
                    resource.Id = id;
                    resource.Self = new Uri(self, UriKind.Relative);
                    resourceVersion.Version = 0;
                    versionsResolver(resource).Add(resource.Version.ToString(), resourceVersion);                    

                    var path = $"{self}/versions/{resourceVersion.Version}";
                    var blobClient = blobContainerClient.GetBlobClient(path);
                    await blobClient.UploadAsync(req.Body, new BlobUploadOptions()
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = contentType
                        },
                        Tags = resourceVersion.Tags?.ToDictionary((x) => x.Name, (x) => x.Value)
                    }, CancellationToken.None);

                    resource.GroupId = groupid;
                    var result = await container.CreateItemAsync<TResource>(resource, new PartitionKey(self));

                    if (eventGridClient != null)
                    {
                        var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resourceVersion);
                        await this.eventGridClient.SendEventAsync(createdEvent);
                    }

                    var response = req.CreateResponse(HttpStatusCode.Created);
                    response.Headers.Add(LocationHeader, new Uri(req.Url, "/" + path).ToString());
                    await response.WriteAsJsonAsync(result.Resource);
                    response.StatusCode = HttpStatusCode.Created;
                    return response;
                }
                catch (CosmosException ex)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    errorResponse.WriteString(ex.Message);
                    return errorResponse;

                }
                catch (Exception)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }
            }
            else
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                TResourceVersion resourceVersion = JsonConvert.DeserializeObject<TResourceVersion>(requestBody);
                try
                {
                    var existingContainer = await container.ReadItemAsync<TResource>(resourceVersion.Id, new PartitionKey(groupid));
                    var versions = versionsResolver(existingContainer.Resource);
                    resourceVersion.Version = versions.Keys.Max(v => int.Parse(v)) + 1;
                    versions.Add(resourceVersion.Version.ToString(), resourceVersion);
                    existingContainer.Resource.ModifiedOn = DateTime.UtcNow;
                    
                    var result1 = await container.UpsertItemAsync<TResource>(existingContainer.Resource, new PartitionKey(groupid));
                    if (eventGridClient != null)
                    {
                        var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resourceVersion);
                        await this.eventGridClient.SendEventAsync(createdEvent);
                    }

                    var response = req.CreateResponse(HttpStatusCode.Created);
                    response.Headers.Add(LocationHeader, new Uri(req.Url, "versions/" + resourceVersion.Version).ToString());
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
                    TResource resource = new TResource();
                    resource.Id = id;
                    resource.Description = req.Headers.Contains(ResourceDescriptionHeader) ? req.Headers.GetValues(ResourceDescriptionHeader).FirstOrDefault() : null;
                    resource.Name = req.Headers.Contains(ResourceNameHeader) ? req.Headers.GetValues(ResourceNameHeader).FirstOrDefault() : null;
                    resource.Docs = req.Headers.Contains(ResourceDocsHeader) ? new Uri(req.Headers.GetValues(ResourceDocsHeader).FirstOrDefault()) : null;
                    resourceVersion.Version = 0;
                    versionsResolver(resource).Add(resourceVersion.Version.ToString(), resourceVersion);
                    resource.GroupId = groupid;
                    resource.CreatedOn = resource.ModifiedOn = DateTime.UtcNow;
                    
                    var result = await container.CreateItemAsync<TResource>(resource, new PartitionKey(groupid));

                    if (eventGridClient != null)
                    {
                        var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resourceVersion);
                        await this.eventGridClient.SendEventAsync(createdEvent);
                    }

                    var response = req.CreateResponse(HttpStatusCode.Created);
                    response.Headers.Add(LocationHeader, new Uri(req.Url, "versions/" + resourceVersion.Version).ToString());
                    await response.WriteAsJsonAsync(result.Resource);
                    return response;
                }
                catch (CosmosException ex)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    errorResponse.WriteString(ex.Message);
                    return errorResponse;

                }
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
