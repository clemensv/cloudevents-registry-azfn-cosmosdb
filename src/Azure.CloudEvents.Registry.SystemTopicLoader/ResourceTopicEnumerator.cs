// Copyright (c) Cloud Native Foundation.
// Licensed under the Apache 2.0 license.
// See LICENSE file in the project root for full license information.

namespace Azure.CloudEvents.Registry.SystemTopicLoader
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.Management.EventGrid;
    using Microsoft.Azure.Management.EventGrid.Models;
    using Microsoft.Azure.Management.ResourceManager;
    using Microsoft.Azure.Management.ResourceManager.Models;
    using Microsoft.Rest;
    using Microsoft.Rest.Azure;
    using xRegistry.Types.MessageDefinitionsRegistry;
    using xRegistry.Types.SchemaRegistry;
    using static System.Net.Mime.MediaTypeNames;
    
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

        public async IAsyncEnumerable<xRegistry.Types.EndpointRegistry.Endpoint> EnumerateRegistryServicesAsync(Uri referencesBaseUri, string azureResourceGroupName)
        {
            await InitializeAsync();
            await foreach (var resource in EnumerateResourceGroupResourcesWithEventsAsync(azureSubscriptionId, azureResourceGroupName))
            {
                if (resource.Type.StartsWith("Microsoft.EventGrid", StringComparison.OrdinalIgnoreCase))
                    continue;

                yield return MapGridTopicToRegistryServiceObject(referencesBaseUri, resource);
            }
        }

        public async IAsyncEnumerable<DefinitionGroup> EnumerateSystemDefinitionGroups(Uri baseUri)
        {
            var authority = new Uri(baseUri, "groups/");

            await InitializeAsync();
            foreach (var info in this.topicTypes)
            {
                DateTime utcNow = DateTime.UtcNow;
                DefinitionGroup group = new DefinitionGroup()
                {
                    Id = info.Key,
                    Version = utcNow.ToFileTimeUtc(),
                    Format = DefinitionGroupBaseFormat.CloudEvents_1_0,
                    Definitions = new Dictionary<string, Definition>(),
                    Self = new Uri(authority, "./" + info.Key).ToString(),
                    Origin = "https://" + authority.Host,
                    CreatedOn = utcNow,
                    ModifiedOn = utcNow
                };

                foreach (var typeInfo in info.Value)
                {
                    foreach (var eventType in typeInfo.Item2)
                    {
                        if (group.Definitions.ContainsKey(eventType.Name))
                            continue;

                        string sourcePattern = typeInfo.Item1.SourceResourceFormat;
                        sourcePattern = sourcePattern?.Replace('<', '{')?.Replace('>', '}');


                        CloudEventDefinition ceDef = new CloudEventDefinition()
                        {
                            Format = CloudEventDefinitionFormat.CloudEvents_1_0,
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
                                    Time = new MetadataPropertyTimeStamp
                                    {
                                        Required = true,
                                    },
                                    Datacontenttype = new MetadataPropertySymbol
                                    {
                                        Value = "application/json",
                                        Required = true
                                    },
                                    Source = new MetadataPropertyUriTemplate
                                    {
                                        Value = sourcePattern
                                    }
                                }
                            },
                            Version = utcNow.ToFileTimeUtc(),
                            Description = eventType.Description,
                            Self = new Uri(authority, "./" + info.Key + "/definitions/" + eventType.Name).ToString(),
                            Origin = "https://" + authority.Host,
                            Id = eventType.Name,
                            CreatedOn = utcNow,
                            ModifiedOn = utcNow
                        };
                        if (!string.IsNullOrEmpty(eventType.SchemaUrl) && Uri.IsWellFormedUriString(eventType.SchemaUrl, UriKind.Absolute))
                        {
                            string pattern = "https://github\\.com/(.*?)/blob/(.*?)/(.*)";
                            string replacement = "https://raw.githubusercontent.com/$1/$2/$3";
                            eventType.SchemaUrl = System.Text.RegularExpressions.Regex.Replace(eventType.SchemaUrl, pattern, replacement);

                            var evsu = new Uri(eventType.SchemaUrl);
                            if (!downloaded.TryGetValue(evsu, out var schemaText))
                            {
                                using (var client = new HttpClient())
                                {
                                    var response = await client.GetAsync(evsu);
                                    schemaText = await response.Content.ReadAsStringAsync();
                                    downloaded.Add(evsu, schemaText);
                                }
                            }
                            Uri schemaUrl = null;
                            Match match = Regex.Match(schemaText, $"([A-Za-z0-9_]*{(eventType.Name+"EventData").Split(".").Last()})", RegexOptions.IgnoreCase);
                            if (match.Success)
                            {
                                schemaUrl = new Uri(baseUri, $"schemagroups/{info.Key}/schemas/{eventType.Name}EventData#/definitions/{match.Groups[0].Value}");
                            }
                            else 
                            {
                                schemaUrl = new Uri(baseUri, $"schemagroups/{info.Key}/schemas/{eventType.Name}EventData");
                            }
                            
                            ceDef.SchemaUrl = schemaUrl.ToString();
                            ceDef.Metadata.Attributes.Dataschema = new MetadataPropertyUriTemplate
                            {
                                Value = schemaUrl.ToString(),
                                Required = true,
                            };
                        }
                        group.Definitions.Add(eventType.Name, ceDef);
                    }
                }
                yield return group;
            }
        }

        Dictionary<Uri, string> downloaded = new Dictionary<Uri, string>();
        public async IAsyncEnumerable<xRegistry.Types.SchemaRegistry.SchemaGroup> EnumerateSystemDefinitionSchemaGroups(Uri baseUri)
        {
            var authority = new Uri(baseUri, "schemagroups/");

            await InitializeAsync();
            foreach (var info in this.topicTypes)
            {
                DateTime utcNow = DateTime.UtcNow;
                SchemaGroup group = new SchemaGroup()
                {
                    Id = info.Key,
                    Version = utcNow.ToFileTimeUtc(),
                    Schemas = new Dictionary<string, Schema>(),
                    Self = new Uri(authority, "./" + info.Key).ToString(),
                    Origin = "https://" + authority.Host,
                    CreatedOn = utcNow,
                    ModifiedOn = utcNow
                };

                foreach (var typeInfo in info.Value)
                {
                    foreach (var eventType in typeInfo.Item2)
                    {
                        string eventDataName = eventType.Name + "EventData";
                        if (group.Schemas.ContainsKey(eventDataName))
                            continue;

                        Uri schemaUrl;
                        if (!Uri.TryCreate(eventType.SchemaUrl, new UriCreationOptions(), out schemaUrl))
                        {
                            continue;
                        }

                        if (string.IsNullOrEmpty(schemaUrl.Fragment))
                        {
                            if ( !downloaded.TryGetValue(schemaUrl, out var schemaText))
                            {
                                using (var client = new HttpClient())
                                {
                                    var response = await client.GetAsync(schemaUrl);
                                    schemaText = await response.Content.ReadAsStringAsync();
                                    downloaded.Add(schemaUrl, schemaText);
                                }
                            }
                            Match match = Regex.Match(schemaText, $"([A-Za-z0-9_]*{eventDataName.Split(".").Last()})");
                            if (match.Success)
                            {
                                schemaUrl = new UriBuilder(schemaUrl) { Fragment = $"/definitions/{match.Groups[0].Value}" }.Uri;
                            }
                        }

                        var version = utcNow.ToFileTimeUtc();
                        var versionString = version.ToString();
                        
                        group.Schemas.Add(eventDataName, new Schema()
                        {
                            Id = eventDataName,
                            Version = version,
                            Versions = new Dictionary<string, SchemaVersion>()
                            {
                                { versionString, new SchemaVersion()
                                    {
                                        Id = versionString,
                                        Version = version,
                                        SchemaUrl = schemaUrl,
                                        Self = new Uri(authority, "./" + info.Key + "/schemas/" + eventType.Name + "EventData/versions/1.0").ToString(),
                                        Origin = "https://" + authority.Host,
                                        CreatedOn = utcNow,
                                        ModifiedOn = utcNow
                                    }
                                }
                            },
                            Self = new Uri(authority, "./" + info.Key + "/schemas/" + eventType.Name + "EventData").ToString(),
                            Origin = "https://" + authority.Host,
                            CreatedOn = utcNow,
                            ModifiedOn = utcNow
                        });
                    }
                }
                yield return group;
            }
        }

        xRegistry.Types.EndpointRegistry.Endpoint MapGridTopicToRegistryServiceObject(Uri baseUri, GenericResourceExpanded resource)
        {
            string normalizedId = NormalizeId(resource.Id);
            var authority = new Uri(baseUri, "endpoints/");
            var self = new Uri(authority, "./" + normalizedId);
            DateTime utcNow = DateTime.UtcNow;
            var endpoint = new xRegistry.Types.EndpointRegistry.Endpoint()
            {
                Id = normalizedId,
                Name = resource.Name,
                Description = $"{resource.Name} {resource.Kind}",
                Docs = GetDocsUrl(resource.Type),
                Version = utcNow.ToFileTimeUtc(),
                Usage = xRegistry.Types.EndpointRegistry.EndpointUsage.Subscriber,
                Config = new xRegistry.Types.EndpointRegistry.EndpointConfigSubscriber
                {
                    Protocol = xRegistry.Types.EndpointRegistry.EndpointConfigBaseProtocol.HTTP,
                    Endpoints = new[] { new Uri(baseUri, "subscriptions") },
                },
                Self = self?.ToString(),
                Authscope = baseUri.AbsoluteUri,
                Origin = "https://" + authority.Host,
                CreatedBy = "CloudEvents Generator",
                ModifiedOn = DateTime.UtcNow,
                CreatedOn = DateTime.UtcNow                
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
                endpoint.DefinitionGroups = new List<string>();
                endpoint.DefinitionGroups.Add(new Uri(baseUri, "./groups/"+ resType).ToString());
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