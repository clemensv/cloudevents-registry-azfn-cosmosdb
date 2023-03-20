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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using xRegistry.Types.Registry;
using Container = Microsoft.Azure.Cosmos.Container;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace Azure.CloudEvents.Registry
{

    public partial class RegistryService
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
        private const string DatabaseId = "discovery";
        private const string RoutePrefix = "registry/";
        private const string EndpointsName = "endpoints";
        private const string DefinitionGroupsName = "definitiongroups";
        private const string DefinitionGroupsCollection = "groups";
        private const string SchemaGroupsName = "schemagroups";
        private const string SchemasName = "schemas";
        private const string DefinitionsName = "definitions";
        private const string EndpointDefinitionsCollection = "epdefinitions";
        private CosmosClient cosmosClient;
        private readonly EventGridPublisherClient eventGridClient;
        private BlobContainerClient schemasBlobClient;

        public RegistryService(CosmosClient cosmosClient, EventGridPublisherClient eventGridClient, BlobServiceClient blobClient)
        {
            this.cosmosClient = cosmosClient;
            this.eventGridClient = eventGridClient;
            this.schemasBlobClient = blobClient.GetBlobContainerClient(SchemasName);
        }

        [Function("getDocument")]
        public async Task<HttpResponseData> GetDocument(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = RoutePrefix)]
            HttpRequestData req,
            ILogger log)
        {
            if (req.Method.Equals("post", StringComparison.InvariantCultureIgnoreCase))
            {
                // the dispatcher doesn't seem to work for POST at the root
                return await UploadDoc(req, log);
            }

            Uri baseUri = new UriBuilder(req.Url)
            {
                Path = req.Url.AbsolutePath.TrimEnd('/') + "/",
                Query = null
            }.Uri;
            Document document = new Catalog()
            {
                EndpointsUrl = new Uri(ComposeUriReference(baseUri, EndpointsName)),
                DefinitionGroupsUrl = new Uri(ComposeUriReference(baseUri, DefinitionGroupsName)),
                SchemaGroupsUrl = new Uri(ComposeUriReference(baseUri, SchemaGroupsName))
            };
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(document);
            return response;
        }

        [Function("uploadDoc")]
        public async Task<HttpResponseData> UploadDoc(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = RoutePrefix)]
            HttpRequestData req,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Catalog document = JsonConvert.DeserializeObject<Catalog>(requestBody);
            if (document.SchemaGroups != null)
            {
                var ctrGroups = this.cosmosClient.GetContainer(DatabaseId, SchemaGroupsName);
                var ctrSchemas = this.cosmosClient.GetContainer(DatabaseId, SchemasName);
                var res = await PutGroupsHandler<xRegistry.Types.SchemaRegistry.SchemaGroup, xRegistry.Types.SchemaRegistry.Schema>(
                    req, (g) => g.Schemas, ctrGroups, ctrSchemas, document.SchemaGroups);
                if (res.StatusCode != HttpStatusCode.OK)
                    return res;
            }
            if (document.DefinitionGroups != null)
            {
                Container ctrGroups = this.cosmosClient.GetContainer(DatabaseId, "groups");
                Container ctrDefs = this.cosmosClient.GetContainer(DatabaseId, DefinitionsName);
                var res = await PutGroupsHandler<xRegistry.Types.MessageDefinitionsRegistry.DefinitionGroup, xRegistry.Types.MessageDefinitionsRegistry.Definition>(
                    req, (g) => g.Definitions, ctrGroups, ctrDefs, document.DefinitionGroups);
                if (res.StatusCode != HttpStatusCode.OK)
                    return res;
            }
            if (document.Endpoints != null)
            {
                Container ctrEndpoints = this.cosmosClient.GetContainer(DatabaseId, EndpointsName);
                Container ctrdefs = this.cosmosClient.GetContainer(DatabaseId, EndpointDefinitionsCollection);
                var res = await PutGroupsHandler<xRegistry.Types.EndpointRegistry.Endpoint, xRegistry.Types.EndpointRegistry.Definition>(req, (e) => e.Definitions, ctrEndpoints, ctrdefs, document.Endpoints);
                if (res.StatusCode != HttpStatusCode.OK)
                    return res;
            }
            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(document);
            return response;
        }

        private void StripCosmosProperties(IResource document)
        {
            var keys = document.AdditionalProperties.Keys.ToArray();
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i].StartsWith("_"))
                {
                    document.AdditionalProperties.Remove(keys[i]);
                }
            }
        }

        public async Task<HttpResponseData> GetGroups<T>(
            HttpRequestData req,
            ILogger log,
            Container container)
                where T : IResource, new()
        {
            Dictionary<string, T> groupDict = new();

            using (FeedIterator<T> resultSet = container.GetItemQueryIterator<T>())
            {
                while (resultSet.HasMoreResults)
                {
                    try
                    {
                        foreach (var group in await resultSet.ReadNextAsync())
                        {
                            group.Self = ComposeUriReference(req.Url, group.Id);
                            groupDict.Add(group.Id, group);
                        }
                    }
                    catch (CosmosException ex)
                    {
                        log.LogError(ex, "Error reading groups");
                    }
                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(groupDict);
            return res;
        }

        public async Task<HttpResponseData> PutGroups<TGroup, TResource>(
            HttpRequestData req,
            ILogger log,
            Func<TGroup, IDictionary<string, TResource>> itemResolver,
            Container ctrGroups,
            Container ctrResource) where TGroup : IResource, new()
                                   where TResource : IResource, new()                                   
        {

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Dictionary<string, TGroup> groups = JsonConvert.DeserializeObject<Dictionary<string,TGroup>>(requestBody);
            return await PutGroupsHandler<TGroup, TResource>(req, itemResolver, ctrGroups, ctrResource, groups);
        }

        private async Task<HttpResponseData> PutGroupsHandler<TGroup, TResource>(HttpRequestData req, Func<TGroup, IDictionary<string, TResource>> itemResolver, Container ctrGroups, Container ctrResource, IDictionary<string, TGroup> groups)
            where TGroup : IResource, new()
            where TResource : IResource, new()
        {
            foreach (var group in groups.Values)
            {
                return await PutGroupHandler<TGroup, TResource>(req, group.Id, itemResolver, ctrGroups, ctrResource, group);
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            return res;
        }

        public async Task<HttpResponseData> PostGroups<TGroup, TResource>(
            HttpRequestData req,
            ILogger log,
            Func<TGroup, IDictionary<string, TResource>> itemResolver,
            Container ctrGroups,
            Container ctrResource) where TGroup : IResource, new()
                                   where TResource : IResource, new()
        {

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Dictionary<string, TGroup> groups = JsonConvert.DeserializeObject<Dictionary<string, TGroup>>(requestBody);
            return await PostGroupsHandler<TGroup, TResource>(req, itemResolver, ctrGroups, ctrResource, groups);
        }

        private async Task<HttpResponseData> PostGroupsHandler<TGroup, TResource>(
            HttpRequestData req,
            Func<TGroup, IDictionary<string, TResource>> itemResolver,
            Container ctrGroups,
            Container ctrResource,
            IDictionary<string, TGroup> groups)
            where TGroup : IResource, new()
            where TResource : IResource, new()
        {
            Dictionary<string, TGroup> responseGroups = new();
            if (groups == null)
            {
                var errorResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                errorResponse.WriteString("No groups provided");
                return errorResponse;
            }

            foreach (var group in groups.Values)
            {
                var result = await PutGroupHandler<TGroup, TResource>(req, group.Id, itemResolver, ctrGroups, ctrResource, group);
                if (result.StatusCode != HttpStatusCode.OK)
                {
                    return result;
                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(responseGroups);
            return res;
        }
        

        string ComposeUriReference(Uri baseUri, string path)
        {

            return new UriBuilder(baseUri)
            {
                Path = baseUri.AbsolutePath + (baseUri.AbsolutePath.EndsWith("/") ? path : "/" + path)
            }.Uri.ToString();
        }

        public async Task<HttpResponseData> GetGroup<TGroup, TResource>(
            HttpRequestData req,
            string id,
            ILogger log,
            Func<TGroup, IDictionary<string, TResource>> itemResolver,
            Container ctrGroups,
            Container ctrResource) where TGroup : IResource, new()
                                 where TResource : IResource, new()
        {
            try
            {

                var existingItem = await ctrGroups.ReadItemAsync<TGroup>(id, new PartitionKey(id));
                existingItem.Resource.Self = ComposeUriReference(req.Url, existingItem.Resource.Id);
                var itemCollection = itemResolver(existingItem.Resource);
                if (itemCollection != null)
                {
                    itemCollection.Clear();
                }
                else
                {
                    itemCollection = new Dictionary<string, TResource>();
                }
                using (FeedIterator<TResource> resultSet = ctrResource.GetItemQueryIterator<TResource>(default(string), null, new QueryRequestOptions { PartitionKey = new PartitionKey(id) }))
                {
                    while (resultSet.HasMoreResults)
                    {
                        foreach (var group in await resultSet.ReadNextAsync())
                        {
                            group.Self = ComposeUriReference(req.Url, group.Id);
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


        public async Task<HttpResponseData> PutGroup<TGroup, TResource>(
            HttpRequestData req,
            string id,
            ILogger log,
            Func<TGroup, IDictionary<string, TResource>> itemResolver,
            Container ctrGroups,
            Container ctrResource) where TGroup : IResource, new()
                                   where TResource : IResource, new()
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            TGroup resource = JsonConvert.DeserializeObject<TGroup>(requestBody);
            return await PutGroupHandler<TGroup, TResource>(req, id, itemResolver, ctrGroups, ctrResource, resource);
        }

        private async Task<HttpResponseData> PutGroupHandler<TGroup, TResource>(
            HttpRequestData req, string id, Func<TGroup, IDictionary<string, TResource>> itemResolver, Container ctrGroups, Container ctrResource, TGroup resource)
            where TGroup : IResource, new()
            where TResource : IResource, new()
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

        public async Task<HttpResponseData> DeleteGroup<TGroup, TResource>(
            HttpRequestData req,
            string id,
            ILogger log,
            Func<TGroup, IDictionary<string, TResource>> itemResolver,
            Container ctrGroups,
            Container ctrResource) where TGroup : IResource, new()
                                   where TResource : IResource, new()
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

        public async Task<HttpResponseData> GetResources<T>(
            HttpRequestData req,
            string groupid,
            ILogger log,
            Container container) where T : IResource, new() 
        {
            Dictionary<string, T> groupDict = new Dictionary<string, T>();

            using (FeedIterator<T> resultSet = container.GetItemQueryIterator<T>(default(string), null, new QueryRequestOptions { PartitionKey = new PartitionKey(groupid) }))
            {
                while (resultSet.HasMoreResults)
                {
                    foreach (var group in await resultSet.ReadNextAsync())
                    {
                        group.Self = ComposeUriReference(req.Url, group.Id);
                        groupDict.Add(group.Id, group);
                    }
                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(groupDict);
            return res;
        }

        public async Task<HttpResponseData> PostResources<T>(
            HttpRequestData req,
            string groupid,
            ILogger log,
            Container container) where T : IResource, new()
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Dictionary<string, T> items = JsonConvert.DeserializeObject<Dictionary<string,T>>(requestBody);
            Dictionary<string, T> responseItems = new Dictionary<string, T>();
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
        public async Task<HttpResponseData> GetResource<TResource>(
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log,
            Container container) where TResource : IResource, new()
        {
            try
            {

                var existingItem = await container.ReadItemAsync<TResource>(id, new PartitionKey(groupid));
                existingItem.Resource.Self = ComposeUriReference(req.Url, existingItem.Resource.Id);
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
            string self) where TResource : IResource, new()
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            TResource resource = JsonConvert.DeserializeObject<TResource>(requestBody);
            resource.Self = new Uri( self).ToString();
            return await PutResourceHandler(req, groupid, container, resource);
        }

        private async Task<HttpResponseData> PutResourceHandler<TResource>(HttpRequestData req, string groupid, Container container, TResource resource) where TResource : IResource, new()
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
            where TResourceVersion : IResource
            where TResource : IResource, new()
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
            BlobContainerClient blobContainerClient) where TResourceVersion : IResource
                                 where TResource : IResource, new()
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
            Func<TResource, IDictionary<string, TResourceVersion>> versionsResolver = null) where TResourceVersion : IResource
                                 where TResource : IResource, new()
        {
            try
            {
                var existingItem = await container.ReadItemAsync<TResource>(id, new PartitionKey(groupid));
                if ( existingItem == null || existingItem.Resource == null)
                {
                    var res = req.CreateResponse(HttpStatusCode.NotFound);
                    return res;
                }
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
                    string self) where TResourceVersion : IResource, new()
                                 where TResource : IResource, new()
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
                    var existingContainer = await container.ReadItemAsync<TResource>(id, new PartitionKey(groupid));
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
                        }
                    }, CancellationToken.None);

                    existingContainer.Resource.ModifiedOn = DateTime.UtcNow;
                    var result1 = await container.UpsertItemAsync<TResource>(existingContainer.Resource, new PartitionKey(groupid));
                    if (eventGridClient != null)
                    {
                        var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resourceVersion);
                        await this.eventGridClient.SendEventAsync(createdEvent);
                    }

                    var response = req.CreateResponse(HttpStatusCode.Created);
                    response.Headers.Add(LocationHeader, ComposeUriReference(req.Url, "versions/" + resourceVersion.Version).ToString());
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
                    resource.Self = self;
                    resourceVersion.Version = 0;
                    versionsResolver(resource).Add(resource.Version.ToString(), resourceVersion);                    

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
                    var result = await container.CreateItemAsync<TResource>(resource, new PartitionKey(groupid));

                    if (eventGridClient != null)
                    {
                        var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, resourceVersion);
                        await this.eventGridClient.SendEventAsync(createdEvent);
                    }

                    var response = req.CreateResponse(HttpStatusCode.Created);
                    response.Headers.Add(LocationHeader, ComposeUriReference(req.Url, "/" + path).ToString());
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
                    response.Headers.Add(LocationHeader, ComposeUriReference(req.Url, "versions/" + resourceVersion.Version).ToString());
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
                    response.Headers.Add(LocationHeader, ComposeUriReference(req.Url, "versions/" + resourceVersion.Version).ToString());
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
            Container container) where T : IResource
        {
            try
            {
                var existingItem = await container.ReadItemAsync<Resource>(id, new PartitionKey(groupid));
                var result = await container.DeleteItemAsync<T>(id, new PartitionKey(groupid));

                if (this.eventGridClient != null)
                {
                    var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, id);
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




    }
}
