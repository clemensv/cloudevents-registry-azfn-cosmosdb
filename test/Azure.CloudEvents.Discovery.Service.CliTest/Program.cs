
using System;
using System.Net.Http;
using System.Threading.Tasks;


namespace Azure.CloudEvents.Discovery
{
    class Program
    {
        static async Task Main(string[] args)
        {
            DiscoveryClient client = new DiscoveryClient(new HttpClient());
            client.BaseUrl = "http://localhost:7071/";
            Console.WriteLine($"----- Existing endpoints -----");
            try
            {
                var endpoints = await client.GetEndpointsAsync(null);
                foreach (var item in endpoints.Values)
                {
                    Console.WriteLine($"Existing: Id {item.Id}, Epoch {item.Epoch}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


            Console.WriteLine($"----- Create endpoints -----");
            for (int i = 0; i < 10; i++)
            {
                var service = new Endpoint()
                {
                    Id = i.ToString(),
                    Description = $"This is service {i}",
                    Definitions = new Definitions
                    {
                        { $"Endpoint{i} Event1", new CloudEventDefinition
                        {
                            Description = $"Endpoint{i} Event1",
                            Metadata = new CloudEventMetadata {
                                Type = new MetadataPropertyString {
                                    Value = $"Endpoint{i}.Event1",
                                }
                            }
                        } },
                        { $"Endpoint{i} Event2",new CloudEventDefinition
                        {
                            Description = $"Endpoint{i} Event2",
                            Metadata = new CloudEventMetadata
                            {
                                Type = new MetadataPropertyString
                                {
                                    Value = $"Endpoint{i}.Event2"
                                }
                            }
                        } }
                    },
                    Usage = Usage.Subscriber,
                    Config = new EndpointConfigSubscriber
                    {
                        Protocol = "HTTP",
                        Endpoints = new[] { new Uri("https://example.com/foo") },
                   }
                };

                Endpoint createdService = null;
                try
                {

                    //createdService = await client.PostEndpointAsync(service);
                    Console.WriteLine($"Created: Id {createdService.Id}, Epoch {createdService.Epoch}");
                }
                catch (ApiException apiException)
                {
                    if (apiException.StatusCode != 409)
                    {
                        throw;
                    }
                    Console.WriteLine($"Conflict: Id {createdService?.Id}, Epoch {createdService?.Epoch}");
                }
            }

            Console.WriteLine($"----- Update endpoints -----");
            for (int i = 0; i < 10; i++)
            {
                var existingService = await client.GetEndpointAsync(i.ToString());
                existingService.Description = $"This is service {i} Update";

                bool correct = false;
                try
                {
                    await client.PutEndpointAsync(existingService, existingService.Id);
                    throw new InvalidOperationException("Must not get here because we did not update the Eppch");
                }
                catch (ApiException apiException)
                {
                    if (apiException.StatusCode == 409)
                    {
                        correct = true;
                    }
                }
                if (!correct)
                {
                    throw new Exception("Epoch validation failed");
                }
                existingService.Epoch += 1;
                await client.PutEndpointAsync(existingService, existingService.Id );
                Console.WriteLine($"Updated: Id {existingService.Id}, Epoch {existingService.Epoch}");
            }

            for (int i = 0; i < 10; i++)
            {
                var existingService = await client.GetEndpointAsync(i.ToString());

                await client.DeleteEndpointAsync(
                    existingService.Epoch, existingService.Id
                    );

                Console.WriteLine($"Deleted: Id {existingService.Id}, Epoch {existingService.Epoch}");
            }
        }
    }
}
