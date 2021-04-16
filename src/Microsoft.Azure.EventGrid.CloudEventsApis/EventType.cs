using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.Azure.EventGrid.CloudEventsApis
{
    using Newtonsoft.Json;

    public class EventTypeProperties
    {
        [JsonProperty("displayName")]
        public string DisplayName { get; set; }

        [JsonProperty("description")]
        public string Description { get; set; }

        [JsonProperty("schemaUrl")]
        public string SchemaUrl { get; set; }

        [JsonProperty("isInDefaultSet")]
        public bool IsInDefaultSet { get; set; }
    }

    public class EventType
    {
        [JsonProperty("properties")]
        public EventTypeProperties Properties { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }
    }



}
