namespace xRegistry.Types.Registry
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

namespace xRegistry.Types.EndpointRegistry
{
    public partial class Resource : xRegistry.Types.Registry.IResource
    {
    }
    public class CloudEventMetadata : Metadata5 { }
    public class KafkaMetadata : Metadata4 { }

    public class HttpMetadata : Metadata3 { }
    public class MqttMetadata : Metadata2 { }
    public class AmqpMetadata : Metadata { }
}

namespace xRegistry.Types.SchemaRegistry
{
    public partial class Resource : xRegistry.Types.Registry.IResource
    {
    }

    public partial class Schema : xRegistry.Types.Registry.IResource
    {
        public Schema()
        {
            this.Format = "JSONSchema/draft-07";
        }
    }

    public partial class SchemaGroup : xRegistry.Types.Registry.IResource
    {
        public SchemaGroup()
        {
            this.Format = "JSONSchema/draft-07";
        }
    }
}

namespace xRegistry.Types.MessageDefinitionsRegistry
{
    public partial class Resource : xRegistry.Types.Registry.IResource
    {
    }

    public class CloudEventMetadata : Metadata5 { }
    public class KafkaMetadata : Metadata4 { }

    public class HttpMetadata : Metadata3 { }
    public class MqttMetadata : Metadata2 { }
    public class AmqpMetadata : Metadata { }
}