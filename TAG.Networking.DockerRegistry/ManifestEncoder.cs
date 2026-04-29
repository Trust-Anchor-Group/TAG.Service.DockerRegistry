using System;
using System.Text;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Model;
using TAG.Networking.DockerRegistry.Model.OciImages;
using Waher.Content;
using Waher.Runtime.Inventory;

namespace TAG.Networking.DockerRegistry
{
	public class ManifestEncoder : IContentEncoder
	{
		public ManifestEncoder()
		{

		}

		public string[] ContentTypes => contentTypes;
		private static readonly string[] contentTypes = new string[]
		{
			OCIImageManifest.MediaTypeValue,
			OciImageIndex.MediaTypeValue
        };


		public string[] FileExtensions => throw new System.NotImplementedException();

		public Task<ContentResponse> EncodeAsync(object Object, Encoding Encoding, ICodecProgress Progress, params string[] AcceptedContentTypes)
		{
			if (!(Object is IManifest Manifest))
			{
				return Task.FromResult(new ContentResponse(new ArgumentException("Object not IManifest.", nameof(Object))));
			}

			return Task.FromResult(new ContentResponse(Manifest.MediaType, Object, Manifest.Raw));
		}

		public bool Encodes(object Object, out Grade Grade, params string[] AcceptedContentTypes)
		{
			if (Object is IManifest)
			{
				Grade = Grade.Ok;
				return true;
			}
			else
			{
				Grade = Grade.NotAtAll;
				return false;
			}
		}

		public bool TryGetContentType(string FileExtension, out string ContentType)
		{
			ContentType = "";
			return false;
		}

		public bool TryGetFileExtension(string ContentType, out string FileExtension)
		{
			FileExtension = "";
			return false;
		}
	}
}
