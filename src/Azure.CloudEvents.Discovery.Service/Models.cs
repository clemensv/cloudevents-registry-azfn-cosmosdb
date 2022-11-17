using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.CloudEvents.Discovery.Service
{

    public interface WithParentId
    {
        public string ParentId { get; set; }
    }

    public class Definition_Db : Definition, WithParentId
    {
        [Newtonsoft.Json.JsonProperty("parentId")]
        public string ParentId { get; set; }
    }

    public class Schema_Db: Schema, WithParentId
    {
        [Newtonsoft.Json.JsonProperty("parentId")]
        public string ParentId { get; set; }
    }
}
