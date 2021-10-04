
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
            client.BaseUrl = "http://localhost:7071/api";
            Console.WriteLine($"----- Existing services -----");
            try
            {
                var services = await client.GetServicesAsync(null);
                foreach (var item in services)
                {
                    Console.WriteLine($"Existing: Id { item.Id }, Epoch {item.Epoch}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);                 
            }
            

            Console.WriteLine($"----- Create services -----");
            for (int i = 0; i < 10; i++)
            {
                var service = new Service()
                {
                    Id = i.ToString(),
                    Description = $"This is service {i}",
                    Events = new Eventtypes
                    {
                        new Eventtype
                        {
                            Type = $"Service{i}.Event1",
                            Description = $"Service{i} Event1"
                        },
                        new Eventtype
                        {
                            Type = $"Service{i}.Event2",
                            Description = $"Service{i} Event1",
                        }
                    },
                    Protocols = new[] { "HTTP" },
                    Subscriptionurl = "https://example.com/foo"
                };

                Service createdService = null;
                try
                {

                    createdService = await client.PostServiceAsync(service.Id, service);
                    Console.WriteLine($"Created: Id { createdService.Id }, Epoch {createdService.Epoch}");
                }
                catch (ApiException apiException)
                {
                    if (apiException.StatusCode != 409)
                    {
                        throw;
                    }
                    Console.WriteLine($"Conflict: Id { createdService?.Id }, Epoch {createdService?.Epoch}");
                }
            }

            Console.WriteLine($"----- Update services -----");
            for (int i = 0; i < 10; i++)
            {
                var existingService = await client.GetServiceAsync(i.ToString());
                existingService.Description = $"This is service {i} Update";

                bool correct = false;
                try
                {
                    await client.PostServiceAsync(existingService.Id, existingService);
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
                await client.PostServiceAsync(existingService.Id, existingService);
                Console.WriteLine($"Updated: Id { existingService.Id }, Epoch {existingService.Epoch}");
            }

            for (int i = 0; i < 10; i++)
            {
                var existingService = await client.GetServiceAsync(i.ToString());

                await client.DeleteServiceAsync(
                    existingService.Id,
                    new Serviceinstance { Id = existingService.Id, Epoch = existingService.Epoch });

                Console.WriteLine($"Deleted: Id { existingService.Id }, Epoch {existingService.Epoch}");
            }
        }
    }
}
