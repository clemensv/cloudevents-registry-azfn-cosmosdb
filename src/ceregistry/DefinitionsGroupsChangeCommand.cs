using Azure.CloudEvents.Discovery;
using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;

namespace ceregistry
{
    [Command("change-group")]
    internal class DefinitionsGroupsChangeCommand : CommonOptions
    {
        [Option(CommandOptionType.SingleValue, Description = "The name of the definition group to change."), Required]
        public string GroupName { get; set; }
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
            var group = await client.GetGroupAsync(GroupName);
            group.Version = group.Version + 1;
            if ( !string.IsNullOrEmpty(Description))
            {
                group.Description = Description;
            }
            if (!string.IsNullOrEmpty(Origin))
            {
                group.Origin = Origin;
            }
            if (Docs != null)
            {
                group.Docs = Docs;
            }
            if (Tags != null)
            {
                group.Tags = Tags;
            }
            await client.PutGroupAsync(group, group.Id);
            return 0;
        }
    }
}