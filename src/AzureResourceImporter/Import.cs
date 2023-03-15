using Azure.CloudEvents.EndpointRegistry;
using Azure.CloudEvents.MessageDefinitionsRegistry;
using Azure.CloudEvents.Registry;
using Azure.CloudEvents.Registry.SystemTopicLoader;
using Azure.CloudEvents.SchemaRegistry;
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
            if (!this.RegistryEndpoint.EndsWith("/"))
            {
                this.RegistryEndpoint += "/";
            }

            var httpClient = new HttpClient();
            if (!string.IsNullOrEmpty(this.FunctionsKey))
            {
                httpClient.DefaultRequestHeaders.Add("x-functions-key", this.FunctionsKey);
            }
            MessageDefinitionsRegistryClient client = new MessageDefinitionsRegistryClient(RegistryEndpoint, httpClient);
            await foreach (var group in rte.EnumerateSystemDefinitionGroups(new Uri(this.RegistryEndpoint)))
            {
                group.Version = DateTime.UtcNow.ToFileTimeUtc();
                IResource createdGroup = null;
                try
                {
                    createdGroup = await client.PutResourceGroupAsync(group, group.Id);
                }
                catch (Azure.CloudEvents.MessageDefinitionsRegistry.ApiException apiException)
                {
                    if (apiException.StatusCode != 409)
                    {
                        Console.WriteLine(apiException.Message);
                    }
                }

                Console.WriteLine(JsonConvert.SerializeObject(group, Formatting.Indented));
            }
            SchemaRegistryClient client2 = new SchemaRegistryClient(RegistryEndpoint, httpClient);
            await foreach (var group in rte.EnumerateSystemDefinitionSchemaGroups(new Uri(this.RegistryEndpoint)))
            {
                group.Version = DateTime.UtcNow.ToFileTimeUtc();
                IResource createdGroup = null;
                try
                {
                    createdGroup = await client2.PutResourceGroupAsync(group, group.Id);
                }
                catch (Azure.CloudEvents.SchemaRegistry.ApiException apiException)
                {
                    if (apiException.StatusCode != 409)
                    {
                        Console.WriteLine(apiException.Message);
                    }
                }

                Console.WriteLine(JsonConvert.SerializeObject(group, Formatting.Indented));
            }
            EndpointRegistryClient client3 = new EndpointRegistryClient(RegistryEndpoint, httpClient);
            await foreach (var endpoint in rte.EnumerateRegistryServicesAsync(new Uri(this.RegistryEndpoint), this.ResourceGroupName))
            {
                endpoint.Version = DateTime.UtcNow.ToFileTimeUtc();
                IResource createdService = null;
                try
                {
                    createdService = await client3.PutResourceGroupAsync(endpoint, endpoint.Id);
                }
                catch (Azure.CloudEvents.EndpointRegistry.ApiException apiException)
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
