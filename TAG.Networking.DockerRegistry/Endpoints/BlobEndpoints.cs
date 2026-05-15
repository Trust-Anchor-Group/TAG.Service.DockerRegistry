using System.IO;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Errors;
using TAG.Networking.DockerRegistry.Model;
using Waher.Networking.HTTP;
using Waher.Networking.Sniffers;

namespace TAG.Networking.DockerRegistry.Endpoints
{
	internal class BlobEndpoints : DockerEndpoints
	{
		public BlobEndpoints(ManifestManager ManifestManager, BlobManager BlobManager, ISniffer[] Sniffers)
            : base(ManifestManager, BlobManager, Sniffers)
        {
		}

		public async Task GET(HttpRequest Request, HttpResponse Response, ByteRangeInterval Interval, DockerActor Actor, DockerRepository Repository, string Reference)
		{
            await AssertRepositoryPrivilages(Actor, Repository, DockerRepository.RepositoryAction.Pull, Request);

            if (!HashDigest.TryParseDigest(Reference, out HashDigest Digest))
				throw new BadRequestException(new DockerErrors(DockerErrorCode.DIGEST_INVALID, "Provided digest did not match uploaded content."), apiHeader);

			FileStream? BlobStream = await this.blobManager.ReadBlob(Digest);

			if (BlobStream == null)
				throw new NotFoundException(new DockerErrors(DockerErrorCode.BLOB_UNKNOWN, "Blob not found."), apiHeader);

			Request.Header.AcceptEncoding = null;

			using (BlobStream)
			{
				long Offset = Interval?.First ?? 0L;
				long Count;

				Count = BlobStream.Length;

				Response.StatusCode = 200;
				Response.SetHeader("Content-Length", BlobStream.Length.ToString());
				Response.SetHeader("Docker-Content-Digest", Digest.ToString());
				Response.SetHeader("Content-Range", Offset.ToString() + "-" +
					(Offset + Count - 1).ToString() + "/" + BlobStream.Length.ToString());

				await WriteToResponse(Response, BlobStream, Offset, Count);
			}

			await Response.SendResponse();
		}

		public async Task DELETE(HttpRequest Request, HttpResponse Response, DockerActor Actor, DockerRepository Repository, string Reference)
		{
			throw new BadRequestException(new DockerErrors(DockerErrorCode.UNAUTHORIZED, "Deleting blobs via API is not allowed"));
		}
	}
}
