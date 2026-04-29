using System.Collections.Generic;

namespace TAG.Networking.DockerRegistry.Model.OciImages
{
	public interface IOciContentDescriptor : IContentDescriptor
	{
		public string[] Urls { get; set; }
		public Dictionary<string, string> Annotations { get; set; }
		public string Data { get; set; }
		public string ArtifactType { get; set; }
		public OciPlatform Platform { get; set; }
	}
}
