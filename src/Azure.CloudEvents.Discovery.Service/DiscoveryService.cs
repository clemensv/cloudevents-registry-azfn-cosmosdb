using Azure.CloudEvents.Discovery.Service;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using PartitionKey = Microsoft.Azure.Cosmos.PartitionKey;

namespace Azure.CloudEvents.Discovery
{

    public class DiscoveryService
    {
        private const string DeletedEventType = "io.cloudevents.discovery.deleted";
        private const string ChangedEventType = "io.cloudevents.discovery.changed";
        private const string CreatedEventType = "io.cloudevents.discovery.created";

        private CosmosClient cosmosClient;
        private readonly EventGridPublisherClient eventGridClient;

        public DiscoveryService(CosmosClient cosmosClient, EventGridPublisherClient eventGridClient)
        {
            this.cosmosClient = cosmosClient;
            this.eventGridClient = eventGridClient;
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
            var self = GetSelfReference(new Uri(req.Url.GetLeftPart(UriPartial.Path)));
            return await GetGroups<Group, Groups>(req, log, this.cosmosClient.GetContainer("discovery", "groups"));
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
            ILogger log)
        {
            var self = GetSelfReference(new Uri(req.Url.GetLeftPart(UriPartial.Path)));
            return await GetGroups<Definition, Definitions>(req, log, this.cosmosClient.GetContainer("discovery", "definitions"));
        }

        [Function("postDefinitions")]
        public async Task<HttpResponseData> PostGroupDefinitions(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "groups/{groupid}/definitions")]
            HttpRequestData req,
            ILogger log)
        {
            return await PostGroups<Definition, Definitions>(req, log, this.cosmosClient.GetContainer("discovery", "definitions"));
        }

        [Function("deleteDefinitions")]
        public async Task<HttpResponseData> DeleteGroupDefinitions(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "groups/{groupid}/definitions")]
            HttpRequestData req,
            ILogger log)
        {
            return await DeleteGroups<Reference, Definition, Definitions>(req, log, this.cosmosClient.GetContainer("discovery", "definitions"));
        }

        [Function("getDefinition")]
        public async Task<HttpResponseData> GetDefinitionDefinition(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "groups/{groupid}/definitions/{id}")]
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log)
        {
            return await GetResource<Definition>(req, groupid, id, log, this.cosmosClient.GetContainer("discovery", "definitions"));
        }

        [Function("putDefinition")]
        public async Task<HttpResponseData> PutDefinitionDefinition(
           [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "groups/{groupid}/definitions/{id}")]
            HttpRequestData req,
           string groupid,
           string id,
           ILogger log)
        {
            return await PutResource<Definition>(req, groupid, id, log, this.cosmosClient.GetContainer("discovery", "definitions"));
        }

        [Function("deleteDefinition")]
        public async Task<HttpResponseData> DeleteDefinitionDefinition(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "groups/{groupid}/definitions/{id}")]
            HttpRequestData req,
            string groupid,
            string id,
            ILogger log)
        {
            return await DeleteResource<Definition>(req, groupid, id, log, this.cosmosClient.GetContainer("discovery", "definitions"));
        }

        [Function("getSchemaGroups")]
        public async Task<HttpResponseData> GetSchemaGroups(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "schemagroups")]
            HttpRequestData req,
            ILogger log)
        {
            var self = GetSelfReference(new Uri(req.Url.GetLeftPart(UriPartial.Path)));
            return await GetGroups<SchemaGroup, SchemaGroups>(req, log, this.cosmosClient.GetContainer("discovery", "schemaGroups"));
        }

        [Function("postSchemaGroups")]
        public async Task<HttpResponseData> PostSchemaGroups(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "schemagroups")]
            HttpRequestData req,
            ILogger log)
        {
            return await PostGroups<SchemaGroup, SchemaGroups>(req, log, this.cosmosClient.GetContainer("discovery", "schemaGroups"));
        }

        [Function("deleteSchemaGroups")]
        public async Task<HttpResponseData> DeleteSchemaGroups(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "schemagroups")]
            HttpRequestData req,
            ILogger log)
        {
            return await DeleteGroups<Reference, SchemaGroup, SchemaGroups>(req, log, this.cosmosClient.GetContainer("discovery", "schemaGroups"));
        }

        [Function("getSchemaGroup")]
        public async Task<HttpResponseData> GetSchemaGroup(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "schemaGroups/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            return await GetGroup<SchemaGroup>(req, id, log, this.cosmosClient.GetContainer("discovery", "schemaGroups"));
        }

        [Function("putSchemaGroup")]
        public async Task<HttpResponseData> PutSchemaGroup(
           [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "schemaGroups/{id}")]
            HttpRequestData req,
           string id,
           ILogger log)
        {
            return await PutGroup<SchemaGroup>(req, id, log, this.cosmosClient.GetContainer("discovery", "schemaGroups"));
        }

        [Function("deleteSchemaGroup")]
        public async Task<HttpResponseData> DeleteSchemaGroup(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "schemaGroups/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            return await DeleteGroup<SchemaGroup>(req, id, log, this.cosmosClient.GetContainer("discovery", "schemaGroups"));
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
           [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "schemaGroups/{schemaGroupid}/schemas/{id}")]
            HttpRequestData req,
           string schemaGroupid,
           string id,
           ILogger log)
        {
            return await PutResource<Schema>(req, schemaGroupid, id, log, this.cosmosClient.GetContainer("discovery", "schemas"));
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
                        if (group.Epoch <= existingItem.Resource.Epoch)
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
                if (resource.Epoch <= existingResource.Resource.Epoch)
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
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Reference reference = JsonConvert.DeserializeObject<Reference>(requestBody);
            try
            {
                var existingResource = await container.ReadItemAsync<Reference>(reference.Id, new PartitionKey(reference.Id));
                var result = await container.DeleteItemAsync<T>(reference.Id, new PartitionKey(reference.Id));

                if (this.eventGridClient != null)
                {
                    var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, reference);
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
                        if (item.Epoch <= existingItem.Resource.Epoch)
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
                if (resource.Epoch <= existingItem.Resource.Epoch)
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
                Usage = Usage.Subscriber,
                Config = new EndpointConfigSubscriber
                {
                    Protocol = "HTTP",
                    Endpoints = new[] { new Uri(baseUri, "subscriptions") },
                },
                Description = "Discovery Endpoint",
                Epoch = 0,
                Id = "self",
                Definitions = new Definitions
                {
                    { CreatedEventType,  new CloudEventDefinition()
                    {
                        Metadata = new CloudEventMetadata {
                            Type = new MetadataPropertyString {
                                Value = CreatedEventType,
                                Required = true
                            }
                        },
                        Description = "Discovery Endpoint Entry Created",
                    } },
                    { ChangedEventType, new CloudEventDefinition()
                    {
                        Metadata = new CloudEventMetadata {
                            Type = new MetadataPropertyString {
                                Value = ChangedEventType,
                                Required = true
                            }
                        },
                        Description = "Discovery Endpoint Entry Changed"
                    } },
                    { DeletedEventType, new CloudEventDefinition()
                    {
                        Metadata = new CloudEventMetadata {
                            Type = new MetadataPropertyString {
                                Value = DeletedEventType,
                                Required = true
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
