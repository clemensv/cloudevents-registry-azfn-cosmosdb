using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Azure.Storage.Blobs;
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
using System.Threading.Tasks;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;
using Container = Microsoft.Azure.Cosmos.Container;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;
using Azure.Storage.Blobs.Models;
using System.Threading;
using System.ComponentModel;

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
                    req, (g)=>g.Schemas, ctrGroups, ctrSchemas, manifest.SchemaGroups);
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
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }


            foreach (var group in groups.Values)
            {

                try
                {
                    var existingItem = await ctrGroups.ReadItemAsync<TGroup>(group.Id, new PartitionKey(group.Id));
                    if (existingItem.StatusCode == HttpStatusCode.OK)
                    {
                        if (group.Version <= existingItem.Resource.Version)
                        {
                            // define code & response
                            return req.CreateResponse(HttpStatusCode.Conflict);
                        }

                        var resourceDict = itemResolver(group);
                        if (resourceDict != null)
                        {
                            foreach (var resource in resourceDict.Values)
                            {
                                try
                                {
                                    var existingResource1 = await ctrResource.ReadItemAsync<Schema>(resource.Id, new PartitionKey(group.Id));

                                    await ctrGroups.UpsertItemAsync<TResource>(resource);
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
                                    resource.GroupId = group.Id;
                                    await ctrResource.CreateItemAsync<TResource>(resource, new PartitionKey(group.Id));
                                }
                                catch (CosmosException)
                                {
                                    return req.CreateResponse(HttpStatusCode.BadRequest);
                                }
                            }
                        }
                        var result = await ctrGroups.UpsertItemAsync<TGroup>(group);
                        responseGroups.Add(result.Resource.Id, result.Resource);

                        if (this.eventGridClient != null)
                        {
                            var changedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), ChangedEventType, group);
                            await this.eventGridClient.SendEventAsync(changedEvent);
                        }
                    }
                    else
                    {
                        var result = await ctrGroups.CreateItemAsync<TGroup>(group);
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

        private async Task<HttpResponseData> PutGroupHandler<TGroup, TResource, TResourceDict>(HttpRequestData req, string id, Func<TGroup, TResourceDict> itemResolver, Container ctrGroups, Container ctrResource, TGroup resource)
            where TGroup : Resource, new()
            where TResource : Resource, new()
            where TResourceDict : IDictionary<string, TResource>, new()
        {
            try
            {
                var existingResource = await ctrGroups.ReadItemAsync<TGroup>(resource.Id, new PartitionKey(id));
                if (resource.Version <= existingResource.Resource.Version)
                {
                    // define code & response
                    return req.CreateResponse(HttpStatusCode.Conflict);
                }
                var result1 = await ctrGroups.UpsertItemAsync<TGroup>(resource);

                var itemCollection = itemResolver(resource);
                if (itemCollection != null)
                {
                    foreach (var item in itemCollection.Values)
                    {
                        try
                        {
                            var existingResource1 = await ctrResource.ReadItemAsync<TResource>(item.Id, new PartitionKey(id));
                            item.GroupId = id;
                            await ctrResource.UpsertItemAsync<TResource>(item, new PartitionKey(id));
                            continue;
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
                            item.GroupId = id;
                            var result = await ctrResource.CreateItemAsync<TResource>(item, new PartitionKey(id));
                        }
                        catch (CosmosException)
                        {
                            return req.CreateResponse(HttpStatusCode.BadRequest);
                        }
                    }
                }

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
                        catch (CosmosException)
                        {
                            return req.CreateResponse(HttpStatusCode.BadRequest);
                        }
                    }
                }

                if (eventGridClient != null)
                {
                    var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resource);
                    await this.eventGridClient.SendEventAsync(createdEvent);
                }


                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(result.Resource);
                return response;
            }
            catch (CosmosException)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
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
                        item.GroupId = groupid;
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
                return req.CreateResponse(HttpStatusCode.BadRequest);
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
                catch (CosmosException)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);

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
            Container container) where TResource : Resource, new()
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            TResource resource = JsonConvert.DeserializeObject<TResource>(requestBody);
            try
            {
                var existingItem = await container.ReadItemAsync<TResource>(resource.Id, new PartitionKey(groupid));
                if (resource.Version <= existingItem.Resource.Version)
                {
                    // define code & response
                    return req.CreateResponse(HttpStatusCode.Conflict);
                }
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
            catch (CosmosException)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

        }

        private static void SetResourceHeaders<TResource, TResourceVersion>(TResource schema, TResourceVersion latestVersion, HttpResponseData res)
            where TResourceVersion : ResourceWithVersion
            where TResource : ResourceWithVersionedResources, new()
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
            res.Headers.Add(ResourceVersionHeader, latestVersion.Version);
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
            Container container,
            BlobContainerClient blobContainerClient) where TResourceVersion : ResourceWithVersion
                                 where TResource : ResourceWithVersionedResources, new()
        {
            var queryDictionary = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(req.Url.Query);
            if (queryDictionary.ContainsKey("meta") || blobContainerClient == null)
            {
                if (queryDictionary["meta"].First().ToLower() != "false")
                {
                    var existingItem = await container.ReadItemAsync<TResource>(id, new PartitionKey(groupid));
                    var latestVersion = existingItem.Resource.Versions[versionid];
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    await res.WriteAsJsonAsync(latestVersion);
                    return res;
                }
            }
            else
            {
                var existingItem = await container.ReadItemAsync<TResource>(id, new PartitionKey(groupid));
                var latestVersion = existingItem.Resource.Versions[versionid];
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
            Func<TResourceVersion, object> objectResolver = null) where TResourceVersion : ResourceWithVersion
                                 where TResource : ResourceWithVersionedResources, new()
        {
            try
            {
                var existingItem = await container.ReadItemAsync<TResource>(id, new PartitionKey(groupid));
                var latest = existingItem.Resource.Versions.Max((x) => x.Key);
                var latestVersion = (TResourceVersion)existingItem.Resource.Versions[latest];

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
                    Container container,
                    BlobContainerClient blobContainerClient,
                    string self) where TResourceVersion : ResourceWithVersion, new()
                                         where TResource : ResourceWithVersionedResources, new()
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
                    if (existingContainer.Resource.Versions == null )
                    {
                        existingContainer.Resource.Versions = new Dictionary<string, ResourceWithVersion>();
                    }
                    else
                    {
                        nextVersion = existingContainer.Resource.Versions.Values.Max(v => int.Parse(v.Version)) + 1;
                    }                    
                    resourceVersion.Version = nextVersion.ToString();
                    existingContainer.Resource.Versions.Add(resourceVersion.Version, resourceVersion);

                    var path = $"{self}/versions/{resourceVersion.Version}";
                    var blobClient = blobContainerClient.GetBlobClient(path);
                    await blobClient.UploadAsync(req.Body, new BlobUploadOptions()
                    {
                        HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
                    }, CancellationToken.None);

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
                    resource.Id = id;
                    resource.Self = new Uri(self, UriKind.Relative);
                    resource.Versions = new Dictionary<string, ResourceWithVersion>();
                    resource.Versions.Add("1", resourceVersion);
                    resourceVersion.Version = "1";

                    var path = $"{self}/versions/{resourceVersion.Version}";
                    var blobClient = blobContainerClient.GetBlobClient(path);
                    await blobClient.UploadAsync(req.Body, new BlobUploadOptions()
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = contentType
                        }
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
                catch (CosmosException)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }
                catch (Exception)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }
            }
            else
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                TResourceVersion resource = JsonConvert.DeserializeObject<TResourceVersion>(requestBody);
                try
                {
                    var existingContainer = await container.ReadItemAsync<TResource>(resource.Id, new PartitionKey(groupid));
                    var nextVersion = existingContainer.Resource.Versions.Values.Max(v => int.Parse(v.Version)) + 1;
                    resource.Version = nextVersion.ToString();
                    existingContainer.Resource.Versions.Add(resource.Version, resource);
                    var result1 = await container.UpsertItemAsync<TResource>(existingContainer.Resource, new PartitionKey(groupid));
                    if (eventGridClient != null)
                    {
                        var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resource);
                        await this.eventGridClient.SendEventAsync(createdEvent);
                    }

                    var response = req.CreateResponse(HttpStatusCode.Created);
                    response.Headers.Add(LocationHeader, new Uri(req.Url, "versions/" + resource.Version).ToString());
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
                    TResource newContainer = new TResource();
                    newContainer.Id = id;
                    newContainer.Versions.Add("1", resource);
                    newContainer.GroupId = groupid;
                    var result = await container.CreateItemAsync<TResource>(newContainer, new PartitionKey(groupid));

                    if (eventGridClient != null)
                    {
                        var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resource);
                        await this.eventGridClient.SendEventAsync(createdEvent);
                    }

                    var response = req.CreateResponse(HttpStatusCode.Created);
                    response.Headers.Add(LocationHeader, new Uri(req.Url, "versions/" + resource.Version).ToString());
                    await response.WriteAsJsonAsync(result.Resource);
                    return response;
                }
                catch (CosmosException)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
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
