
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

    public class RegistryEventSubscriptionService
    {
        readonly SubscriptionProxy _proxy;
        private readonly string subscriptionId;
        private readonly string resourceGroup;
        private readonly string provider;
        private readonly string resourceType;
        private readonly string resourceName;
        const string collectionRoute = "/subscriptions";
        const string subscriptionRoute = collectionRoute + "/{eventSubscriptionId}";

        public RegistryEventSubscriptionService(SubscriptionProxy proxy)
        {
            this._proxy = proxy;
            this.subscriptionId = proxy.DefaultSubscriptionId;
            this.resourceGroup = proxy.DefaultResourceGroup;
            this.provider = "Microsoft.EventGrid";
            this.resourceType = "topics";
            this.resourceName = "cloudevents-registry";
        }

        [Function("CreateSubscription")]
        public async Task<HttpResponseData> CreateSubscriptionAsync(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = collectionRoute)]
            HttpRequestData req,
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
#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
        public async Task<HttpResponseData> UpdateSubscription(
#pragma warning restore CS1998 // Async method lacks 'await' operators and will run synchronously
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = subscriptionRoute)]
            HttpRequestData req,
            string eventSubscriptionId,
            ILogger log)
        {
            return req.CreateResponse(System.Net.HttpStatusCode.MethodNotAllowed);
        }

    }
}