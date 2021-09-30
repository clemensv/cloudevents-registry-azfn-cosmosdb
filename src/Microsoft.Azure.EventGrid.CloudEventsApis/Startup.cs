using Microsoft.Azure.EventGrid.CloudEventsApis;
using Microsoft.Azure.Functions.Extensions.DependencyInjection;

[assembly: FunctionsStartup(typeof(Startup))]

namespace Microsoft.Azure.EventGrid.CloudEventsApis
{
    using System;
    using Microsoft.Azure.EventGrid.CloudEventsApiBridge;
    using Microsoft.Azure.Functions.Extensions.DependencyInjection;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using Microsoft.IdentityModel.Clients.ActiveDirectory;

    public class Startup : FunctionsStartup
    {
        public override void Configure(IFunctionsHostBuilder builder)
        {
            var subscriptionId = Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID");
            var resourceGroup = Environment.GetEnvironmentVariable("AZURE_RESOURCE_GROUP");

            builder.Services.AddSingleton<IResourceGroupDiscoveryMapper>((s) =>
            {
                return new ResourceGroupDiscoveryMapper(
                     subscriptionId, resourceGroup,
                     new AzureIdentityCredentialAdapter());
            });
        }
    }
}