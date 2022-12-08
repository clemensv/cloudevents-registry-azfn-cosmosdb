using Azure.CloudEvents.Discovery;
using McMaster.Extensions.CommandLineUtils;

namespace ceregistry
{
    [Command("remove-group")]
    internal class DefinitionsGroupsRemoveCommand : CommonOptions
    {
        [Argument(0, Description = "The name of the definition group to remove.")]
        public string GroupName { get; set; }

        public virtual async Task<int> OnExecuteAsync(CommandLineApplication app)
        {
            HttpClient httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("x-functions-key", AccessKey);
            var client = new DiscoveryClient(httpClient);
            client.BaseUrl = Endpoint;
            var group = await client.GetGroupAsync(GroupName);
            await client.DeleteGroupAsync(group.Id, group.Version+1);
            return 0;
        }
    }
}