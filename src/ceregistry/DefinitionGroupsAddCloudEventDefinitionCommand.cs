using Azure.CloudEvents.Discovery;
using McMaster.Extensions.CommandLineUtils;

namespace ceregistry
{
    [Command("add-cloudevent-definition")]
    internal class DefinitionGroupsAddCloudEventDefinitionCommand : CommonOptions
    {
        [Argument(0, Description = "The name of the definition group to add the definition to.")]
        public string GroupName { get; set; }

        [Argument(1, Description = "The name of the definition to add to the definition group.")]
        public string DefinitionName { get; set; }
        [Option(CommandOptionType.SingleValue, Description = "Description of the definition group")]
        public string Description { get; set; }
        [Option(CommandOptionType.SingleValue, Description = "URL pointing to documentation details on the definition group", ShortName = "")]
        public Uri Docs { get; set; }
        [Option(CommandOptionType.SingleValue, Description = "Origin of the definition group")]
        public string Origin { get; set; }
        [Option(CommandOptionType.MultipleValue, Description = "Tags to set for the definition group")]
        public Tags[]? Tags { get; set; }
        
        
        public virtual async Task<int> OnExecuteAsync(CommandLineApplication app)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-functions-key", AccessKey);
            var client = new DiscoveryClient(httpClient);
            client.BaseUrl = Endpoint;

            var definition = new CloudEventDefinition
            {
                Id = DefinitionName,
                Name = DefinitionName,
                Description = Description,
                Docs = Docs,
                Origin = Origin,
                Tags = Tags,
                Version = 1
            };
            await client.PostDefinitionAsync(definition, GroupName, DefinitionName);
            return 0;
        }
    }
}