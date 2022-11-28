using Azure.CloudEvents.Discovery;
using Azure.CloudEvents.Discovery.SystemTopicLoader;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net.Http;
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

            var httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(this.FunctionsKey))
            {
                httpClient.DefaultRequestHeaders.Add("x-functions-key", this.FunctionsKey);
            }
            DiscoveryClient client = new DiscoveryClient(httpClient);
            client.BaseUrl = this.DiscoveryEndpoint;

            await foreach (var group in rte.EnumerateSystemDefinitionGroups(new Uri(this.DiscoveryEndpoint)))
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

            await foreach (var endpoint in rte.EnumerateDiscoveryServicesAsync(new Uri(this.DiscoveryEndpoint), this.ResourceGroupName))
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
