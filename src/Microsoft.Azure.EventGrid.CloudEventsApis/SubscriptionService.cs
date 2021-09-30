
namespace Microsoft.Azure.EventGrid.CloudEventsApis
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Http.Extensions;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.EventGrid.CloudEventsApiBridge;
    using Microsoft.Azure.EventGrid.CloudEventsApis.Subscriptions;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class SubscriptionService 
    {
        readonly IResourceGroupDiscoveryMapper _mapper;

        const string collectionRoute =
            "subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{provider}/{resourceType}/{resourceName}/eventSubscriptions";
        const string subscriptionRoute = collectionRoute + "/{eventSubscriptionId}";

        public SubscriptionService(IResourceGroupDiscoveryMapper mapper)
        {
            this._mapper = mapper;
        }
        
        [FunctionName("CreateSubscription")]
        public async Task<IActionResult> CreateSubscriptionAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = collectionRoute)]
            HttpRequest req,
            string subscriptionId,
            string resourceGroup,
            string provider,
            string resourceType,
            string resourceName,
            ILogger log)
        {
            var subscriptionRequest = JsonConvert.DeserializeObject<SubscriptionRequest>(await req.ReadAsStringAsync());
            var subscription = await _mapper.CreateSubscription(subscriptionId, resourceGroup, provider, resourceType, resourceName, subscriptionRequest);
            
            return new CreatedResult(new Uri(new Uri(req.GetEncodedUrl()), subscription.Id).AbsoluteUri, subscription);
        }

        [FunctionName("DeleteSubscription")]
        public async Task<IActionResult> DeleteSubscriptionAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = subscriptionRoute)]
            HttpRequest req, 
            string subscriptionId,
            string resourceGroup,
            string provider,
            string resourceType,
            string resourceName,
            string eventSubscriptionId,
            ILogger log)
        {
            await _mapper.DeleteSubscription(subscriptionId, resourceGroup, provider, resourceType, resourceName, eventSubscriptionId);
            return new OkObjectResult(string.Empty);
        }

        [FunctionName("GetSubscription")]
        public async Task<IActionResult> GetSubscriptionAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = subscriptionRoute)]
            HttpRequest req,
            string subscriptionId,
            string resourceGroup,
            string provider,
            string resourceType,
            string resourceName,
            string eventSubscriptionId,
            ILogger log)
        {
            var sub = await _mapper.GetSubscription(subscriptionId, resourceGroup, provider, resourceType, resourceName, eventSubscriptionId);
            return new OkObjectResult(sub);
        }

        
        [FunctionName("GetSubscriptions")]
        public async Task<IActionResult> GetSubscriptionsAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = collectionRoute)]
            HttpRequest req,
            string subscriptionId,
            string resourceGroup,
            string provider,
            string resourceType,
            string resourceName,
            ILogger log )
        {
            List<Subscription> subs = new List<Subscription>();

            await foreach (var sub in _mapper.GetSubscriptions(subscriptionId, resourceGroup, provider, resourceType, resourceName))
            {
                subs.Add(sub);
            }
            return new OkObjectResult(subs);
        }
                                                                     
        [FunctionName("UpdateSubscription")]
        public async Task<IActionResult> UpdateSubscription(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = subscriptionRoute)]
            HttpRequest req,
            string subscriptionId,
            string resourceGroup,
            string provider,
            string resourceType,
            string resourceName,
            string eventSubscriptionId,
            ILogger log)
        {
            return new StatusCodeResult(405);
        }
                          
    }
}