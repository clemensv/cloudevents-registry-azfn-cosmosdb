using Azure.CloudEvents.Discovery;
using McMaster.Extensions.CommandLineUtils;

namespace ceregistry
{
    [Command("defgroup")]
    [Subcommand(typeof(DefinitionsGroupsAddCommand), 
                typeof(DefinitionsGroupsRemoveCommand),
                typeof(DefinitionsGroupsChangeCommand),
                typeof(DefinitionGroupsAddCloudEventDefinitionCommand))]
    internal class DefinitionGroupsCommand : CommonOptions
    {
        public virtual int OnExecute(CommandLineApplication app)
        {
            app.ShowHelp();
            return 0;
        }
    }
}