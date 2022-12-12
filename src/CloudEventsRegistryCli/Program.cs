using McMaster.Extensions.CommandLineUtils;

namespace CloudEventsRegistryCli
{
    [Command("ceregistry")]
    [Subcommand(typeof(UploadCommand))]
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
