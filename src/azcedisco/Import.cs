using Azure.CloudEvents.Discovery;
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

            await foreach (var service in rte.EnumerateDiscoveryServicesAsync(new Uri(this.DiscoveryEndpoint), this.ResourceGroupName))
            {
                Service createdService = null;
                try
                {
                    createdService = await client.PostServiceAsync(service.Id, service);
                }
                catch (ApiException apiException)
                {
                    if (apiException.StatusCode != 409)
                    {
                        Console.WriteLine(apiException.Message);
                    }
                }

                Console.WriteLine(JsonConvert.SerializeObject(service, Formatting.Indented));
            }
            return 0;
        }
    }
}
