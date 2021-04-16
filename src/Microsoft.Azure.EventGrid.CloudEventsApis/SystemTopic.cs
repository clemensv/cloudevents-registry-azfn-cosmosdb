// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 

using System.Collections.Generic;

namespace Microsoft.Azure.EventGrid.CloudEventsApiBridge
{
    using Newtonsoft.Json;
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse); 
    public class SystemTopicProperties
    {
        [JsonProperty("provider")]
        public string Provider { get; set; }

        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("resourceRegionType")]
        public string ResourceRegionType { get; set; }

        [JsonProperty("provisioningState")]
        public string ProvisioningState { get; set; }

        [JsonProperty("supportedLocations")]
        public List<string> SupportedLocations { get; set; }

        [JsonProperty("sourceResourceFormat")]
        public string SourceResourceFormat { get; set; }
    }

    public class SystemTopic
    {
        [JsonProperty("properties")]
        public SystemTopicProperties Properties { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }


}