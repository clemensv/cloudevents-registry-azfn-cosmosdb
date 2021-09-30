
namespace Microsoft.Azure.EventGrid.CloudEventsApiBridge
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.Azure.EventGrid.CloudEventsApis.Discovery;
    using Microsoft.Azure.EventGrid.CloudEventsApis.Subscriptions;

    public interface IResourceGroupDiscoveryMapper
    {
        Task <Subscription> CreateSubscription(string subscriptionId, string resourceGroup, string provider, string resourceType, string resourceName, SubscriptionRequest subscriptionRequest);

        IAsyncEnumerable<Service> EnumerateServicesAsync(Uri baseUri);
        IAsyncEnumerable<Subscription> GetSubscriptions(string subscriptionId, string resourceGroup, string provider, string resourceType, string resourceName);
        Task<Subscription> GetSubscription(string subscriptionId, string resourceGroup, string provider, string resourceType, string resourceName, string eventSubscriptionId);

        Task DeleteSubscription(string subscriptionId, string resourceGroup, string provider, string resourceType, string resourceName, string eventSubscriptionId);
    }
}