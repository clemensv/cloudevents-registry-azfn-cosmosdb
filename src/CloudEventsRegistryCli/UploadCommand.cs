using Azure.CloudEvents.Registry;
using McMaster.Extensions.CommandLineUtils;
using Newtonsoft.Json;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;

namespace CloudEventsRegistryCli
{
    internal class UploadCommand : CommonOptions
    {
        [Option(CommandOptionType.SingleValue, Description = "The file to upload", ShortName = "f"), Required]
        public string FileName { get; set; }

        public async Task OnExecute()
        {
            try
            {
                using (var file = File.OpenRead(FileName))
                {
                    var sr = new StreamReader(file);
                    try
                    {
                        var obj = JsonConvert.DeserializeObject(sr.ReadToEnd());
                        try
                        {
                            HttpClient httpClient = new HttpClient();
                            httpClient.DefaultRequestHeaders.Add("x-functions-key", AccessKey);
                            var client = new RegistryClient(Endpoint, httpClient);
                            await client.UploadDocAsync(obj);
                        }
                        catch (ApiException ex)
                        {
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error: {ex.Message}");
                        }
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"Error parsing file: {ex.Message}");
                    }
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"File not found: {FileName}");
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error {ex.Message} reading file: {FileName}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}