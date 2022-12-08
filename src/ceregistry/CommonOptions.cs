using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;

namespace ceregistry
{
    class CommonOptions
    {
        [Option(CommandOptionType.SingleValue, Description = "Registry endpoint", ShortName = "e")]
        public string Endpoint { get; set; }
        [Option(CommandOptionType.SingleValue, Description = "Registry endpoint access key", ShortName = "k")]
        public string AccessKey { get; set; }
    }
}
