using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.CloudEvents.Discovery.Service
{

    public interface HasVersions<T>
    {
        public T Versions{ get; set; }
    }

}
