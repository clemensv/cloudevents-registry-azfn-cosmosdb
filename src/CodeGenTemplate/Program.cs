namespace Microsoft.Storage
{
    using CloudNative.CloudEvents;
    using CloudNative.CloudEvents.Http;
    using CloudNative.CloudEvents.SystemTextJson;

    class StorageEventSender
    {
        HttpClient client;
        private readonly ContentMode contentMode;
        private readonly CloudEventFormatter formatter;

        public StorageEventSender(HttpClient client, ContentMode contentMode, CloudEventFormatter formatter)
        {
            this.contentMode = contentMode;
            this.formatter = formatter;
        }

        public StorageEventSender(HttpClient client)
            : this(client, ContentMode.Structured, new JsonEventFormatter())
        {
        }


        public async Task SendBlobCreatedAsync(string subscriptionId, string resourceGroupName, string storageAccountName, object data)
        {
            CloudEvent cloudEvent = new CloudEvent();
            cloudEvent.SetAttributeFromString("id", Guid.NewGuid().ToString());
            cloudEvent.Data = data; 
            //{
            //    Id = Guid.NewGuid().ToString(),
            //    Source = new Uri($"https://management.azure.com/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Storage/storageAccounts/{storageAccountName}"),
            //    Type = "Microsoft.Storage.BlobCreated",
            //    Data = data,
            //    DataContentType = "application/json",
            //    Time = DateTimeOffset.UtcNow
            //};


            await client.PostAsync(client.BaseAddress, cloudEvent.ToHttpContent(contentMode, formatter));

        }
    }
}