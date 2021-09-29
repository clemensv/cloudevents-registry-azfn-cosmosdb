using System;

namespace SystemTopicEnumeration
{
    using Azure.Identity;
    using Microsoft.Azure.Management.EventGrid;
    using Microsoft.Rest;

    class Program
    {
        static void Main(string[] args)
        {
            var defaultClient = new DefaultAzureCredential();
            var token = defaultClient.GetToken(new Azure.Core.TokenRequestContext(new[] { $"https://management.azure.com/.default" }));
            ServiceClientCredentials serviceClientCreds = new TokenCredentials(token.Token);
            EventGridManagementClient em = new EventGridManagementClient(serviceClientCreds);

            
            foreach (var st in em.TopicTypes.List())
            {
                Console.WriteLine(st.Name);
                foreach (var et in em.TopicTypes.ListEventTypes(st.Name))
                {
                  Console.WriteLine("  "+et.Name);   
                }
            }
        }
    }
}
