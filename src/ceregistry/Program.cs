using McMaster.Extensions.CommandLineUtils;

namespace ceregistry
{
    [Command("ceregistry")]
    [Subcommand(typeof(EndpointsCommand), typeof(DefinitionGroupsCommand), typeof(SchemasCommand),typeof(UploadCommand))]
    class CeRegistry : CommonOptions
    {
        public static int Main(string[] args)
            => CommandLineApplication.Execute<CeRegistry>(args);

        public virtual int OnExecute(CommandLineApplication app)
        {
            app.ShowHelp();
            return 0;
        }
    }
}
