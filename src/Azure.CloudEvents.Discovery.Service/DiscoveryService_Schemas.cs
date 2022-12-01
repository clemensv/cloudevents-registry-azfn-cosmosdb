using Azure.Messaging;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Azure.CloudEvents.Discovery
{
    public partial class DiscoveryService
    {
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
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "schemagroups/{id}")]
        HttpRequestData req,
            string id,
            ILogger log)
        {
            var ctrGroups = this.cosmosClient.GetContainer("discovery", "schemagroups");
            var ctrSchemas = this.cosmosClient.GetContainer("discovery", "schemas");

            try
            {

                var existingItem = await ctrGroups.ReadItemAsync<SchemaGroup>(id, new PartitionKey(id));
                existingItem.Resource.Self = new Uri(req.Url, existingItem.Resource.Id);
                if (existingItem.Resource.Schemas != null)
                {
                    existingItem.Resource.Schemas.Clear();
                }
                else
                {
                    existingItem.Resource.Schemas = new Schemas();
                }
                using (FeedIterator<Schema> resultSet = ctrSchemas.GetItemQueryIterator<Schema>(default(string), null, new QueryRequestOptions { PartitionKey = new PartitionKey(id) }))
                {
                    while (resultSet.HasMoreResults)
                    {
                        foreach (var group in await resultSet.ReadNextAsync())
                        {
                            group.Self = new Uri(req.Url, group.Id);
                            existingItem.Resource.Schemas.Add(group.Id, group);
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

        [Function("putSchemaGroup")]
        public async Task<HttpResponseData> PutSchemaGroup(
           [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "schemagroups/{id}")]
        HttpRequestData req,
           string id,
           ILogger log)
        {
            var ctrGroups = this.cosmosClient.GetContainer("discovery", "schemagroups");
            var ctrSchemas = this.cosmosClient.GetContainer("discovery", "schemas");
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            SchemaGroup resource = JsonConvert.DeserializeObject<SchemaGroup>(requestBody);
            try
            {
                var existingResource = await ctrGroups.ReadItemAsync<SchemaGroup>(resource.Id, new PartitionKey(resource.Id));
                if (resource.Version <= existingResource.Resource.Version)
                {
                    // define code & response
                    return req.CreateResponse(HttpStatusCode.Conflict);
                }
                var result1 = await ctrGroups.UpsertItemAsync<SchemaGroup>(resource);

                if (resource.Schemas != null)
                {
                    foreach (var item in resource.Schemas.Values)
                    {
                        try
                        {
                            var existingResource1 = await ctrSchemas.ReadItemAsync<Schema>(item.Id, new PartitionKey(resource.Id));
                            await ctrGroups.UpsertItemAsync<Schema>(item);
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
                            var result = await ctrSchemas.CreateItemAsync<Schema>(item, new PartitionKey(resource.Id));
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
                var result = await ctrGroups.CreateItemAsync<SchemaGroup>(resource, new PartitionKey(resource.Id));

                if (resource.Schemas != null)
                {
                    foreach (var item in resource.Schemas.Values)
                    {
                        try
                        {
                            await ctrSchemas.CreateItemAsync<Schema>(item, new PartitionKey(resource.Id));
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

        [Function("deleteSchemaGroup")]
        public async Task<HttpResponseData> DeleteSchemaGroup(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "schemagroups/{id}")]
        HttpRequestData req,
            string id,
            ILogger log)
        {
            var ctrGroups = this.cosmosClient.GetContainer("discovery", "schemagroups");
            var ctrSchemas = this.cosmosClient.GetContainer("discovery", "schemas");
            try
            {
                PartitionKey partitionKey = new PartitionKey(id);
                var existingResource = await ctrGroups.ReadItemAsync<SchemaGroup>(id, partitionKey);
                var result = await ctrGroups.DeleteItemAsync<SchemaGroup>(id, new PartitionKey(id));
                using (FeedIterator<Schema> resultSet = ctrSchemas.GetItemQueryIterator<Schema>(default(string), null, new QueryRequestOptions { PartitionKey = new PartitionKey(id) }))
                {
                    while (resultSet.HasMoreResults)
                    {
                        foreach (var group in await resultSet.ReadNextAsync())
                        {
                            await ctrSchemas.DeleteItemAsync<Schema>(group.Id, partitionKey);
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


        [Function("getSchemas")]
        public async Task<HttpResponseData> GetSchemaGroupSchemas(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "schemagroups/{schemaGroupid}/schemas")]
        HttpRequestData req,
            string schemaGroupid,
            ILogger log)
        {
            var self = GetSelfReference(new Uri(req.Url.GetLeftPart(UriPartial.Path)));
            return await GetResources<Schema, Schemas>(req, schemaGroupid, log, this.cosmosClient.GetContainer("discovery", "schemas"));
        }

        [Function("postSchemas")]
        public async Task<HttpResponseData> PostSchemaGroupSchemas(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "schemagroups/{schemaGroupid}/schemas")]
        HttpRequestData req,
            string schemaGroupid,
            ILogger log)
        {
            return await PostResources<Schema, Schemas>(req, schemaGroupid, log, this.cosmosClient.GetContainer("discovery", "schemas"));
        }

        [Function("deleteSchemas")]
        public async Task<HttpResponseData> DeleteSchemaGroupSchemas(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "schemagroups/{schemaGroupid}/schemas")]
        HttpRequestData req,
            string schemaGroupid,
            ILogger log)
        {
            return await DeleteResources<Reference, Schema, Schemas>(req, schemaGroupid, log, this.cosmosClient.GetContainer("discovery", "schemas"));
        }

        

        [Function("getLatestSchema")]
        public async Task<HttpResponseData> getLatestSchema(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "schemagroups/{schemaGroupid}/schemas/{id}")]
        HttpRequestData req,
            string schemaGroupid,
            string id,
            ILogger log)
        {
            Microsoft.Azure.Cosmos.Container container = this.cosmosClient.GetContainer("discovery", "schemas");

            try
            {
                var existingItem = await container.ReadItemAsync<Schema>(id, new PartitionKey(schemaGroupid));
                var latest = existingItem.Resource.Versions.Max((x) => x.Key);
                var latestVersion = existingItem.Resource.Versions[latest];

                if ( req.Url.Query.Contains("meta"))
                {
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    res.Headers.Add("Content-Type", "application/json");
                    await res.WriteAsJsonAsync(latestVersion);
                    return res;
                }
                else if (latestVersion.Schemaurl != null)
                {
                    var res = req.CreateResponse(HttpStatusCode.TemporaryRedirect);
                    res.Headers.Add("Location", latestVersion.Schemaurl.ToString());
                    SetResourceHeaders(existingItem.Resource, latestVersion, res);
                    return res;
                }
                else if (latestVersion.Schemaobject != null)
                {
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    res.Headers.Add("Content-Type", "application/json");
                    await res.WriteAsJsonAsync(latestVersion.Schemaobject);
                    SetResourceHeaders(existingItem.Resource, latestVersion, res);
                    return res;
                }
                else
                {
                    var self = $"schemagroups/{schemaGroupid}/schemas/{id}";
                    var path = $"{self}/versions/{latest}";
                    var blobClient = this.schemasBlobClient.GetBlobClient(path);
                    var download = await blobClient.DownloadStreamingAsync();
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    res.Body = download.Value.Content;
                    res.Headers.Add("Content-Type", download.Value.Details.ContentType);
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

        private static void SetResourceHeaders(Schema schema, SchemaVersion latestVersion, HttpResponseData res)
        {
            res.Headers.Add("resource-id", latestVersion.Id);
            if (!string.IsNullOrEmpty(latestVersion.Description))
            {
                res.Headers.Add("resource-description", latestVersion.Description);
            }
            else if ( !string.IsNullOrEmpty( schema.Description))
            {
                res.Headers.Add("resource-description", schema.Description);
            }
            if (latestVersion.Docs != null)
            {
                res.Headers.Add("resource-docs", latestVersion.Docs.ToString());
            }
            else if ( schema.Docs != null)
            {
                res.Headers.Add("resource-docs", schema.Docs.ToString());
            }
            if (!string.IsNullOrEmpty(latestVersion.Name))
            {
                res.Headers.Add("resource-name", latestVersion.Name);
            }
            else if ( !string.IsNullOrEmpty(schema.Name))
            {
                res.Headers.Add("resource-name", schema.Name);
            }
            if (!string.IsNullOrEmpty(latestVersion.Origin))
            {
                res.Headers.Add("resource-origin", latestVersion.Origin);
            }
            else if (!string.IsNullOrEmpty(schema.Origin))
            {
                res.Headers.Add("resource-name", schema.Origin);
            }
            res.Headers.Add("resource-version", latestVersion.Version);
            res.Headers.Add("resource-createdon", latestVersion.CreatedOn.ToString("o"));
            if (!string.IsNullOrEmpty(latestVersion.CreatedBy))
            {
                res.Headers.Add("resource-createdby", latestVersion.CreatedBy);
            }
            res.Headers.Add("resource-modifiedon", latestVersion.ModifiedOn.ToString("o"));
            if (!string.IsNullOrEmpty(latestVersion.ModifiedBy))
            {
                res.Headers.Add("resource-modifiedby", latestVersion.ModifiedBy);
            }
        }

        [Function("putSchema")]
        public async Task<HttpResponseData> PutSchema(
           [HttpTrigger(AuthorizationLevel.Function, "put", Route = "schemagroups/{schemaGroupid}/schemas/{id}")]
        HttpRequestData req,
           string schemaGroupid,
           string id,
           ILogger log)
        {
            return await PutResource<Schema>(req, schemaGroupid, id, log, this.cosmosClient.GetContainer("discovery", "schemas"));
        }

        [Function("postSchemaVersion")]
        public async Task<HttpResponseData> PostSchemaVersion(
           [HttpTrigger(AuthorizationLevel.Function, "post", Route = "schemagroups/{schemaGroupid}/schemas/{id}")]
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
                SchemaVersion schemaversion = new()
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
                    schema.Versions = new Dictionary<string, SchemaVersion>();
                    schema.Versions.Add("1", schemaversion);
                    schemaversion.Version = "1";

                    var path = $"{self}/versions/{schemaversion.Version}";
                    var blobClient = this.schemasBlobClient.GetBlobClient(path);
                    await blobClient.UploadAsync(req.Body, new BlobUploadOptions()
                    {
                        HttpHeaders = new BlobHttpHeaders
                        {
                            ContentType = contentType
                        }
                    }, CancellationToken.None);

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
                return await PostResourceVersion<SchemaVersion, Schema>(req, schemaGroupid, id, log, container);
            }
        }

        [Function("deleteSchema")]
        public async Task<HttpResponseData> DeleteSchema(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "schemagroups/{schemaGroupid}/schemas/{id}")]
        HttpRequestData req,
            string schemaGroupid,
            string id,
            ILogger log)
        {
            return await DeleteResource<Schema>(req, schemaGroupid, id, log, this.cosmosClient.GetContainer("discovery", "schemas"));
        }

        [Function("getSchemaVersion")]
        public async Task<HttpResponseData> GetSchemaVersion(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "schemagroups/{schemaGroupid}/schemas/{id}/versions/{versionid}")]
        HttpRequestData req,
            string schemaGroupid,
            string id,
            string versionid,
            ILogger log)
        {

            var container = this.cosmosClient.GetContainer("discovery", "schemas");
            var queryDictionary = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(req.Url.Query);
            if (queryDictionary.ContainsKey("metadata"))
            {
                if (queryDictionary["metadata"].First().ToLower() != "false")
                {
                    var existingItem = await container.ReadItemAsync<Schema>(id, new PartitionKey(schemaGroupid));
                    var latestVersion = existingItem.Resource.Versions[versionid];
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    await res.WriteAsJsonAsync(latestVersion);
                    return res;
                }
            }
            else
            {
                var existingItem = await container.ReadItemAsync<Schema>(id, new PartitionKey(schemaGroupid));
                var latestVersion = existingItem.Resource.Versions[versionid];
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
                    SetResourceHeaders(existingItem.Resource, latestVersion, res);
                    res.Headers.Add("Content-Type", contentType);
                    res.Body = blobClient.DownloadStreaming().Value.Content;
                    return res;

                }
            }
            return req.CreateResponse(HttpStatusCode.InternalServerError);
        }
    }
}
