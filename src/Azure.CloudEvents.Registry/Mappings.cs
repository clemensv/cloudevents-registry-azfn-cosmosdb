using System;
using System.Collections.Generic;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

namespace Azure.CloudEvents.Registry
{
    partial class Resource : IResource
    {

    }

    public interface IResource
    {
        public string Id { get; set; }
        public string GroupId { get; set; }
        public long Version { get; set; }
        public string Self { get; set; }
        public string Description { get; set; }
        public string Name { get; set; }
        public System.Uri Docs { get; set; }
        public string Origin { get; set; }
        public string CreatedBy { get; set; }
        public System.DateTimeOffset CreatedOn { get; set; }
        public string ModifiedBy { get; set; }
        public System.DateTimeOffset ModifiedOn { get; set; }
        public System.Collections.Generic.IDictionary<string, object> AdditionalProperties { get; set;  }
    }
}

namespace Azure.CloudEvents.EndpointRegistry
{
    public partial class Resource : Azure.CloudEvents.Registry.IResource
    {
    }
    public class CloudEventMetadata : Metadata5 { }
    public class KafkaMetadata : Metadata4 { }

    public class HttpMetadata : Metadata3 { }
    public class MqttMetadata : Metadata2 { }
    public class AmqpMetadata : Metadata { }
}

namespace Azure.CloudEvents.SchemaRegistry
{
    public partial class Resource : Azure.CloudEvents.Registry.IResource
    {
    }

    public partial class Schema : Azure.CloudEvents.Registry.IResource
    {
        public Schema()
        {
            this.Format = "JSONSchema/draft-07";
        }
    }

    public partial class SchemaGroup : Azure.CloudEvents.Registry.IResource
    {
        public SchemaGroup()
        {
            this.Format = "JSONSchema/draft-07";
        }
    }
}

namespace Azure.CloudEvents.MessageDefinitionsRegistry
{
    public partial class Resource : Azure.CloudEvents.Registry.IResource
    {
    }

    public class CloudEventMetadata : Metadata5 { }
    public class KafkaMetadata : Metadata4 { }

    public class HttpMetadata : Metadata3 { }
    public class MqttMetadata : Metadata2 { }
    public class AmqpMetadata : Metadata { }
}