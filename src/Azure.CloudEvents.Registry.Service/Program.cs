using Azure.CloudEvents.EventGridBridge;
using Azure.Core.Serialization;
using Azure.Messaging.EventGrid;
using Azure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;
using System.Threading.Tasks;

namespace Azure.CloudEvents.Registry
{
    public class Program
    {
        public static void Main()
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWorkerDefaults((b) =>
                {
                    b.UseNewtonsoftJson();
                }                            
                )
                .ConfigureServices(ConfigureServices)
                .Build();

            host.Run();
        }

        private static void ConfigureServices(HostBuilderContext hostBuilder, IServiceCollection endpoints)
        {


            endpoints.AddSingleton((s) =>
            {
                string endpoint = hostBuilder.Configuration["COSMOSDB_ENDPOINT"];
                if (string.IsNullOrEmpty(endpoint))
                {
                    throw new ArgumentNullException("Please specify a valid endpoint in the appSettings.json file or your Azure Functions Settings.");
                }

                string authKey = hostBuilder.Configuration["COSMOSDB_KEY"];
                if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
                {
                    throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json file or your Azure Functions Settings.");
                }

                CosmosClientBuilder configurationBuilder = new CosmosClientBuilder(endpoint, authKey);
                return configurationBuilder.Build();
            });

            endpoints.AddSingleton((s) =>
            {
                string endpoint = hostBuilder.Configuration["EVENTGRID_ENDPOINT"];
                if (string.IsNullOrEmpty(endpoint))
                {
                    throw new ArgumentNullException("Please specify a valid endpoint in the appSettings.json file or your Azure Functions Settings.");
                }

                string authKey = hostBuilder.Configuration["EVENTGRID_KEY"];
                if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
                {
                    throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json file or your Azure Functions Settings.");
                }

                return new EventGridPublisherClient(new Uri(endpoint), new AzureKeyCredential(authKey));
            });

            endpoints.AddSingleton((s) =>
            {
                string endpoint = hostBuilder.Configuration["BLOB_ENDPOINT"];
                if (string.IsNullOrEmpty(endpoint))
                {
                    throw new ArgumentNullException("Please specify a valid endpoint in the appSettings.json file or your Azure Functions Settings.");
                }

                string authName = hostBuilder.Configuration["BLOB_ACCOUNT"];
                if (string.IsNullOrEmpty(authName) || string.Equals(authName, "Account name"))
                {
                    throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json file or your Azure Functions Settings.");
                }

                string authKey = hostBuilder.Configuration["BLOB_KEY"];
                if (string.IsNullOrEmpty(authKey) || string.Equals(authKey, "Super secret key"))
                {
                    throw new ArgumentException("Please specify a valid AuthorizationKey in the appSettings.json file or your Azure Functions Settings.");
                }

                return new BlobServiceClient(new Uri(endpoint), new StorageSharedKeyCredential(authName, authKey));
            });

            endpoints.AddSingleton<SubscriptionProxy>((s) =>
            {
                return new SubscriptionProxy();
            });
        }
    }

    internal static class WorkerConfigurationExtensions
    {
        
        /// <summary>
        /// The functions worker uses the Azure SDK's ObjectSerializer to abstract away all JSON serialization. This allows you to
        /// swap out the default System.Text.Json implementation for the Newtonsoft.Json implementation.
        /// To do so, add the Microsoft.Azure.Core.NewtonsoftJson nuget package and then update the WorkerOptions.Serializer property.
        /// This method updates the Serializer to use Newtonsoft.Json. Call /api/HttpFunction to see the changes.
        /// </summary>
        public static IFunctionsWorkerApplicationBuilder UseNewtonsoftJson(this IFunctionsWorkerApplicationBuilder builder)
        {
            builder.Services.Configure<WorkerOptions>(workerOptions =>
            {
                var settings = NewtonsoftJsonObjectSerializer.CreateJsonSerializerSettings();
                settings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                settings.NullValueHandling = NullValueHandling.Ignore;

                workerOptions.Serializer = new NewtonsoftJsonObjectSerializer(settings);
            });

            return builder;
        }
    }
}