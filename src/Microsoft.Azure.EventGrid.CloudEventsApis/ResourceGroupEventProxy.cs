﻿// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace Microsoft.Azure.EventGrid.CloudEventsApiBridge
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using System.Security.Authentication;
    using System.Threading.Tasks;
    using Microsoft.Azure.EventGrid.CloudEventsApis.Discovery;
    using Microsoft.Azure.EventGrid.CloudEventsApis.Subscriptions;
    using Microsoft.Azure.Management.EventGrid;
    using Microsoft.Azure.Management.EventGrid.Models;
    using Microsoft.Azure.Management.ResourceManager;
    using Microsoft.Azure.Management.ResourceManager.Models;
    using Microsoft.Rest;
    using Newtonsoft.Json;
    using EventType = Microsoft.Azure.EventGrid.CloudEventsApis.EventType;
    using Filter = Microsoft.Azure.EventGrid.CloudEventsApis.Subscriptions.Filter;
    using Subscription = Microsoft.Azure.EventGrid.CloudEventsApis.Subscriptions.Subscription;
    using Type = Microsoft.Azure.EventGrid.CloudEventsApis.Discovery.Type;

    //using Microsoft.Azure.Management.EventGrid.Models;

    public class ResourceGroupEventProxy : IResourceGroupEventProxy
    {
        readonly TokenCredentials tokenCredentials;

        readonly string resourceGroupName;

        readonly string fixedSubscriptionId;

        EventGridManagementClient gridClient;

        bool initialized = false;

        readonly object initializeMutex = new object();

        ResourceManagementClient resourceGroupClient;

        Dictionary<string, List<Tuple<SystemTopic, IEnumerable<EventType>>>> topicTypes;

        public ResourceGroupEventProxy(string subscriptionId, string resourceGroupName,
           TokenCredentials tokenCredentials)
        {
            this.fixedSubscriptionId = subscriptionId;
            this.resourceGroupName = resourceGroupName;
            this.tokenCredentials = tokenCredentials;
        }

        public async Task<Subscription> CreateSubscription(string subscriptionId, string resourceGroup, string provider, string resourceType,
            string resourceName, SubscriptionRequest subscriptionRequest)
        {
            if ( !subscriptionId.Equals(fixedSubscriptionId))
                throw new UnauthorizedAccessException();

            await InitializeAsync();

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

        

        public async IAsyncEnumerable<Service> EnumerateServicesAsync(Uri baseUri)
        {
            await InitializeAsync();
            await foreach (var resource in EnumerateResourcesAsync())
            {
                if ( resource.Type.StartsWith("Microsoft.EventGrid", StringComparison.OrdinalIgnoreCase))
                    continue;
                
                var self = new Uri(baseUri, resource.Id + "/");
                var service = new Service()
                {
                    Id = self.AbsoluteUri,
                    Name = resource.Id,
                    Description = resource.Name,
                    Docsurl = GetDocsUrl(resource.Type),
                    Protocols = new List<string>() { "HTTP" },
                    Subscriptionurl = new Uri(self, "eventSubscriptions").AbsoluteUri,
                    Url = self.AbsoluteUri,
                    Authscope = baseUri.AbsoluteUri,
                };
                if (resource.ChangedTime.HasValue)
                {
                    service.AdditionalProperties.Add("azreschangedtime", resource.ChangedTime.Value);
                }

                if (resource.CreatedTime.HasValue)
                {
                    service.AdditionalProperties.Add("azrescreatedtime", resource.CreatedTime);
                }

                if (!string.IsNullOrEmpty(resource.ProvisioningState))
                {
                    service.AdditionalProperties.Add("azresprovisioningstate", resource.ProvisioningState);
                }

                if (!string.IsNullOrEmpty(resource.Kind))
                {
                    service.AdditionalProperties.Add("azreskind", resource.Kind);
                }

                if (!string.IsNullOrEmpty(resource.Id))
                {
                    service.AdditionalProperties.Add("azresid", resource.Id);
                }

                if (!string.IsNullOrEmpty(resource.Location))
                {
                    service.AdditionalProperties.Add("azreslocation", resource.Location);
                }

                if (!string.IsNullOrEmpty(resource.Type))
                {
                    service.AdditionalProperties.Add("azrestype", resource.Type);
                    service.Events = new List<Type>();
                    var resType = resource.Type.Split('/')[0].ToLowerInvariant();
                    if (this.topicTypes.TryGetValue(resType, out var info))
                    {
                        foreach (var typeInfo in info)
                        {
                            foreach (var eventType in typeInfo.Item2)
                            {
                                string sourcePattern = typeInfo.Item1.Properties.SourceResourceFormat;
                                sourcePattern = sourcePattern.Replace('<', '{').Replace('>', '}');

                                service.Events.Add(new Type()
                                {
                                    Type1 = eventType.Name,
                                    Dataschema = eventType.Properties.SchemaUrl,
                                    Specversions = new string[] { "1.0" },
                                    Sourcetemplate = new Uri(baseUri, sourcePattern).ToString(),
                                    Description = eventType.Name
                                });
                            }
                        }
                    }
                }

                yield return service;
            }
        }

        public async IAsyncEnumerable<Subscription> GetSubscriptions(string subscriptionId, string resourceGroup, string provider, string resourceType, string resourceName)
        {
            if (!subscriptionId.Equals(fixedSubscriptionId))
                throw new UnauthorizedAccessException();

            await InitializeAsync();

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

        public async Task<Subscription> GetSubscription(string subscriptionId, string resourceGroup, string provider, string resourceType, string resourceName, string eventSubscriptionId)
        {
            if (!subscriptionId.Equals(fixedSubscriptionId))
                throw new UnauthorizedAccessException();

            await InitializeAsync();
            
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
            string resourceName, string eventSubscriptionId)
        {
            if (!subscriptionId.Equals(fixedSubscriptionId))
                throw new UnauthorizedAccessException();

            await InitializeAsync();

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

        public async Task InitializeAsync()
        {
            lock (initializeMutex)
            {
                if (initialized)
                {
                    return;
                }
                else
                {
                    initialized = true;
                }
            }

            this.resourceGroupClient = new ResourceManagementClient(tokenCredentials)
            {
                SubscriptionId = fixedSubscriptionId
            };
            gridClient = new EventGridManagementClient(tokenCredentials)
            {
                SubscriptionId = fixedSubscriptionId,
                LongRunningOperationRetryTimeout = 2
            };
            this.topicTypes = new Dictionary<string, List<Tuple<SystemTopic, IEnumerable<EventType>>>>();

            // we can fetch those just once since they're (fairly) stable
            var topics = EnumerateSystemTopics();
            foreach (var topic in topics)
            {
                //var eventTypes = await gridClient.TopicTypes.ListEventTypesAsync(topic.Type);
                var keys = topic.Name.Split('.');
                var key = $"{keys[0]}.{keys[1]}".ToLowerInvariant();

                var eventTypes = EnumerateEventTypes(topic.Name);
                if (this.topicTypes.TryGetValue(key, out var info))
                {
                    info.Add(new Tuple<SystemTopic, IEnumerable<EventType>>(topic, eventTypes));
                }
                else
                {
                    this.topicTypes.Add(key, new List<Tuple<SystemTopic, IEnumerable<EventType>>>()
                    {
                        new Tuple<SystemTopic, IEnumerable<EventType>>(topic, eventTypes)
                    });
                }
            }
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

        IEnumerable<EventType> EnumerateEventTypes(string types)
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"Microsoft.Azure.EventGrid.CloudEventsApis.{types.ToLowerInvariant()}.json";

            var manifestResourceStream = assembly.GetManifestResourceStream(resourceName);
            if (manifestResourceStream == null)
            {
                return new EventType[0];
            }
            else
            {
                using (Stream stream = manifestResourceStream)
                using (StreamReader reader = new StreamReader(stream))
                {
                    return JsonConvert.DeserializeObject<EventType[]>(reader.ReadToEnd());
                }
            }
        }

        async IAsyncEnumerable<GenericResourceExpanded> EnumerateResourcesAsync()
        {
            var resources = await resourceGroupClient.Resources.ListByResourceGroupAsync(resourceGroupName);

            //// we will filter those down to resources that have a matching Event Grid provider 
            foreach (var resource in resources)
            {
                if (this.topicTypes.ContainsKey(resource.Type.Split('/')[0].ToLowerInvariant()))
                {
                    yield return resource;
                }
            }
        }

        IEnumerable<SystemTopic> EnumerateSystemTopics()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = "Microsoft.Azure.EventGrid.CloudEventsApis.topictypes.json";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                return JsonConvert.DeserializeObject<SystemTopic[]>(reader.ReadToEnd());
            }

            //return gridClient.TopicTypes.ListAsync();
        }

        string GetDocsUrl(string resourceType)
        {
            string provider = resourceType.Split('/')[0].ToLowerInvariant();

            switch (provider)
            {
                case "microsoft.storage":
                    return "https://docs.microsoft.com/azure/event-grid/event-schema-blob-storage";
                case "microsoft.appconfiguration":
                    return "https://docs.microsoft.com/azure/event-grid/event-schema-app-configuration";
                case "microsoft.web":
                    return "https://docs.microsoft.com/azure/event-grid/event-schema-app-service";
                case "microsoft.communication":
                    return "https://docs.microsoft.com/azure/event-grid/event-schema-communication-services";
                case "microsoft.containerregistry":
                    return "https://docs.microsoft.com/azure/event-grid/event-schema-container-registry";
                case "microsoft.eventhub":
                    return "https://docs.microsoft.com/azure/event-grid/event-schema-event-hubs";
                case "microsoft.devices":
                    return "https://docs.microsoft.com/azure/event-grid/event-schema-iot-hub";
                case "microsoft.keyvault":
                    return "https://docs.microsoft.com/azure/event-grid/event-schema-key-vault";
                case "microsoft.machinelearningservice":
                    return "https://docs.microsoft.com/azure/event-grid/event-schema-machine-learning";
                case "microsoft.maps":
                    return "https://docs.microsoft.com/azure/event-grid/event-schema-azure-maps";
                case "microsoft.media":
                    return "https://docs.microsoft.com/azure/event-grid/event-schema-media-services";
                case "microsoft.resources":
                    return "https://docs.microsoft.com/azure/event-grid/event-schema-resource-groups";
                case "microsoft.servicebus":
                    return "https://docs.microsoft.com/azure/event-grid/event-schema-service-bus";
                case "microsoft.signalrservice":
                    return "https://docs.microsoft.com/azure/event-grid/event-schema-azure-signalr";
            }

            return null;
        }
    }
}