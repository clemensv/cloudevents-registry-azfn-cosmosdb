using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Azure.CloudEvents.Discovery
{
    public partial class FileParameter
    {
        public FileParameter(System.IO.Stream data)
            : this(data, null, null)
        {
        }


        public FileParameter(System.IO.Stream data, string fileName)
                : this(data, fileName, null)
        {
        }
        public FileParameter(System.IO.Stream data, string fileName, string contentType)
        {
            Data = data;
            FileName = fileName;
            ContentType = contentType;
        }

        public System.IO.Stream Data { get; private set; }

        public string FileName { get; private set; }

        public string ContentType { get; private set; }
    }
}
