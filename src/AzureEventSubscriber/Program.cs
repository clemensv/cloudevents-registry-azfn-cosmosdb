using System;

namespace AzureEventSubscriber
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Net.Mime;
    using System.Threading.Tasks;
    using xRegistry.Types.EndpointRegistry;
    using xRegistry.Types.Registry;
    using Azure.CloudEvents.Subscriptions;
    using Azure.Identity;
    using CloudNative.CloudEvents;
    using CloudNative.CloudEvents.NewtonsoftJson;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.Relay;
    using Newtonsoft.Json;

    class Program
    {
        static HybridConnectionListener hybridConnectionlistener;
        static JsonEventFormatter jev = new JsonEventFormatter();

        public static async Task<int> Main(string[] args)
            => await CommandLineApplication.ExecuteAsync<Program>(args);

        [Option(ShortName = "d", Description = "CloudEvents Registry endpoint."), Required]
        public string RegistryEndpoint { get; }

        [Option(ShortName = "c", Description = "Azure Relay namespace connection string."), Required]
        public string ConnectionString { get; } = null;

        [Option(ShortName = "r", Description = "Azure Relay name."), Required]
        public string RelayName { get; } = null;

        private async Task OnExecuteAsync()
        {


            RelayConnectionStringBuilder rsb = new RelayConnectionStringBuilder(ConnectionString);
            await StartEventListener(ConnectionString, RelayName);

            var httpClient = new HttpClient();
            var endpointRegistryClient = new EndpointRegistryClient(RegistryEndpoint, httpClient);
            var subscriptions = new Dictionary<string, Subscription>();

            var azureCredential = new DefaultAzureCredential();
            var token = await azureCredential.GetTokenAsync(new Azure.Core.TokenRequestContext(new string[] { "https://management.azure.com/.default" }));
            var subscriptionsHttpClient = new HttpClient();
            subscriptionsHttpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            var endpoints = await endpointRegistryClient.GetResourceGroupAllAsync(string.Empty);
            foreach (var service in endpoints.Values)
            {
                try
                {
                    var subscriptionClient = new SubscriptionsClient(subscriptionsHttpClient) { BaseUrl = service.Config?.Endpoints?.First()?.ToString() };
                    var types = new List<string>();
                    foreach (var serviceEvent in service.Definitions.Values)
                    {
                        if (serviceEvent is CloudEventDefinition)
                        {
                            if (((CloudEventDefinition)serviceEvent).Metadata.Attributes != null)
                            {
                                types.Add(((CloudEventDefinition)serviceEvent).Metadata.Attributes.Type.Value);
                            }
                        }
                    }

                    SubscriptionRequest subscriptionRequest = new SubscriptionRequest()
                    {
                        Protocol = Azure.CloudEvents.Subscriptions.Protocol.HTTP,
                        Sink = $"https://{rsb.Endpoint.Host}/{RelayName}",
                        Types = types
                    };

                    Console.WriteLine($"Subscribing for events from {service.Name} via {service.Config?.Endpoints?.First()?.ToString()}");
                    var subscription = await subscriptionClient.CreateSubscriptionAsync(subscriptionRequest);
                    subscriptions.Add(service.Config?.Endpoints?.First()?.ToString(), subscription);

                }
                catch (Exception e)
                {
                    Console.WriteLine($"Exception {e.Message} {service.Name}");
                }
            }

            Console.WriteLine("Press enter to clean up");
            Console.ReadLine();

            foreach (var subscription in subscriptions)
            {
                var subscriptionClient = new SubscriptionsClient(subscriptionsHttpClient) { BaseUrl = subscription.Key };
                await subscriptionClient.DeleteSubscriptionAsync(subscription.Value.Id);
            }

            await hybridConnectionlistener.CloseAsync();

        }

        static Task StartEventListener(string relayConnectionString, string hybridConnectionName)
        {

            hybridConnectionlistener = new HybridConnectionListener(relayConnectionString, hybridConnectionName)
            {
                RequestHandler = (context) =>
                {
                    if (context.Request.HttpMethod.Equals("options", StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.Headers["WebHook-Allowed-Origin"] = context.Request.Headers["WebHook-Request-Origin"];
                        context.Response.Headers["WebHook-Allowed-Rate"] = "120";
                        context.Response.StatusCode = System.Net.HttpStatusCode.OK;
                        context.Response.Close();
                    }
                    else
                    {
                        ProcessEventGridEvents(context);
                        context.Response.StatusCode = System.Net.HttpStatusCode.OK;
                        context.Response.Close();
                    }
                }
            };


            return hybridConnectionlistener.OpenAsync();
        }

        static void ProcessEventGridEvents(RelayedHttpListenerContext context)
        {
            ContentType contentType = new ContentType(context.Request.Headers["Content-Type"] as string ?? String.Empty);             
            var ce = jev.DecodeStructuredModeMessage(context.Request.InputStream, contentType, null);
            Console.WriteLine(ce.Type);
        }
    }
}
