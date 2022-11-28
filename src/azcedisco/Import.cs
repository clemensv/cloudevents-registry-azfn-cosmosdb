﻿using Azure.CloudEvents.Discovery;
using Azure.CloudEvents.Discovery.SystemTopicLoader;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace azcedisco
{
    [Command()]
    class Import : CommonOptions
    {
        public virtual async Task<int> OnExecuteAsync(CommandLineApplication app)
        {
            var cred = new AzureIdentityCredentialAdapter();
            var rte = new ResourceTopicEnumerator(this.SubscriptionId, cred);

            DiscoveryClient client = new DiscoveryClient(new System.Net.Http.HttpClient());
            client.BaseUrl = this.DiscoveryEndpoint;

            await foreach (var group in rte.EnumerateSystemDefinitionGroups(new Uri("https://discovery.azure.com/")))
            {
                group.Version = DateTime.UtcNow.ToFileTimeUtc();
                Group createdGroup = null;
                try
                {
                    createdGroup = await client.PutGroupAsync(group, group.Id);
                }
                catch (ApiException apiException)
                {
                    if (apiException.StatusCode != 409)
                    {
                        Console.WriteLine(apiException.Message);
                    }
                }

                Console.WriteLine(JsonConvert.SerializeObject(group, Formatting.Indented));
            }

            await foreach (var endpoint in rte.EnumerateDiscoveryServicesAsync(new Uri("https://discovery.azure.com/"), this.ResourceGroupName))
            {
                endpoint.Version = DateTime.UtcNow.ToFileTimeUtc();
                Endpoint createdService = null;
                try
                {
                    createdService = await client.PutEndpointAsync(endpoint, endpoint.Id);
                }
                catch (ApiException apiException)
                {
                    if (apiException.StatusCode != 409)
                    {
                        Console.WriteLine(apiException.Message);
                    }
                }

                Console.WriteLine(JsonConvert.SerializeObject(endpoint, Formatting.Indented));
            }
            return 0;
        }
    }
}
