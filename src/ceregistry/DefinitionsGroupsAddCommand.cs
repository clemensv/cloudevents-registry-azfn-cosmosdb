using Azure.CloudEvents.Discovery;
using McMaster.Extensions.CommandLineUtils;
using Microsoft.VisualBasic;
using System.ComponentModel.DataAnnotations;

namespace ceregistry
{
    [Command("add-group")]
    internal class DefinitionsGroupsAddCommand : CommonOptions
    {
        [Option(CommandOptionType.SingleValue, Description = "The name of the definition group to add."), Required]
        public string GroupName { get; set; }
        [Option(CommandOptionType.SingleValue, Description = "Description of the definition group")]
        public string Description { get; set; }
        [Option(CommandOptionType.SingleValue, Description = "URL pointing to documentation details on the definition group", ShortName = "")]
        public Uri Docs { get; set; }
        [Option(CommandOptionType.SingleValue, Description = "Origin of the definition group", ShortName = "o")]
        public string Origin { get; set; }
        [Option(CommandOptionType.MultipleValue, Description = "Tags to set for the definition group")]
        public Tags[]? Tags { get; set; }


        public virtual int OnExecute(CommandLineApplication app)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-functions-key", AccessKey);
            var client = new DiscoveryClient(httpClient);
            client.BaseUrl = Endpoint;
            var group = new Group { 
                Id = GroupName, 
                Name = GroupName, 
                Description = Description, 
                Docs = Docs,
                Origin = Origin,
                Tags = Tags,
                Version = 1
            };
            client.PutGroupAsync(group, group.Id).Wait();
            return 0;
        }
    }
}