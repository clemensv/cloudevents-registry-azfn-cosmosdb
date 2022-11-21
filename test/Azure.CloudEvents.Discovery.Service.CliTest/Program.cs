
using System;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;


namespace Azure.CloudEvents.Discovery
{
    class Program
    {
        static async Task Main(string[] args)
        {
            DiscoveryClient client = new DiscoveryClient(new HttpClient());
            client.BaseUrl = "http://localhost:11000/";
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
                var endpoint = new Endpoint()
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

                Endpoint createdEndpoint = null;
                try
                {

                    createdEndpoint = await client.PutEndpointAsync(endpoint, endpoint.Id);
                    Console.WriteLine($"Created: Id {createdEndpoint.Id}, Epoch {createdEndpoint.Epoch}");
                }
                catch (ApiException apiException)
                {
                    if (apiException.StatusCode != 409)
                    {
                        throw;
                    }
                    Console.WriteLine($"Conflict: Id {createdEndpoint?.Id}, Epoch {createdEndpoint?.Epoch}");
                }
            }

            Console.WriteLine($"----- Update endpoints -----");
            for (int i = 0; i < 10; i++)
            {
                var existingEndpoint = await client.GetEndpointAsync(i.ToString());
                existingEndpoint.Description = $"This is service {i} Update";

                bool correct = false;
                try
                {
                    await client.PutEndpointAsync(existingEndpoint, existingEndpoint.Id);
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
                existingEndpoint.Epoch += 1;
                await client.PutEndpointAsync(existingEndpoint, existingEndpoint.Id);
                Console.WriteLine($"Updated: Id {existingEndpoint.Id}, Epoch {existingEndpoint.Epoch}");
            }

            for (int i = 0; i < 10; i++)
            {
                var existingEndpoint = await client.GetEndpointAsync(i.ToString());

                await client.DeleteEndpointAsync(
                    existingEndpoint.Epoch, existingEndpoint.Id
                    );

                Console.WriteLine($"Deleted: Id {existingEndpoint.Id}, Epoch {existingEndpoint.Epoch}");
            }


            Console.WriteLine($"----- Existing Groups -----");
            try
            {
                var Groups = await client.GetGroupsAsync(null);
                foreach (var item in Groups.Values)
                {
                    Console.WriteLine($"Existing: Id {item.Id}, Epoch {item.Epoch}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


            Console.WriteLine($"----- Create Groups -----");
            for (int i = 0; i < 10; i++)
            {
                var Group = new Group()
                {
                    Id = i.ToString(),
                    Description = $"This is service {i}",
                    Definitions = new Definitions
                    {
                        { $"Group{i} Event1", new CloudEventDefinition
                        {
                            Description = $"Group{i} Event1",
                            Metadata = new CloudEventMetadata {
                                Type = new MetadataPropertyString {
                                    Value = $"Group{i}.Event1",
                                }
                            }
                        } },
                        { $"Group{i} Event2",new CloudEventDefinition
                        {
                            Description = $"Group{i} Event2",
                            Metadata = new CloudEventMetadata
                            {
                                Type = new MetadataPropertyString
                                {
                                    Value = $"Group{i}.Event2"
                                }
                            }
                        } }
                    }
                };

                Group createdGroup = null;
                try
                {

                    createdGroup = await client.PutGroupAsync(Group, Group.Id);
                    Console.WriteLine($"Created: Id {createdGroup.Id}, Epoch {createdGroup.Epoch}");
                }
                catch (ApiException apiException)
                {
                    if (apiException.StatusCode != 409)
                    {
                        throw;
                    }
                    Console.WriteLine($"Conflict: Id {createdGroup?.Id}, Epoch {createdGroup?.Epoch}");
                }
            }

            Console.WriteLine($"----- Update Groups -----");
            for (int i = 0; i < 10; i++)
            {
                var existingGroup = await client.GetGroupAsync(i.ToString());
                existingGroup.Description = $"This is service {i} Update";

                bool correct = false;
                try
                {
                    await client.PutGroupAsync(existingGroup, existingGroup.Id);
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
                existingGroup.Epoch += 1;
                await client.PutGroupAsync(existingGroup, existingGroup.Id);
                Console.WriteLine($"Updated: Id {existingGroup.Id}, Epoch {existingGroup.Epoch}");
            }

            for (int i = 0; i < 10; i++)
            {
                var existingGroup = await client.GetGroupAsync(i.ToString());

                await client.DeleteGroupAsync(existingGroup.Id, existingGroup.Epoch);

                Console.WriteLine($"Deleted: Id {existingGroup.Id}, Epoch {existingGroup.Epoch}");
            }


            Console.WriteLine($"----- Existing SchemaGroups -----");
            try
            {
                var SchemaGroups = await client.GetSchemaGroupsAsync(null);
                foreach (var item in SchemaGroups.Values)
                {
                    Console.WriteLine($"Existing: Id {item.Id}, Epoch {item.Epoch}");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


            Console.WriteLine($"----- Create SchemaGroups -----");
            for (int i = 0; i < 10; i++)
            {
                var SchemaGroup = new SchemaGroup()
                {
                    Id = i.ToString(),
                    Description = $"This is service {i}",
                   
                };

                SchemaGroup createdGroup = null;
                try
                {

                    createdGroup = await client.PutSchemagroupAsync(SchemaGroup, SchemaGroup.Id);
                    for (int j = 0; j < 10; j++)
                    {
                        Schema schema = new()
                        {
                            Id = $"Group{i} Event1",
                            Description = $"Group{i} Event1"
                        };

                        string schemaText = "This is a fake schema format";

                        await client.RegisterSchemaDocumentAsync("", "text", new FileParameter(new MemoryStream(Encoding.UTF8.GetBytes(schemaText)), "", "application/schema; format=text"), SchemaGroup.Id, schema.Id); ;

                    }
                    
                    Console.WriteLine($"Created: Id {createdGroup.Id}, Epoch {createdGroup.Epoch}");
                }
                catch (ApiException apiException)
                {
                    if (apiException.StatusCode != 409)
                    {
                        throw;
                    }
                    Console.WriteLine($"Conflict: Id {createdGroup?.Id}, Epoch {createdGroup?.Epoch}");
                }
            }

            Console.WriteLine($"----- Update SchemaGroups -----");
            for (int i = 0; i < 10; i++)
            {
                var existingGroup = await client.GetSchemagroupAsync(i.ToString());
                existingGroup.Description = $"This is service {i} Update";

                bool correct = false;
                try
                {
                    await client.PutSchemagroupAsync(existingGroup, existingGroup.Id);
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
                existingGroup.Epoch += 1;
                await client.PutSchemagroupAsync(existingGroup, existingGroup.Id);
                Console.WriteLine($"Updated: Id {existingGroup.Id}, Epoch {existingGroup.Epoch}");
            }

            for (int i = 0; i < 10; i++)
            {
                var existingGroup = await client.GetSchemagroupAsync(i.ToString());

                await client.DeleteSchemagroupAsync(existingGroup.Epoch, existingGroup.Id);

                Console.WriteLine($"Deleted: Id {existingGroup.Id}, Epoch {existingGroup.Epoch}");
            }

        }


    }
}
