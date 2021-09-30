
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
        readonly IResourceGroupDiscoveryMapper _mapper;

        public DiscoveryService(IResourceGroupDiscoveryMapper mapper)
        {
            this._mapper = mapper;
        }

        [FunctionName("services")]
        public async Task<IActionResult> Services(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "services")]
            HttpRequest req,
            ILogger log)
        {
            if (req.Method.Equals("get", StringComparison.InvariantCultureIgnoreCase))
            {
                string name = req.Query["name"];
                List<Service> svcs = new List<Service>();
                await foreach (var svc in _mapper.EnumerateServicesAsync(new UriBuilder(req.Scheme, req.Host.Host,
                    req.Host.Port ?? -1).Uri))
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
            else
            {
                return new UnauthorizedResult();
            }
        }

        [FunctionName("service")]
        public async Task<IActionResult> Service(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "services/{id}")]
            HttpRequest req,
            string id,
            ILogger log)
        {
            string realId = Uri.UnescapeDataString(id);
            if (req.Method.Equals("get", StringComparison.InvariantCultureIgnoreCase))
            {
                await foreach (var svc in _mapper.EnumerateServicesAsync(new UriBuilder(req.Scheme, req.Host.Host,
                    req.Host.Port ?? -1).Uri))
                {
                    if (realId.Equals(svc.Id))
                    {
                        return new OkObjectResult(svc);
                    }
                }

                return new NotFoundResult();
            }
            else
            {
                return new UnauthorizedResult();
            }
        }

    }
}