using McMaster.Extensions.CommandLineUtils;

namespace azcedisco
{
    [Command()]
    class Remove : CommonOptions
    {
        public virtual int OnExecute(CommandLineApplication app)
        {
            app.ShowHelp();
            return 0;
        }
    }
}
