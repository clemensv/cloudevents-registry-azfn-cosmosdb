using McMaster.Extensions.CommandLineUtils;
using Microsoft.VisualBasic.FileIO;
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
            var app = new CommandLineApplication();
            var keyOption = app.Option("--key <KEY>", "The API key for the Azure Functions instance", CommandOptionType.SingleValue);
            var baseUrlOption = app.Option("--baseUrl <URL>", "The base URL for the Azure Functions instance", CommandOptionType.SingleValue);
            app.OnExecuteAsync(async cancellationToken =>
            {
                await Run(keyOption, baseUrlOption);
            });
            app.Execute(args);

        }

        private static async Task Run(CommandOption keyOption, CommandOption baseUrlOption)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-functions-key", keyOption.Value());
            DiscoveryClient client = new DiscoveryClient(httpClient);
            client.BaseUrl = baseUrlOption.Value();


            Console.WriteLine($"----- Existing endpoints -----");
            try
            {
                var endpoints = await client.GetEndpointsAsync(null);
                foreach (var item in endpoints.Values)
                {
                    Console.WriteLine($"Existing: Id {item.Id}, Version {item.Version}");
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
                        {
                            $"Endpoint{i} Event1", new CloudEventDefinition
                            {
                                Id = $"Endpoint{i} Event1",
                                Description = $"This is event type Endpoint{i} Event1",
                                Metadata = new CloudEventMetadata {
                                    Attributes = new Attributes {
                                        Type = new MetadataPropertyString { Value = $"Endpoint{i}.Event1" }
                                    }
                                }
                            }
                        },
                        {
                            $"Endpoint{i} Event2", new CloudEventDefinition
                            {
                                Id = $"Endpoint{i} Event2",
                                Description = $"This is event type Endpoint{i} Event2",
                                Metadata = new CloudEventMetadata
                                {
                                    Attributes = new Attributes {
                                        Type = new MetadataPropertyString
                                        {
                                            Value = $"Endpoint{i}.Event2"
                                        }
                                    }
                                }
                            } }
                    },
                    Usage = EndpointUsage.Subscriber,
                    Config = new EndpointConfigSubscriber
                    {
                        Protocol = "http",
                        Endpoints = new[] {
                            new Uri("https://example.com/foo")
                        },
                    }
                };

                Endpoint createdEndpoint = null;
                try
                {

                    createdEndpoint = await client.PutEndpointAsync(endpoint, endpoint.Id);
                    Console.WriteLine($"Created: Id {createdEndpoint.Id}, Version {createdEndpoint.Version}");
                }
                catch (ApiException apiException)
                {
                    if (apiException.StatusCode != 409)
                    {
                        throw;
                    }
                    Console.WriteLine($"Conflict: Id {createdEndpoint?.Id}, Version {createdEndpoint?.Version}");
                }
            }

            Console.WriteLine($"----- Update endpoints -----");
            for (int i = 0; i < 10; i++)
            {
                var existingEndpoint = await client.GetEndpointAsync(i.ToString());
                existingEndpoint.Description = $"This is service {i} Update";
                existingEndpoint.Version += 1;
                await client.PutEndpointAsync(existingEndpoint, existingEndpoint.Id);
                Console.WriteLine($"Updated: Id {existingEndpoint.Id}, Version {existingEndpoint.Version}");
            }

            for (int i = 0; i < 10; i++)
            {
                var existingEndpoint = await client.GetEndpointAsync(i.ToString());

                await client.DeleteEndpointAsync(
                    existingEndpoint.Version, existingEndpoint.Id
                    );

                Console.WriteLine($"Deleted: Id {existingEndpoint.Id}, Version {existingEndpoint.Version}");
            }


            Console.WriteLine($"----- Existing Groups -----");
            try
            {
                var Groups = await client.GetGroupsAsync(null);
                foreach (var item in Groups.Values)
                {
                    Console.WriteLine($"Existing: Id {item.Id}, Version {item.Version}");
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
                            Id = $"Group{i} Event1",
                            Metadata = new CloudEventMetadata {
                                Attributes = new Attributes {
                                    Type = new MetadataPropertyString {
                                        Value = $"Group{i}.Event1",
                                    } }
                            }
                        } },
                        { $"Group{i} Event2", new CloudEventDefinition
                        {
                            Id = $"Group{i} Event2",
                            Metadata = new CloudEventMetadata
                            {
                                Attributes = new Attributes {
                                    Type = new MetadataPropertyString
                                    {
                                        Value = $"Group{i}.Event2"
                                    }
                                }
                            }
                        } }
                    }
                };

                Group createdGroup = null;
                try
                {

                    createdGroup = await client.PutGroupAsync(Group, Group.Id);
                    Console.WriteLine($"Created: Id {createdGroup.Id}, Version {createdGroup.Version}");
                }
                catch (ApiException apiException)
                {
                    if (apiException.StatusCode != 409)
                    {
                        throw;
                    }
                    Console.WriteLine($"Conflict: Id {createdGroup?.Id}, Version {createdGroup?.Version}");
                }
            }

            Console.WriteLine($"----- Update Groups -----");
            for (int i = 0; i < 10; i++)
            {
                var existingGroup = await client.GetGroupAsync(i.ToString());
                existingGroup.Description = $"This is service {i} Update";

                existingGroup.Version += 1;
                await client.PutGroupAsync(existingGroup, existingGroup.Id);
                Console.WriteLine($"Updated: Id {existingGroup.Id}, Version {existingGroup.Version}");
            }

            for (int i = 0; i < 10; i++)
            {
                var existingGroup = await client.GetGroupAsync(i.ToString());

                await client.DeleteGroupAsync(existingGroup.Id, existingGroup.Version);

                Console.WriteLine($"Deleted: Id {existingGroup.Id}, Version {existingGroup.Version}");
            }


            Console.WriteLine($"----- Existing SchemaGroups -----");
            try
            {
                var SchemaGroups = await client.GetSchemaGroupsAsync(null);
                foreach (var item in SchemaGroups.Values)
                {
                    Console.WriteLine($"Existing: Id {item.Id}, Version {item.Version}");
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

                    createdGroup = await client.PutSchemaGroupAsync(SchemaGroup, SchemaGroup.Id);
                    for (int j = 0; j < 10; j++)
                    {
                        Schema schema = new()
                        {
                            Id = $"schema{i}.{j}",
                            Description = $"schema{i}.{j}"
                        };

                        string schemaText = "This is a fake schema format";

                        await client.PostSchemaDocumentAsync(schema.Description, null, null, null, "text",
                            new MemoryStream(Encoding.UTF8.GetBytes(schemaText)),
                            SchemaGroup.Id, schema.Id); ;

                    }

                    Console.WriteLine($"Created: Id {createdGroup.Id}, Version {createdGroup.Version}");
                }
                catch (ApiException apiException)
                {
                    if (apiException.StatusCode != 409)
                    {
                        throw;
                    }
                    Console.WriteLine($"Conflict: Id {createdGroup?.Id}, Version {createdGroup?.Version}");
                }
            }

            Console.WriteLine($"----- Update SchemaGroups -----");
            for (int i = 0; i < 10; i++)
            {
                var existingGroup = await client.GetSchemaGroupAsync(i.ToString());
                existingGroup.Description = $"This is service {i} Update";
                existingGroup.Version += 1;
                await client.PutSchemaGroupAsync(existingGroup, existingGroup.Id);
                Console.WriteLine($"Updated: Id {existingGroup.Id}, Version {existingGroup.Version}");
            }

            for (int i = 0; i < 10; i++)
            {
                var existingGroup = await client.GetSchemaGroupAsync(i.ToString());

                await client.DeleteSchemaGroupAsync(existingGroup.Version, existingGroup.Id);

                Console.WriteLine($"Deleted: Id {existingGroup.Id}, Version {existingGroup.Version}");
            }
        }
    }
}
