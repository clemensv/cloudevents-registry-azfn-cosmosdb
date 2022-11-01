
namespace Azure.CloudEvents.EventGridBridge
{
    using System;
    using System.Linq;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Azure.CloudEvents.Subscriptions;
    using Microsoft.Azure.Functions.Worker;
    using Microsoft.Azure.Functions.Worker.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;
    using Microsoft.Rest;

    public class SubscriptionService
    {
        readonly SubscriptionProxy _proxy;

        const string collectionRoute =
            "subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{provider}/{resourceType}/{resourceName}/eventSubscriptions";
        const string subscriptionRoute = collectionRoute + "/{eventSubscriptionId}";

        public SubscriptionService(SubscriptionProxy proxy)
        {
            this._proxy = proxy;
        }

        [Function("CreateSubscription")]
        public async Task<HttpResponseData> CreateSubscriptionAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = collectionRoute)]
            HttpRequestData req,
            string subscriptionId,
            string resourceGroup,
            string provider,
            string resourceType,
            string resourceName,
            ILogger log)
        {
            var subscriptionRequest = JsonConvert.DeserializeObject<SubscriptionRequest>(await req.ReadAsStringAsync());
            var authorizationHeader = req.Headers?.GetValues("Authorization")?.First();
            if (authorizationHeader == null)
            {
                return req.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
            }
            var tokenCredentials = new Microsoft.Rest.TokenCredentials(authorizationHeader);

            var subscription = await _proxy.CreateSubscription(subscriptionId, resourceGroup, provider, resourceType, resourceName, subscriptionRequest, tokenCredentials);
            var res = req.CreateResponse(System.Net.HttpStatusCode.Created);
            res.Headers.Add("Location", new Uri(req.Url, subscription.Id).AbsoluteUri);
            await res.WriteAsJsonAsync(subscription);
            return res;
        }

        [Function("DeleteSubscription")]
        public async Task<HttpResponseData> DeleteSubscriptionAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = subscriptionRoute)]
            HttpRequestData req,
            string subscriptionId,
            string resourceGroup,
            string provider,
            string resourceType,
            string resourceName,
            string eventSubscriptionId,
            ILogger log)
        {
            var authorizationHeader = req.Headers?.GetValues("Authorization")?.First();
            if (authorizationHeader == null)
            {
                return req.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
            }
            var tokenCredentials = new Microsoft.Rest.TokenCredentials(authorizationHeader);

            await _proxy.DeleteSubscription(subscriptionId, resourceGroup, provider, resourceType, resourceName, eventSubscriptionId, tokenCredentials);
            return req.CreateResponse(System.Net.HttpStatusCode.OK);
        }

        [Function("GetSubscription")]
        public async Task<HttpResponseData> GetSubscriptionAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = subscriptionRoute)]
            HttpRequestData req,
            string subscriptionId,
            string resourceGroup,
            string provider,
            string resourceType,
            string resourceName,
            string eventSubscriptionId,
            ILogger log)
        {
            var authorizationHeader = req.Headers?.GetValues("Authorization")?.First();
            if (authorizationHeader == null)
            {
                return req.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
            }
            var tokenCredentials = new Microsoft.Rest.TokenCredentials(authorizationHeader);

            var sub = await _proxy.GetSubscription(subscriptionId, resourceGroup, provider, resourceType, resourceName, eventSubscriptionId, tokenCredentials);
            var res = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await res.WriteAsJsonAsync(sub);
            return res;
        }


        [Function("GetSubscriptions")]
        public async Task<HttpResponseData> GetSubscriptionsAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = collectionRoute)]
            HttpRequestData req,
            string subscriptionId,
            string resourceGroup,
            string provider,
            string resourceType,
            string resourceName,
            ILogger log)
        {
            List<Subscription> subs = new List<Subscription>();

            var authorizationHeader = req.Headers?.GetValues("Authorization")?.First();
            if (authorizationHeader == null)
            {
                return req.CreateResponse(System.Net.HttpStatusCode.Unauthorized);
            }
            var tokenCredentials = new Microsoft.Rest.TokenCredentials(authorizationHeader);

            await foreach (var sub in _proxy.GetSubscriptions(subscriptionId, resourceGroup, provider, resourceType, resourceName, tokenCredentials))
            {
                subs.Add(sub);
            }
            var res = req.CreateResponse(System.Net.HttpStatusCode.OK);
            await res.WriteAsJsonAsync(subs);
            return res;
        }

        [Function("UpdateSubscription")]
        public async Task<HttpResponseData> UpdateSubscription(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = subscriptionRoute)]
            HttpRequestData req,
            string subscriptionId,
            string resourceGroup,
            string provider,
            string resourceType,
            string resourceName,
            string eventSubscriptionId,
            ILogger log)
        {
            return req.CreateResponse(System.Net.HttpStatusCode.MethodNotAllowed);
        }

    }
}