using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Azure.CloudEvents.Discovery
{
    public partial class DiscoveryService
    {
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

    }
}
