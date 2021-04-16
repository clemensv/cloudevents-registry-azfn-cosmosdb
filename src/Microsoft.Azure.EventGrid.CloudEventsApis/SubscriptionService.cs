
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
        readonly IResourceGroupEventProxy proxy;

        const string collectionRoute =
            "subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{provider}/{resourceType}/{resourceName}/eventSubscriptions";
        const string subscriptionRoute = collectionRoute + "/{eventSubscriptionId}";

        public SubscriptionService(IResourceGroupEventProxy proxy)
        {
            this.proxy = proxy;
        }
        
        [FunctionName("CreateSubscription")]
        public async Task<IActionResult> CreateSubscriptionAsync(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = collectionRoute)]
            HttpRequest req,
            string subscriptionId,
            string resourceGroup,
            string provider,
            string resourceType,
            string resourceName,
            ILogger log)
        {
            var subscriptionRequest = JsonConvert.DeserializeObject<SubscriptionRequest>(await req.ReadAsStringAsync());
            var subscription = await proxy.CreateSubscription(subscriptionId, resourceGroup, provider, resourceType, resourceName, subscriptionRequest);
            
            return new CreatedResult(new Uri(new Uri(req.GetEncodedUrl()), subscription.Id).AbsoluteUri, subscription);
        }

        [FunctionName("DeleteSubscription")]
        public async Task<IActionResult> DeleteSubscriptionAsync(
            [HttpTrigger(AuthorizationLevel.Function, "delete", Route = subscriptionRoute)]
            HttpRequest req, 
            string subscriptionId,
            string resourceGroup,
            string provider,
            string resourceType,
            string resourceName,
            string eventSubscriptionId,
            ILogger log)
        {
            await proxy.DeleteSubscription(subscriptionId, resourceGroup, provider, resourceType, resourceName, eventSubscriptionId);
            return new OkObjectResult(string.Empty);
        }

        [FunctionName("GetSubscription")]
        public async Task<IActionResult> GetSubscriptionAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = subscriptionRoute)]
            HttpRequest req,
            string subscriptionId,
            string resourceGroup,
            string provider,
            string resourceType,
            string resourceName,
            string eventSubscriptionId,
            ILogger log)
        {
            var sub = await proxy.GetSubscription(subscriptionId, resourceGroup, provider, resourceType, resourceName, eventSubscriptionId);
            return new OkObjectResult(sub);
        }

        
        [FunctionName("GetSubscriptions")]
        public async Task<IActionResult> GetSubscriptionsAsync(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = collectionRoute)]
            HttpRequest req,
            string subscriptionId,
            string resourceGroup,
            string provider,
            string resourceType,
            string resourceName,
            ILogger log )
        {
            List<Subscription> subs = new List<Subscription>();

            await foreach (var sub in proxy.GetSubscriptions(subscriptionId, resourceGroup, provider, resourceType, resourceName))
            {
                subs.Add(sub);
            }
            return new OkObjectResult(subs);
        }
                                                                     
        [FunctionName("UpdateSubscription")]
        public async Task<IActionResult> UpdateSubscription(
            [HttpTrigger(AuthorizationLevel.Function, "put", Route = subscriptionRoute)]
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