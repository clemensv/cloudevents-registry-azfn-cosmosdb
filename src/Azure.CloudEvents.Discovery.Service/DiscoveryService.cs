using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Azure.Messaging;
using Azure.Messaging.EventGrid;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Serialization.HybridRow.Schemas;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
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

        [Function("endpoints_get")]
        public async Task<HttpResponseData> Endpoints(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "endpoints")]
            HttpRequestData req,
            ILogger log)
        {
            Endpoints endpoints = new Endpoints();
            var self = GetSelfReference(new Uri(req.Url.GetLeftPart(UriPartial.Path)));
            endpoints.Add(self.Id, self);
            var container = this.cosmosClient.GetContainer("discovery", "endpoints");
            using (FeedIterator<Endpoint> resultSet = container.GetItemQueryIterator<Endpoint>())
            {
                while (resultSet.HasMoreResults)
                {
                    foreach (var ep1 in await resultSet.ReadNextAsync())
                    {
                        ep1.Self = new Uri(req.Url, ep1.Id);
                        endpoints.Add(ep1.Id, ep1);
                    }
                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(endpoints);
            return res;
        }

        [Function("endpoints_post")]
        public async Task<HttpResponseData> EndpointsPost(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "endpoints")]
            HttpRequestData req,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Endpoints requestEndpoints = JsonConvert.DeserializeObject<Endpoints>(requestBody);
            Endpoints responseEndpoints = new Endpoints();
            if (requestEndpoints == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var container = this.cosmosClient.GetContainer("discovery", "endpoints");

            foreach (var service in requestEndpoints.Values)
            {

                try
                {
                    var existingItem = await container.ReadItemAsync<Endpoint>(service.Id, new PartitionKey(service.Id));
                    if (existingItem.StatusCode == HttpStatusCode.OK)
                    {
                        if (service.Epoch <= existingItem.Resource.Epoch)
                        {
                            // define code & response
                            return req.CreateResponse(HttpStatusCode.Conflict);
                        }
                        var result = await container.UpsertItemAsync<Endpoint>(service);
                        responseEndpoints.Add(result.Resource.Id, result.Resource);

                        var changedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), ChangedEventType, service);
                        await this.eventGridClient.SendEventAsync(changedEvent);
                    }
                    else
                    {

                        var result = await container.CreateItemAsync<Endpoint>(service);
                        responseEndpoints.Add(result.Resource.Id, result.Resource);

                        var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, service);
                        await this.eventGridClient.SendEventAsync(createdEvent);
                    }
                }
                catch (CosmosException)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);

                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(responseEndpoints);
            return res;

        }

        [Function("endpoints_delete")]
        public async Task<HttpResponseData> EndpointsDelete(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "endpoints")]
            HttpRequestData req,
            ILogger log)
        {

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            List<EndpointReference> requestServices = JsonConvert.DeserializeObject<List<EndpointReference>>(requestBody);
            Endpoints responseEndpoints = new Endpoints();
            if (requestServices == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var container = this.cosmosClient.GetContainer("discovery", "endpoints");

            foreach (var service in requestServices)
            {

                try
                {
                    var existingItem = await container.ReadItemAsync<Endpoint>(service.Id, new PartitionKey(service.Id));
                    if (existingItem.StatusCode == HttpStatusCode.OK)
                    {
                        var result = await container.DeleteItemAsync<Endpoint>(service.Id, new PartitionKey(service.Id));
                        responseEndpoints.Add(existingItem.Resource.Id, existingItem.Resource);

                        var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, existingItem);
                        await this.eventGridClient.SendEventAsync(deletedEvent);
                    }

                }
                catch (CosmosException)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);

                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(responseEndpoints);
            return res;

        }

        [Function("endpoint_get")]
        public async Task<HttpResponseData> Endpoint(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "endpoints/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "endpoints");

            if (id.Equals("self", StringComparison.InvariantCultureIgnoreCase))
            {
                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(GetSelfReference(new Uri(req.Url.GetLeftPart(UriPartial.Path))));
                return res;
            }

            try
            {

                var existingItem = await container.ReadItemAsync<Endpoint>(id, new PartitionKey(id));
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



        [Function("endpoint_put")]
        public async Task<HttpResponseData> EndpointPut(
            [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "endpoints/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "endpoints");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Endpoint endpoint = JsonConvert.DeserializeObject<Endpoint>(requestBody);
            try
            {
                var existingItem = await container.ReadItemAsync<Endpoint>(endpoint.Id, new PartitionKey(endpoint.Id));
                
                if (endpoint.Epoch <= existingItem.Resource.Epoch)
                {
                    // define code & response
                    return req.CreateResponse(HttpStatusCode.Conflict);
                }
                var result1 = await container.UpsertItemAsync<Endpoint>(endpoint);

                var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, endpoint);
                await this.eventGridClient.SendEventAsync(createdEvent);

                var res1 = req.CreateResponse(HttpStatusCode.OK);
                await res1.WriteAsJsonAsync(result1.Resource);
                return res1;
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
                var result = await container.CreateItemAsync<Endpoint>(endpoint, new PartitionKey(endpoint.Id));

                var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, endpoint);
                await this.eventGridClient.SendEventAsync(createdEvent);

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(result.Resource);
                return res;
            }
            catch (CosmosException ce)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

        }

        [Function("endpoint_delete")]
        public async Task<HttpResponseData> EndpointDelete(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "endpoints/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "endpoints");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            EndpointReference service = JsonConvert.DeserializeObject<EndpointReference>(requestBody);
            try
            {
                var existingItem = await container.ReadItemAsync<EndpointReference>(service.Id, new PartitionKey(service.Id));
                var result = await container.DeleteItemAsync<Endpoint>(service.Id, new PartitionKey(service.Id));

                var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, service);
                await this.eventGridClient.SendEventAsync(deletedEvent);

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

        [Function("group_get")]
        public async Task<HttpResponseData> Group(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "groups/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "groups");

            if (id.Equals("self", StringComparison.InvariantCultureIgnoreCase))
            {
                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(GetSelfReference(new Uri(req.Url.GetLeftPart(UriPartial.Path))));
                return res;
            }

            try
            {

                var existingItem = await container.ReadItemAsync<Group>(id, new PartitionKey(id));
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

        [Function("group_put")]
        public async Task<HttpResponseData> GroupPut(
            [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "groups/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "groups");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Group group = JsonConvert.DeserializeObject<Group>(requestBody);
            try
            {
                var existingItem = await container.ReadItemAsync<Group>(group.Id, new PartitionKey(group.Id));
                if (group.Epoch <= existingItem.Resource.Epoch)
                {
                    // define code & response
                    return req.CreateResponse(HttpStatusCode.Conflict);
                }
                var result1 = await container.UpsertItemAsync<Group>(group);

                var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, group);
                await this.eventGridClient.SendEventAsync(createdEvent);

                var res1 = req.CreateResponse(HttpStatusCode.OK);
                await res1.WriteAsJsonAsync(result1.Resource);
                return res1;
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
                var result = await container.CreateItemAsync<Group>(group, new PartitionKey(group.Id));

                var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, group);
                await this.eventGridClient.SendEventAsync(createdEvent);

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(result.Resource);
                return res;
            }
            catch (CosmosException ce)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

        }

        [Function("group_delete")]
        public async Task<HttpResponseData> GroupDelete(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "groups/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "groups");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            GroupReference service = JsonConvert.DeserializeObject<GroupReference>(requestBody);
            try
            {
                var existingItem = await container.ReadItemAsync<GroupReference>(service.Id, new PartitionKey(service.Id));
                var result = await container.DeleteItemAsync<Group>(service.Id, new PartitionKey(service.Id));

                var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, service);
                await this.eventGridClient.SendEventAsync(deletedEvent);

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

        [Function("groups_get")]
        public async Task<HttpResponseData> Groups(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "groups")]
            HttpRequestData req,
            ILogger log)
        {
            Groups groups = new Groups();
            var container = this.cosmosClient.GetContainer("discovery", "groups");
            using (FeedIterator<Group> resultSet = container.GetItemQueryIterator<Group>())
            {
                while (resultSet.HasMoreResults)
                {
                    foreach (var ep1 in await resultSet.ReadNextAsync())
                    {
                        ep1.Self = new Uri(req.Url, ep1.Id);
                        groups.Add(ep1.Id, ep1);
                    }
                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(groups);
            return res;
        }

        [Function("groups_post")]
        public async Task<HttpResponseData> GroupsPost(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "groups")]
            HttpRequestData req,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Groups requestGroups = JsonConvert.DeserializeObject<Groups>(requestBody);
            Groups responseGroups = new Groups();
            if (requestGroups == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var container = this.cosmosClient.GetContainer("discovery", "groups");

            foreach (var service in requestGroups.Values)
            {

                try
                {
                    var existingItem = await container.ReadItemAsync<Group>(service.Id, new PartitionKey(service.Id));
                    if (existingItem.StatusCode == HttpStatusCode.OK)
                    {
                        if (service.Epoch <= existingItem.Resource.Epoch)
                        {
                            // define code & response
                            return req.CreateResponse(HttpStatusCode.Conflict);
                        }
                        var result = await container.UpsertItemAsync<Group>(service);
                        responseGroups.Add(result.Resource.Id, result.Resource);

                        var changedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), ChangedEventType, service);
                        await this.eventGridClient.SendEventAsync(changedEvent);
                    }
                    else
                    {

                        var result = await container.CreateItemAsync<Group>(service);
                        responseGroups.Add(result.Resource.Id, result.Resource);

                        var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, service);
                        await this.eventGridClient.SendEventAsync(createdEvent);
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

        [Function("groups_delete")]
        public async Task<HttpResponseData> GroupsDelete(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "groups")]
            HttpRequestData req,
            ILogger log)
        {

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            List<GroupReference> requestServices = JsonConvert.DeserializeObject<List<GroupReference>>(requestBody);
            Groups responseGroups = new Groups();
            if (requestServices == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var container = this.cosmosClient.GetContainer("discovery", "groups");

            foreach (var service in requestServices)
            {

                try
                {
                    var existingItem = await container.ReadItemAsync<Group>(service.Id, new PartitionKey(service.Id));
                    if (existingItem.StatusCode == HttpStatusCode.OK)
                    {
                        var result = await container.DeleteItemAsync<Group>(service.Id, new PartitionKey(service.Id));
                        responseGroups.Add(existingItem.Resource.Id, existingItem.Resource);

                        var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, existingItem);
                        await this.eventGridClient.SendEventAsync(deletedEvent);
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

#if later

        [Function("schema_get")]
        public async Task<HttpResponseData> Schema(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "schemas/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "schemas");

            if (id.Equals("self", StringComparison.InvariantCultureIgnoreCase))
            {
                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(GetSelfReference(new Uri(req.Url.GetLeftPart(UriPartial.Path))));
                return res;
            }

            try
            {

                var existingItem = await container.ReadItemAsync<Schema>(id, new PartitionKey(id));
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



        [Function("schema_put")]
        public async Task<HttpResponseData> SchemaPut(
            [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "schemas/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "schemas");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Schema schema = JsonConvert.DeserializeObject<Schema>(requestBody);
            try
            {
                var existingItem = await container.ReadItemAsync<Schema>(schema.Id, new PartitionKey(schema.Id));
                if (schema.Epoch <= existingItem.Resource.Epoch)
                {
                    // define code & response
                    return req.CreateResponse(HttpStatusCode.Conflict);
                }
                var result1 = await container.UpsertItemAsync<Schema>(schema);

                var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, schema);
                await this.eventGridClient.SendEventAsync(createdEvent);

                var res1 = req.CreateResponse(HttpStatusCode.OK);
                await res1.WriteAsJsonAsync(result1.Resource);
                return res1;
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
                var result = await container.CreateItemAsync<Schema>(schema, new PartitionKey(schema.Id));

                var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, schema);
                await this.eventGridClient.SendEventAsync(createdEvent);

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(result.Resource);
                return res;
            }
            catch (CosmosException ce)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

        }

        [Function("schema_delete")]
        public async Task<HttpResponseData> SchemaDelete(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "schemas/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "schemas");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            SchemaReference service = JsonConvert.DeserializeObject<SchemaReference>(requestBody);
            try
            {
                var existingItem = await container.ReadItemAsync<SchemaReference>(service.Id, new PartitionKey(service.Id));
                var result = await container.DeleteItemAsync<Schema>(service.Id, new PartitionKey(service.Id));

                var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, service);
                await this.eventGridClient.SendEventAsync(deletedEvent);

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


        [Function("schemas_get")]
        public async Task<HttpResponseData> Schemas(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "schemas")]
            HttpRequestData req,
            ILogger log)
        {
            Schemas schemas = new Schemas();
            var self = GetSelfReference(new Uri(req.Url.GetLeftPart(UriPartial.Path)));
            schemas.Add(self.Id, self);
            var container = this.cosmosClient.GetContainer("discovery", "schemas");
            using (FeedIterator<Schema> resultSet = container.GetItemQueryIterator<Schema>())
            {
                while (resultSet.HasMoreResults)
                {
                    foreach (var ep1 in await resultSet.ReadNextAsync())
                    {
                        schemas.Add(ep1.Id, ep1);
                    }
                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(schemas);
            return res;
        }

        [Function("schemas_post")]
        public async Task<HttpResponseData> SchemasPost(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "schemas")]
            HttpRequestData req,
            ILogger log)
        {
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Schemas requestSchemas = JsonConvert.DeserializeObject<Schemas>(requestBody);
            Schemas responseSchemas = new Schemas();
            if (requestSchemas == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var container = this.cosmosClient.GetContainer("discovery", "schemas");

            foreach (var service in requestSchemas.Values)
            {

                try
                {
                    var existingItem = await container.ReadItemAsync<Schema>(service.Id, new PartitionKey(service.Id));
                    if (existingItem.StatusCode == HttpStatusCode.OK)
                    {
                        if (service.Epoch <= existingItem.Resource.Epoch)
                        {
                            // define code & response
                            return req.CreateResponse(HttpStatusCode.Conflict);
                        }
                        var result = await container.UpsertItemAsync<Schema>(service);
                        responseSchemas.Add(result.Resource.Id, result.Resource);

                        var changedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), ChangedEventType, service);
                        await this.eventGridClient.SendEventAsync(changedEvent);
                    }
                    else
                    {

                        var result = await container.CreateItemAsync<Schema>(service);
                        responseSchemas.Add(result.Resource.Id, result.Resource);

                        var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, service);
                        await this.eventGridClient.SendEventAsync(createdEvent);
                    }
                }
                catch (CosmosException)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);

                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(responseSchemas);
            return res;

        }

        [Function("schemas_delete")]
        public async Task<HttpResponseData> SchemasDelete(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "schemas")]
            HttpRequestData req,
            ILogger log)
        {

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            List<SchemaReference> requestServices = JsonConvert.DeserializeObject<List<SchemaReference>>(requestBody);
            Schemas responseSchemas = new Schemas();
            if (requestServices == null)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

            var container = this.cosmosClient.GetContainer("discovery", "schemas");

            foreach (var service in requestServices)
            {

                try
                {
                    var existingItem = await container.ReadItemAsync<Schema>(service.Id, new PartitionKey(service.Id));
                    if (existingItem.StatusCode == HttpStatusCode.OK)
                    {
                        var result = await container.DeleteItemAsync<Schema>(service.Id, new PartitionKey(service.Id));
                        responseSchemas.Add(existingItem.Resource.Id, existingItem.Resource);

                        var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, existingItem);
                        await this.eventGridClient.SendEventAsync(deletedEvent);
                    }

                }
                catch (CosmosException)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);

                }
            }
            var res = req.CreateResponse(HttpStatusCode.OK);
            await res.WriteAsJsonAsync(responseSchemas);
            return res;

        }

        [Function("schema_get")]
        public async Task<HttpResponseData> Schema(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "schemas/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "schemas");

            if (id.Equals("self", StringComparison.InvariantCultureIgnoreCase))
            {
                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(GetSelfReference(new Uri(req.Url.GetLeftPart(UriPartial.Path))));
                return res;
            }

            try
            {

                var existingItem = await container.ReadItemAsync<Schema>(id, new PartitionKey(id));
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

        [Function("schema_put")]
        public async Task<HttpResponseData> SchemaPut(
            [HttpTrigger(AuthorizationLevel.Function, "post", "put", Route = "schemas/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "schemas");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            Schema schema = JsonConvert.DeserializeObject<Schema>(requestBody);
            try
            {
                var existingItem = await container.ReadItemAsync<Schema>(schema.Id, new PartitionKey(schema.Id));
                if (schema.Epoch <= existingItem.Resource.Epoch)
                {
                    // define code & response
                    return req.CreateResponse(HttpStatusCode.Conflict);
                }
                var result1 = await container.UpsertItemAsync<Schema>(schema);

                var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, schema);
                await this.eventGridClient.SendEventAsync(createdEvent);

                var res1 = req.CreateResponse(HttpStatusCode.OK);
                await res1.WriteAsJsonAsync(result1.Resource);
                return res1;
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
                var result = await container.CreateItemAsync<Schema>(schema, new PartitionKey(schema.Id));

                var createdEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), CreatedEventType, schema);
                await this.eventGridClient.SendEventAsync(createdEvent);

                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(result.Resource);
                return res;
            }
            catch (CosmosException ce)
            {
                return req.CreateResponse(HttpStatusCode.BadRequest);
            }

        }

        [Function("schema_delete")]
        public async Task<HttpResponseData> SchemaDelete(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = "schemas/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "schemas");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            SchemaReference service = JsonConvert.DeserializeObject<SchemaReference>(requestBody);
            try
            {
                var existingItem = await container.ReadItemAsync<SchemaReference>(service.Id, new PartitionKey(service.Id));
                var result = await container.DeleteItemAsync<Schema>(service.Id, new PartitionKey(service.Id));

                var deletedEvent = new CloudEvent(req.Url.GetLeftPart(UriPartial.Path), DeletedEventType, service);
                await this.eventGridClient.SendEventAsync(deletedEvent);

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
#endif

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
