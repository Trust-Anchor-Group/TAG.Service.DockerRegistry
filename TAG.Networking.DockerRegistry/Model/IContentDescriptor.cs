using System;
using System.Collections.Generic;
using System.Text;

namespace TAG.Networking.DockerRegistry.Model
{
    public interface IContentDescriptor
    {
        public string MediaType { get; set; }
        public long Size { get; set; }
        public HashDigest Digest { get; set; }
    }
}
