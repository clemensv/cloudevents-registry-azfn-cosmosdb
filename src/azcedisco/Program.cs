using McMaster.Extensions.CommandLineUtils;
using System;

namespace azcedisco
{
    [Command("azcedisco")]
    [Subcommand(typeof(Import))]
    class AzCeDisco : CommonOptions
    {
        public static int Main(string[] args)
            => CommandLineApplication.Execute<AzCeDisco>(args);
       

        public virtual int OnExecute(CommandLineApplication app)
        {
            app.ShowHelp();
            return 0;
        }
    }
}
