// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace Azure.CloudEvents.Discovery.SystemTopicLoader
{
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.CloudEvents.Discovery;
    using Microsoft.Azure.Management.EventGrid;
    using Microsoft.Azure.Management.EventGrid.Models;
    using Microsoft.Azure.Management.ResourceManager;
    using Microsoft.Azure.Management.ResourceManager.Models;
    using Microsoft.Rest;
    
    public class ResourceTopicEnumerator
    {
        private readonly string azureSubscriptionId;
        readonly TokenCredentials tokenCredentials;
        EventGridManagementClient gridClient;
        bool initialized = false;
        readonly object initializeMutex = new object();
        ResourceManagementClient resourceGroupClient;
        Dictionary<string, List<Tuple<TopicTypeInfo, IEnumerable<EventType>>>> topicTypes;

        public ResourceTopicEnumerator(string azureSubscriptionId, TokenCredentials tokenCredentials)
        {
            this.azureSubscriptionId = azureSubscriptionId;
            this.tokenCredentials = tokenCredentials;
        }

        public async IAsyncEnumerable<Service> EnumerateDiscoveryServicesAsync(Uri referencesBaseUri, string azureResourceGroupName)
        {
            await InitializeAsync();
            await foreach (var resource in EnumerateResourceGroupResourcesWithEventsAsync(azureSubscriptionId, azureResourceGroupName))
            {
                if (resource.Type.StartsWith("Microsoft.EventGrid", StringComparison.OrdinalIgnoreCase))
                    continue;

                yield return MapGridTopicToDiscoveryServiceObject(referencesBaseUri, resource);
            }
        }

        Service MapGridTopicToDiscoveryServiceObject(Uri baseUri, GenericResourceExpanded resource)
        {
            string normalizedId = NormalizeId(resource.Id);
            var authority = new Uri(baseUri, "services/");
            var self = new Uri(authority, "./" + normalizedId);
            var service = new Service()
            {
                Id = normalizedId,
                Name = resource.Name,
                Description = $"{resource.Name} {resource.Kind}",
                Docsurl = GetDocsUrl(resource.Type),
                Epoch = (resource.ChangedTime.HasValue ? resource.ChangedTime?.Ticks : resource.CreatedTime?.Ticks) ?? 0,
                Protocols = new List<string>() { "HTTP" },
                Subscriptionurl = new Uri(self, "subscriptions").AbsoluteUri,
                Url = self.AbsoluteUri,
                Authscope = baseUri.AbsoluteUri,
                Authority = authority.AbsoluteUri
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
                service.Events = new Eventtypes();
                var resType = resource.Type.Split('/')[0].ToLowerInvariant();
                if (this.topicTypes.TryGetValue(resType, out var info))
                {
                    foreach (var typeInfo in info)
                    {
                        foreach (var eventType in typeInfo.Item2)
                        {
                            string sourcePattern = typeInfo.Item1.SourceResourceFormat;
                            sourcePattern = sourcePattern.Replace('<', '{').Replace('>', '}');

                            service.Events.Add(new Eventtype()
                            {
                                Type = eventType.Name,
                                Dataschema = eventType.SchemaUrl,
                                Sourcetemplate = new Uri(baseUri, sourcePattern).ToString(),
                                Description = eventType.Name
                            });
                        }
                    }
                }
            }

            return service;
        }

        static string NormalizeId(string id)
        {
            return id.Trim('/').Replace("/", "::");
        }


        async Task InitializeAsync()
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
                SubscriptionId = azureSubscriptionId
            };
            gridClient = new EventGridManagementClient(tokenCredentials)
            {
                SubscriptionId = azureSubscriptionId,
                LongRunningOperationRetryTimeout = 2
            };
            this.topicTypes = new Dictionary<string, List<Tuple<TopicTypeInfo, IEnumerable<EventType>>>>();

            // we can fetch those just once since they're (fairly) stable
            var systemTopics = EnumerateSystemTopics();
            foreach (var topic in systemTopics)
            {
                var eventTypes = await gridClient.TopicTypes.ListEventTypesAsync(topic.Name);
                var keys = topic.Name.Split('.');
                var key = $"{keys[0]}.{keys[1]}".ToLowerInvariant();

                if (this.topicTypes.TryGetValue(key, out var info))
                {
                    info.Add(new Tuple<TopicTypeInfo, IEnumerable<EventType>>(topic, eventTypes));
                }
                else
                {
                    this.topicTypes.Add(key, new List<Tuple<TopicTypeInfo, IEnumerable<EventType>>>()
                    {
                        new Tuple<TopicTypeInfo, IEnumerable<EventType>>(topic, eventTypes)
                    });
                }
            }
        }


        async IAsyncEnumerable<GenericResourceExpanded> EnumerateResourceGroupResourcesWithEventsAsync(string azureSubscriptionId, string azureResourceGroupName)
        {
            var resources = await resourceGroupClient.Resources.ListByResourceGroupAsync(
                azureResourceGroupName, 
                new Microsoft.Rest.Azure.OData.ODataQuery<GenericResourceFilter> { Expand = "createdTime,changedTime" });

            //// we will filter those down to resources that have a matching Event Grid provider 
            foreach (var resource in resources)
            {
                if (this.topicTypes.ContainsKey(resource.Type.Split('/')[0].ToLowerInvariant()))
                {
                    yield return resource;
                }
            }
        }

        IEnumerable<TopicTypeInfo> EnumerateSystemTopics()
        {
            return gridClient.TopicTypes.List();
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