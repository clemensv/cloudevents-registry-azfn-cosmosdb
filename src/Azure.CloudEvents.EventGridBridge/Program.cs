using Azure.CloudEvents.EventGridBridge;
using Azure.Core.Serialization;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System;

namespace Azure.CloudEvents.EventGridBridge
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

        private static void ConfigureServices(HostBuilderContext hostBuilder, IServiceCollection services)
        {

            var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            var resourceGroup = Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP");

            services.AddSingleton<SubscriptionProxy>((s) =>
            {
                return new SubscriptionProxy(
                     subscriptionId, resourceGroup,
                     new AzureIdentityCredentialAdapter());
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