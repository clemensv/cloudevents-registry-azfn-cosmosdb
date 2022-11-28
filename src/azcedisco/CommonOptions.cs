using McMaster.Extensions.CommandLineUtils;
using System.ComponentModel.DataAnnotations;

namespace azcedisco
{
    class CommonOptions
    {
        [Option(CommandOptionType.SingleValue, Description = "Azure subscription id (Guid)", ShortName = "s" )]
        public string SubscriptionId { get; set; }
        [Option(CommandOptionType.SingleValue, Description = "Azure resource group name", ShortName = "r")]
        public string ResourceGroupName { get; set; }
        [Option(CommandOptionType.SingleValue, Description = "Discovery endpoint", ShortName = "e")]
        public string DiscoveryEndpoint { get; set;  }
        [Option(CommandOptionType.SingleValue, Description = "FunctionsKey", ShortName = "f")]
        public string FunctionsKey { get; set; }

    }
}
