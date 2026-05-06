using System;
using System.Collections.Generic;

namespace TAG.Networking.DockerRegistry.Model.OciImages
{
	public class OciImageLayer : IImageLayer, IOciContentDescriptor
	{
		public OciImageLayer()
		{
		
		}
		public OciImageLayer(Dictionary<string, object> Json)
		{
			OciContentDescriptor Descriptor = OciContentDescriptor.Parse(Json);

			if (!(
				Descriptor.MediaType == "application/vnd.oci.image.layer.v1.tar" ||
				Descriptor.MediaType == "application/vnd.oci.image.layer.v1.tar+gzip" ||
				Descriptor.MediaType == "application/vnd.oci.image.layer.nondistributable.v1.tar" ||
				Descriptor.MediaType == "application/vnd.oci.image.layer.nondistributable.v1.tar+gzip" ||
                // artifac media types
                Descriptor.MediaType == "application/vnd.in-toto+json" ||
                Descriptor.MediaType == "application/vnd.syft+json" ||
                Descriptor.MediaType == "application/vnd.cyclonedx+json" || 
                Descriptor.MediaType == "application/vnd.oci.image.config.v1 + json"
            ))
				throw new Exception("Invalid media type");

			MediaType = Descriptor.MediaType;
			Size = Descriptor.Size;
			Digest = Descriptor.Digest;
			Urls = Descriptor.Urls;
			Annotations = Descriptor.Annotations;
			Data = Descriptor.Data;
			ArtifactType = Descriptor.ArtifactType;
			Platform = Descriptor.Platform;
		}

		public string MediaType { get; set; }
		public long Size { get; set; }
		public HashDigest Digest { get; set; }
		public string[] Urls { get; set; }
		public Dictionary<string, string> Annotations { get; set; }
		public string Data { get; set; }
		public string ArtifactType { get; set; }
		public OciPlatform Platform { get; set; }
	}
}
