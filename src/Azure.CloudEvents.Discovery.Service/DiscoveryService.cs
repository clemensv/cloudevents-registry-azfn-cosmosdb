using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Azure.CloudEvents.Discovery
{
    public class DiscoveryService
    {
        private CosmosClient cosmosClient;

        public DiscoveryService(CosmosClient cosmosClient)
        {
            this.cosmosClient = cosmosClient;
        }

        [Function("services")]
        public async Task<HttpResponseData> Services(
            [HttpTrigger(AuthorizationLevel.User|AuthorizationLevel.Function, "get", "post", "delete", "options", Route = "services")]
            HttpRequestData req,
            ILogger log)
        {
            if (req.Method.Equals("get", StringComparison.InvariantCultureIgnoreCase))
            {
                List<Service> services = new List<Service>();
                var container = this.cosmosClient.GetContainer("discovery", "services");
                using (FeedIterator<Service> resultSet = container.GetItemQueryIterator<Service>())
                {
                    while (resultSet.HasMoreResults)
                    {
                        FeedResponse<Service> response = await resultSet.ReadNextAsync();
                        services.AddRange(response);
                        if (response.Diagnostics != null)
                        {
                            Console.WriteLine($" Diagnostics {response.Diagnostics.ToString()}");
                        }
                    }
                }
                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(services);
                return res;
            }
            else if (req.Method.Equals("post", StringComparison.InvariantCultureIgnoreCase))
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                List<Service> requestServices = JsonConvert.DeserializeObject<List<Service>>(requestBody);
                List<Service> responseServices = new List<Service>();
                if (requestServices == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                var container = this.cosmosClient.GetContainer("discovery", "services");

                foreach (var service in requestServices)
                {

                    try
                    {
                        var existingItem = await container.ReadItemAsync<Service>(service.Id, new PartitionKey(service.Id));
                        if (existingItem.StatusCode == HttpStatusCode.OK)
                        {
                            if ( service.Epoch <= existingItem.Resource.Epoch)
                            {
                                // define code & response
                                return req.CreateResponse(HttpStatusCode.Conflict);
                            }
                            var result = await container.UpsertItemAsync<Service>(service);
                            responseServices.Add(result.Resource);
                        }
                        else
                        {

                            var result = await container.CreateItemAsync<Service>(service);
                            responseServices.Add(result.Resource);
                        }
                    }
                    catch (CosmosException)
                    {
                        return req.CreateResponse(HttpStatusCode.BadRequest);

                    }
                }
                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(responseServices);
                return res;
            }
            else if (req.Method.Equals("delete", StringComparison.InvariantCultureIgnoreCase))
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                List<Serviceinstance> requestServices = JsonConvert.DeserializeObject<List<Serviceinstance>>(requestBody);
                List<Service> responseServices = new List<Service>();
                if (requestServices == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }

                var container = this.cosmosClient.GetContainer("discovery", "services");

                foreach (var service in requestServices)
                {

                    try
                    {
                        var existingItem = await container.ReadItemAsync<Service>(service.Id, new PartitionKey(service.Id));
                        if (existingItem.StatusCode == HttpStatusCode.OK)
                        {
                            var result = await container.DeleteItemAsync<Service>(service.Id, new PartitionKey(service.Id));
                            responseServices.Add(existingItem);
                        }

                    }
                    catch (CosmosException)
                    {
                        return req.CreateResponse(HttpStatusCode.BadRequest);

                    }
                }
                var res = req.CreateResponse(HttpStatusCode.OK);
                await res.WriteAsJsonAsync(responseServices);
                return res;
            }
            return req.CreateResponse(HttpStatusCode.MethodNotAllowed);
        }

        [Function("service")]
        public async Task<HttpResponseData> Service(
            [HttpTrigger(AuthorizationLevel.User|AuthorizationLevel.Function, "get", "post", "put", "delete", "options", Route = "services/{id}")]
            HttpRequestData req,
            string id,
            ILogger log)
        {
            var container = this.cosmosClient.GetContainer("discovery", "services");

            if (req.Method.Equals("get", StringComparison.InvariantCultureIgnoreCase))
            {
                try
                {

                    var existingItem = await container.ReadItemAsync<Service>(id, new PartitionKey(id));
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    await res.WriteAsJsonAsync(existingItem.Resource);
                    return res;
                }
                catch(CosmosException ce)
                {
                    if(ce.StatusCode == HttpStatusCode.NotFound)
                    {
                        return req.CreateResponse(HttpStatusCode.NotFound);
                    }
                    return req.CreateResponse(HttpStatusCode.InternalServerError);
                }
            }
            else if (req.Method.Equals("put", StringComparison.InvariantCultureIgnoreCase) ||
                req.Method.Equals("post", StringComparison.InvariantCultureIgnoreCase))
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                Service service = JsonConvert.DeserializeObject<Service>(requestBody);
                try
                {
                    var existingItem = await container.ReadItemAsync<Service>(service.Id, new PartitionKey(service.Id));
                    if (service.Epoch <= existingItem.Resource.Epoch)
                    {
                        // define code & response
                        return req.CreateResponse(HttpStatusCode.Conflict);
                    }
                    var result1 = await container.UpsertItemAsync<Service>(service);
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
                    var result = await container.CreateItemAsync<Service>(service, new PartitionKey(service.Id));
                    var res = req.CreateResponse(HttpStatusCode.OK);
                    await res.WriteAsJsonAsync(result.Resource);
                    return res;
                }
                catch(CosmosException ce)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest);
                }
            }
            else if (req.Method.Equals("delete", StringComparison.InvariantCultureIgnoreCase))
            {
                string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
                Serviceinstance service = JsonConvert.DeserializeObject<Serviceinstance>(requestBody);
                try
                {
                    var existingItem = await container.ReadItemAsync<Serviceinstance>(service.Id, new PartitionKey(service.Id));
                    var result = await container.DeleteItemAsync<Service>(service.Id, new PartitionKey(service.Id));
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
            else
            {
                return req.CreateResponse(HttpStatusCode.MethodNotAllowed);
            }
        }
    }
}
