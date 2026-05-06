using System;
using System.Collections.Generic;

namespace TAG.Networking.DockerRegistry.Model.OciImages
{
	public class OciImageConfig : IImageConfig, IOciContentDescriptor
	{
		public OciImageConfig()
		{
		}
		public OciImageConfig(Dictionary<string, object> Json)
		{
			OciContentDescriptor Descriptor = OciContentDescriptor.Parse(Json);

			if (!(Descriptor.MediaType == "application/vnd.oci.image.config.v1+json"))
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
