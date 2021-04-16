
namespace Microsoft.Azure.EventGrid.CloudEventsApis
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.Azure.EventGrid.CloudEventsApiBridge;
    using Microsoft.Azure.EventGrid.CloudEventsApis.Discovery;
    using Microsoft.Azure.WebJobs;
    using Microsoft.Azure.WebJobs.Extensions.Http;
    using Microsoft.Extensions.Logging;
    using Newtonsoft.Json;

    public class DiscoveryService
    {
        readonly IResourceGroupEventProxy proxy;

        public DiscoveryService(IResourceGroupEventProxy proxy)
        {
            this.proxy = proxy;
        }

        [FunctionName("services")]
        public async Task<IActionResult> GetServices(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
            HttpRequest req,
            ILogger log)
        {
            string name = req.Query["name"];
            List<Service> svcs = new List<Service>();
            await foreach (var svc in proxy.EnumerateServicesAsync(new UriBuilder(req.Scheme, req.Host.Host, req.Host.Port ?? -1).Uri))
            {
                if (string.IsNullOrEmpty(name) || name.Equals(svc.Name))
                {
                    svcs.Add(svc);
                }
            }

            return svcs.Count > 0
                ? (IActionResult)new OkObjectResult(svcs)
                : new NotFoundResult();
        }
    }
}