using System;
using System.Collections.Generic;

namespace TAG.Networking.DockerRegistry.Model.OciImages
{
	public class OciContentDescriptor : IOciContentDescriptor
	{
		public static OciContentDescriptor Parse(Dictionary<string, object> Json)
		{
			OciContentDescriptor Descriptor = new OciContentDescriptor();

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

			if (Json.TryGetValue("urls", out object UrlsObj) && UrlsObj is object[] UrlsArray)
			{
				List<string> UrlsList = new List<string>();
				foreach (object Url in UrlsArray)
				{
					if (Url is string s)
						UrlsList.Add(s);
				}
				Descriptor.Urls = UrlsList.ToArray();
			}
			else
				Descriptor.Urls = null;

			if (Json.TryGetValue("annotations", out object AnnotationsObj) && AnnotationsObj is Dictionary<string, string> JsonAnnotations)
				Descriptor.Annotations = JsonAnnotations;

			if (Json.TryGetValue("data", out object DataObj) && DataObj is string JsonData)
				Descriptor.Data = JsonData;

			if (Json.TryGetValue("artifactType", out object ArtifactTypeObj) && ArtifactTypeObj is string JsonArtifactType)
				Descriptor.ArtifactType = JsonArtifactType;

			// optional platform
			if (Json.TryGetValue("platform", out object PlatformObj) && PlatformObj is Dictionary<string, object> PlatformDict)
			{
				try
				{
					Descriptor.Platform = new OciPlatform(PlatformDict);
				}
				catch (Exception e)
				{
					throw new Exception("Invalid platform.", e);
				}
			}

			return Descriptor;
		}
		public OciPlatform Platform { get; set; }
		public string MediaType { get; set; }
		public long Size { get; set; }
		public HashDigest Digest { get; set; }
		public string[] Urls { get; set; }
		public Dictionary<string, string> Annotations { get; set; }
		public string Data { get; set; }
		public string ArtifactType { get; set; }
	}
}
