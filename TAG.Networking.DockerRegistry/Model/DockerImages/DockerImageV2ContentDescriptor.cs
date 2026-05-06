using System;
using System.Collections.Generic;

namespace TAG.Networking.DockerRegistry.Model.DockerImages
{
	public class DockerImageV2ContentDescriptor : IDockerImageV2ContentDescriptor
	{
		public static DockerImageV2ContentDescriptor Parse(Dictionary<string, object> Json)
		{
			DockerImageV2ContentDescriptor Descriptor = new DockerImageV2ContentDescriptor();

			if (!(Json.TryGetValue("mediaType", out object MediaTypeObj) && MediaTypeObj is string JsonMediaType))
				throw new Exception("Invalid media type.");
			Descriptor.MediaType = JsonMediaType;

			if (!Json.TryGetValue("size", out object SizeObj))
				throw new Exception("Invalid size.");
			if (SizeObj is int JsonSizeInt)
				Descriptor.Size = JsonSizeInt;
			else if (SizeObj is long JsonSizeLong)
				Descriptor.Size = JsonSizeLong;
			else
				throw new Exception("Invalid size.");

			if (!(Json.TryGetValue("digest", out object DigestObj) && DigestObj is string JsonDigestString))
				throw new Exception("Invalid digest.");
			if (!HashDigest.TryParseDigest(JsonDigestString, out HashDigest JsonDigest))
				throw new Exception("Invalid digest.");
			Descriptor.Digest = JsonDigest;

			return Descriptor;
		}

		public string MediaType { get; set; }
		public long Size { get; set; }
		public HashDigest Digest { get; set; }
	}
}
