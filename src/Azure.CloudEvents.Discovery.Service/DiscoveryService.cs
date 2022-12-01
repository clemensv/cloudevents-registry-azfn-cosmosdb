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

namespace Azure.CloudEvents.Discovery
{

    public partial class DiscoveryService
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
            catch (CosmosException)
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
            catch (CosmosException)
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
            catch (CosmosException)
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
