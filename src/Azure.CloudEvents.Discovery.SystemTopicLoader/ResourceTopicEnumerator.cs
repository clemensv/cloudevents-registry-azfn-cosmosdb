// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace Azure.CloudEvents.Discovery.SystemTopicLoader
{
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using Azure.CloudEvents.Discovery;
    using Microsoft.Azure.Management.EventGrid;
    using Microsoft.Azure.Management.EventGrid.Models;
    using Microsoft.Azure.Management.ResourceManager;
    using Microsoft.Azure.Management.ResourceManager.Models;
    using Microsoft.Rest;
    using Microsoft.Rest.Azure;

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

        public async IAsyncEnumerable<Endpoint> EnumerateDiscoveryServicesAsync(Uri referencesBaseUri, string azureResourceGroupName)
        {
            await InitializeAsync();
            await foreach (var resource in EnumerateResourceGroupResourcesWithEventsAsync(azureSubscriptionId, azureResourceGroupName))
            {
                if (resource.Type.StartsWith("Microsoft.EventGrid", StringComparison.OrdinalIgnoreCase))
                    continue;

                yield return MapGridTopicToDiscoveryServiceObject(referencesBaseUri, resource);
            }
        }

        public async IAsyncEnumerable<Group> EnumerateSystemDefinitionGroups(Uri baseUri)
        {
            var authority = new Uri(baseUri, "groups/");

            await InitializeAsync();
            foreach (var info in this.topicTypes)
            {
                Group group = new Group()
                {
                    Id = info.Key,
                    Version = DateTime.UtcNow.ToFileTimeUtc(),
                    Definitions = new Definitions(),
                    Self = new Uri(authority, "./" + info.Key),
                    Origin = "https://" + authority.Host
                };

                foreach (var typeInfo in info.Value)
                {
                    foreach (var eventType in typeInfo.Item2)
                    {
                        if (group.Definitions.ContainsKey(eventType.Name))
                            continue;

                        string sourcePattern = typeInfo.Item1.SourceResourceFormat;
                        sourcePattern = sourcePattern?.Replace('<', '{')?.Replace('>', '}');

                        Uri schemaUrl;
                        if (!Uri.TryCreate(eventType.SchemaUrl, new UriCreationOptions(), out schemaUrl))
                        {
                            schemaUrl = null;
                        }
                        group.Definitions.Add(eventType.Name, new CloudEventDefinition()
                        {
                            Metadata = new CloudEventMetadata
                            {
                                Attributes = new Attributes
                                {

                                    Id = new MetadataPropertyString
                                    {
                                        Required = true
                                    },
                                    Type = new MetadataPropertyString
                                    {
                                        Value = eventType.Name,
                                        Required = true,
                                    },
                                    Time = new MetadataPropertyDateTime
                                    {
                                        Required = true,
                                    },
                                    Datacontenttype = new MetadataPropertySymbol
                                    {
                                        Value = "application/json",
                                        Required = true
                                    },
                                    Dataschema = new MetadataPropertyUriTemplate
                                    {
                                        Value = schemaUrl?.ToString(),
                                        Required = true,
                                    },
                                    Source = new MetadataPropertyUriTemplate
                                    {
                                        Value = sourcePattern
                                    }
                                }
                            },
                            Schemaurl = schemaUrl,
                            Description = eventType.Description,
                            Self = new Uri(authority, "./" + info.Key + "/definitions/" + eventType.Name),
                            Origin = "https://" + authority.Host,
                            Id = eventType.Name,
                        });
                    }
                }
                yield return group;
            }
        }

        Endpoint MapGridTopicToDiscoveryServiceObject(Uri baseUri, GenericResourceExpanded resource)
        {
            string normalizedId = NormalizeId(resource.Id);
            var authority = new Uri(baseUri, "endpoints/");
            var self = new Uri(authority, "./" + normalizedId);
            var endpoint = new Endpoint()
            {
                Id = normalizedId,
                Name = resource.Name,
                Description = $"{resource.Name} {resource.Kind}",
                Docs = GetDocsUrl(resource.Type),
                Version = (resource.ChangedTime.HasValue ? resource.ChangedTime?.Ticks : resource.CreatedTime?.Ticks) ?? 0,
                Usage = EndpointUsage.Subscriber,
                Config = new EndpointConfigSubscriber
                {
                    Protocol = "HTTP",
                    Endpoints = new[] { new Uri(self, "subscriptions") },
                },
                Self = self,
                Authscope = baseUri.AbsoluteUri,
                Origin = "https://" + authority.Host
            };

            if (resource.ChangedTime.HasValue)
            {
                endpoint.ModifiedOn = resource.ChangedTime.Value;
            }

            if (resource.CreatedTime.HasValue)
            {
                endpoint.CreatedOn = resource.ChangedTime.Value;
            }

            if (!string.IsNullOrEmpty(resource.ProvisioningState))
            {
                endpoint.AdditionalProperties.Add("azresprovisioningstate", resource.ProvisioningState);
            }

            if (!string.IsNullOrEmpty(resource.Kind))
            {
                endpoint.AdditionalProperties.Add("azreskind", resource.Kind);
            }

            if (!string.IsNullOrEmpty(resource.Id))
            {
                endpoint.AdditionalProperties.Add("azresid", resource.Id);
            }

            if (!string.IsNullOrEmpty(resource.Location))
            {
                endpoint.AdditionalProperties.Add("azreslocation", resource.Location);
            }

            if (!string.IsNullOrEmpty(resource.Type))
            {
                endpoint.AdditionalProperties.Add("azrestype", resource.Type);
                var resType = resource.Type.Split('/')[0].ToLowerInvariant();
                endpoint.Groups = new GroupUriReferences();
                endpoint.Groups.Add(new Uri(baseUri, "./groups/"+ resType));
            }
            return endpoint;
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

        Uri GetDocsUrl(string resourceType)
        {
            string provider = resourceType.Split('/')[0].ToLowerInvariant();

            switch (provider)
            {
                case "microsoft.storage":
                    return new Uri("https://docs.microsoft.com/azure/event-grid/event-schema-blob-storage");
                case "microsoft.appconfiguration":
                    return new Uri("https://docs.microsoft.com/azure/event-grid/event-schema-app-configuration");
                case "microsoft.web":
                    return new Uri("https://docs.microsoft.com/azure/event-grid/event-schema-app-service");
                case "microsoft.communication":
                    return new Uri("https://docs.microsoft.com/azure/event-grid/event-schema-communication-endpoints");
                case "microsoft.containerregistry":
                    return new Uri("https://docs.microsoft.com/azure/event-grid/event-schema-container-registry");
                case "microsoft.eventhub":
                    return new Uri("https://docs.microsoft.com/azure/event-grid/event-schema-event-hubs");
                case "microsoft.devices":
                    return new Uri("https://docs.microsoft.com/azure/event-grid/event-schema-iot-hub");
                case "microsoft.keyvault":
                    return new Uri("https://docs.microsoft.com/azure/event-grid/event-schema-key-vault");
                case "microsoft.machinelearningservice":
                    return new Uri("https://docs.microsoft.com/azure/event-grid/event-schema-machine-learning");
                case "microsoft.maps":
                    return new Uri("https://docs.microsoft.com/azure/event-grid/event-schema-azure-maps");
                case "microsoft.media":
                    return new Uri("https://docs.microsoft.com/azure/event-grid/event-schema-media-endpoints");
                case "microsoft.resources":
                    return new Uri("https://docs.microsoft.com/azure/event-grid/event-schema-resource-groups");
                case "microsoft.servicebus":
                    return new Uri("https://docs.microsoft.com/azure/event-grid/event-schema-service-bus");
                case "microsoft.signalrservice":
                    return new Uri("https://docs.microsoft.com/azure/event-grid/event-schema-azure-signalr");
            }

            return null;
        }


    }
}