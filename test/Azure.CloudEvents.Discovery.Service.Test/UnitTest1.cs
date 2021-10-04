using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Moq;
using System;
using Xunit;

namespace Azure.CloudEvents.Discovery.Service.Test
{
    public class UnitTest1
    {
        CosmosClient cosmosClient;
        DiscoveryService svc;

        public UnitTest1()
        {
            cosmosClient = new CosmosClient("https://localhost:8081", "C2y6yDjf5/R+ob0N8A7Cgv30VRDJIWEHLM+4QDU5DE2nQ9nDuVTqobD4b8mGGyPMbIZnqyMsEcaGQy67XIw/Jw==");
            svc = new DiscoveryService(cosmosClient);
        }

        
    }
}
