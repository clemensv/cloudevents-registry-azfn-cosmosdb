using System;

namespace ce_disco
{
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Text;
    using System.Threading.Tasks;
    using CloudNative.CloudEvents;
    using McMaster.Extensions.CommandLineUtils;
    using Microsoft.Azure.EventGrid.CloudEventsApiBridge;
    using Microsoft.Azure.EventGrid.CloudEventsApis.Discovery;
    using Microsoft.Azure.EventGrid.CloudEventsApis.Subscriptions;
    using Microsoft.Azure.Relay;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;
    using Newtonsoft.Json;
    using Newtonsoft.Json.Linq;

    class Program
    {
        static HybridConnectionListener hybridConnectionlistener;
        static JsonEventFormatter jev = new JsonEventFormatter();

        public static async Task<int> Main(string[] args)
            => await CommandLineApplication.ExecuteAsync<Program>(args);

        [Option(ShortName = "d", Description = "CloudEvents discovery endpoint."), Required]
        public string DiscoveryEndpoint { get; }

        [Option(ShortName = "c", Description = "Azure Relay namespace connection string."), Required]
        public string ConnectionString { get; } = null;

        [Option(ShortName = "r", Description = "Azure Relay name."), Required]
        public string RelayName { get; } = null;

        private async Task OnExecuteAsync()
        {

            RelayConnectionStringBuilder rsb = new RelayConnectionStringBuilder(ConnectionString);
            await StartEventListener(ConnectionString, RelayName);

            var httpClient = new HttpClient();
            var discoveryClient = new DiscoveryClient(httpClient) { BaseUrl = DiscoveryEndpoint };
            var subscriptions = new Dictionary<string, Subscription>();

            var services = await discoveryClient.GetServicesAsync(string.Empty);
            foreach (var service in services)
            {
                try
                {
                    var subscriptionClient = new SubscriptionsClient(new HttpClient()) { BaseUrl = service.Subscriptionurl };
                    var types = new List<string>();
                    foreach (var serviceEvent in service.Events)
                    {
                        types.Add(serviceEvent.Type);
                    }

                    SubscriptionRequest subscriptionRequest = new SubscriptionRequest()
                    {
                        Protocol = Protocol.HTTP,
                        Sink = $"https://{rsb.Endpoint.Host}/{RelayName}",
                        Types = types
                    };
                    
                    Console.WriteLine($"Subscribing for events from {service.Name} via {service.Subscriptionurl}");
                    var subscription = await subscriptionClient.CreateSubscriptionAsync(subscriptionRequest);
                    subscriptions.Add(service.Subscriptionurl, subscription);

                }
                catch( Exception e)
                {
                    Console.WriteLine($"Exception {e.Message} {service.Name}");
                }
            }
            
            Console.WriteLine("Press enter to clean up");
            Console.ReadLine();

            foreach (var subscription in subscriptions)
            {
                var subscriptionClient = new SubscriptionsClient(httpClient) { BaseUrl = subscription.Key };
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
            var ce = jev.DecodeStructuredEvent(context.Request.InputStream);
            Console.WriteLine(ce.Type);
        }
    }
}
