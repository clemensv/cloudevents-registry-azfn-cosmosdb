using Azure.CloudEvents.Discovery;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;

namespace ceregistry
{
    internal class UploadCommand : CommonOptions
    {
        [Option(CommandOptionType.SingleValue, Description = "The file to upload", ShortName = "f"), Required]
        public string FileName { get; set; }

        public async Task OnExecute()
        {
            using (var file = File.OpenRead(FileName))
            {
                var sr = new StreamReader(file);
                var obj = JsonConvert.DeserializeObject(sr.ReadToEnd());
                HttpClient httpClient = new HttpClient();
                httpClient.DefaultRequestHeaders.Add("x-functions-key", AccessKey);
                var client = new DiscoveryClient(httpClient);
                client.BaseUrl = Endpoint;
                await client.UploadDocAsync(obj);
            }
        }
    }
}