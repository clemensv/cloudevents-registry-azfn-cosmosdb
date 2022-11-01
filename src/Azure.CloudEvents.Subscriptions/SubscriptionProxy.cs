﻿// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace Azure.CloudEvents.EventGridBridge
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Azure.Core;
    using global::Azure.CloudEvents.Subscriptions;
    using Microsoft.Azure.Management.EventGrid;
    using Microsoft.Azure.Management.EventGrid.Models;
    using Microsoft.Azure.Management.ResourceManager;
    using Microsoft.Azure.Management.ResourceManager.Models;
    using Microsoft.Rest;
    using Newtonsoft.Json;
    using Filter = global::Azure.CloudEvents.Subscriptions.Filter;
    using Subscription = global::Azure.CloudEvents.Subscriptions.Subscription;


    //using Microsoft.Azure.Management.EventGrid.Models;

    public class SubscriptionProxy
    {
        Dictionary<string, List<Tuple<TopicTypeInfo, IEnumerable<EventType>>>> topicTypes;

        public SubscriptionProxy()
        {
            
        }

        public string DefaultSubscriptionId { get; set; }
        public string DefaultResourceGroup { get; set; }

        public async Task<Subscription> CreateSubscription(
            string subscriptionId, string resourceGroup, string provider, string resourceType,
            string resourceName, SubscriptionRequest subscriptionRequest,
            TokenCredentials authorizationToken)
        {
            
            var gridClient = new EventGridManagementClient(authorizationToken)
            {
                SubscriptionId = subscriptionId,
                LongRunningOperationRetryTimeout = 2
            };

            Guid subId = Guid.NewGuid();
            var eventSubscription = new EventSubscription()
            {
                Destination = new WebHookEventSubscriptionDestination()
                {
                    EndpointUrl = subscriptionRequest.Sink
                },
                EventDeliverySchema = "CloudEventSchemaV1_0"
            };

            if (subscriptionRequest.Filter != null)
            {
                List<AdvancedFilter> advancedFilters = new List<AdvancedFilter>();
                var filter = subscriptionRequest.Filter;
                AddFilter(filter, false, eventSubscription, advancedFilters);
                if (advancedFilters.Count > 0)
                {
                    eventSubscription.Filter.AdvancedFilters = advancedFilters;
                }
            }

            var sub = await gridClient.EventSubscriptions.CreateOrUpdateAsync($"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{provider}/{resourceType}/{resourceName}", subId.ToString(), eventSubscription);
            return ConvertToCloudEventsSubscription(sub);
        }

        public async IAsyncEnumerable<Subscription> GetSubscriptions(
            string subscriptionId, string resourceGroup, string provider, string resourceType, string resourceName, 
            TokenCredentials authorizationToken)
        {
            var gridClient = new EventGridManagementClient(authorizationToken)
            {
                SubscriptionId = subscriptionId,
                LongRunningOperationRetryTimeout = 2
            };

            var subs = await gridClient.EventSubscriptions.ListByResourceAsync(resourceGroup, provider, resourceType,
                resourceName, top: 100);
            do
            {
                foreach (var sub in subs)
                {
                    var ceSub = ConvertToCloudEventsSubscription(sub);
                    yield return ceSub;
                }

                var link = subs.NextPageLink;
                if (!string.IsNullOrEmpty(link))
                {
                    subs = await gridClient.EventSubscriptions.ListByResourceNextAsync(link);
                }
                else
                {
                    break;
                }

            } while (subs != null);
        }

        public async Task<Subscription> GetSubscription(string subscriptionId, string resourceGroup, string provider, string resourceType, 
            string resourceName, string eventSubscriptionId, TokenCredentials authorizationToken)
        {
           
            var gridClient = new EventGridManagementClient(authorizationToken)
            {
                SubscriptionId = subscriptionId,
                LongRunningOperationRetryTimeout = 2
            };

            var scope =
                $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{provider}/{resourceType}/{resourceName}";
            var sub = await gridClient.EventSubscriptions.GetAsync(scope, eventSubscriptionId);
            if (sub == null)
            {
                return null;
            }

            return ConvertToCloudEventsSubscription(sub);
        }

        public async Task DeleteSubscription(string subscriptionId, string resourceGroup, string provider, string resourceType,
            string resourceName, string eventSubscriptionId, TokenCredentials authorizationToken)
        {
            var gridClient = new EventGridManagementClient(authorizationToken)
            {
                SubscriptionId = subscriptionId,
                LongRunningOperationRetryTimeout = 2
            };

            var scope =
                $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroup}/providers/{provider}/{resourceType}/{resourceName}";
            await gridClient.EventSubscriptions.DeleteAsync(scope, eventSubscriptionId);
        }

        static Subscription ConvertToCloudEventsSubscription(EventSubscription sub)
        {
            var ceSub = new Subscription()
            {
                Id = sub.Name,
                Source = sub.Topic
            };
            if (sub.Destination is WebHookEventSubscriptionDestination)
            {
                ceSub.Protocol = Protocol.HTTP;
                ceSub.Sink = ((WebHookEventSubscriptionDestination)sub.Destination).EndpointBaseUrl;
            }

            return ceSub;
        }
          

        static void AddFilter(Filter filter, bool basicFilterAlreadySet, EventSubscription eventSubscription,
            List<AdvancedFilter> advancedFilters)
        {
            System.Type filterType = filter.GetType();
            if (filterType == typeof(PrefixFilter))
            {
                PrefixFilter f = (PrefixFilter)filter;
                if (f.Prefix != null && !string.IsNullOrEmpty(f.Prefix.Attribute))
                {
                    if (!basicFilterAlreadySet && string.Equals(f.Prefix.Attribute, "subject"))
                    {
                        eventSubscription.Filter.SubjectBeginsWith = f.Prefix.Value;
                    }
                    else
                    {
                        advancedFilters.Add(new StringBeginsWithAdvancedFilter(f.Prefix.Attribute,
                            new List<string>() { f.Prefix.Value }));
                    }
                }
            }
            else if (filterType == typeof(SuffixFilter))
            {
                SuffixFilter f = (SuffixFilter)filter;
                if (f.Suffix != null && !string.IsNullOrEmpty(f.Suffix.Attribute))
                {
                    if (!basicFilterAlreadySet && string.Equals(f.Suffix.Attribute, "subject"))
                    {
                        eventSubscription.Filter.SubjectBeginsWith = f.Suffix.Value;
                    }
                    else
                    {
                        advancedFilters.Add(new StringEndsWithAdvancedFilter(f.Suffix.Attribute,
                            new List<string>() { f.Suffix.Value }));
                    }
                }
            }
            else if (filterType == typeof(ExactFilter))
            {
                ExactFilter f = (ExactFilter)filter;
                if (f.Exact != null && !string.IsNullOrEmpty(f.Exact.Attribute))
                {
                    if (!basicFilterAlreadySet && string.Equals(f.Exact.Attribute, "subject"))
                    {
                        eventSubscription.Filter.SubjectBeginsWith = f.Exact.Value;
                    }
                    else
                    {
                        advancedFilters.Add(new StringInAdvancedFilter(f.Exact.Attribute,
                            new List<string>() { f.Exact.Value }));
                    }
                }
            }
            else if (filterType == typeof(AllFilter))
            {
                foreach (var inner in ((AllFilter)filter).All)
                {
                    AddFilter(inner, true, eventSubscription, advancedFilters);
                }
            }
            else
            {
                throw new InvalidOperationException("Unknown or unsupported filter type");
            }
        }


    }
}