using System.Collections.Generic;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Errors;
using TAG.Networking.DockerRegistry.Model;
using TAG.Networking.DockerRegistry.Model.DockerImages;
using TAG.Networking.DockerRegistry.Model.OciImages;
using Waher.Content;
using Waher.Events;
using Waher.Networking.HTTP;
using Waher.Networking.Sniffers;
using Waher.Networking.XMPP.Provisioning.SearchOperators;
using Waher.Persistence;
using Waher.Persistence.Filters;
using Waher.Security;

namespace TAG.Networking.DockerRegistry.Endpoints
{
    internal class ManifestEndpoints : DockerEndpoints
    {
        public ManifestEndpoints(ManifestManager ManifestManager, BlobManager BlobManager, ISniffer[] Sniffers)
            : base(ManifestManager, BlobManager, Sniffers)
        {

        }

        // <summary>
        // Fetch the manifest identified by name and reference where reference can be a tag or digest.
        // A HEAD request can also be issued to this endpoint to obtain resource information without receiving all data.
        // <summary>

        public async Task GET(HttpRequest Request, HttpResponse Response, DockerActor Actor, DockerRepository Repository, string Reference)
        {
            await AssertRepositoryPrivilages(Actor, Repository, DockerRepository.RepositoryAction.Pull, Request);

            IManifest? Manifest = null;

            if (HashDigest.TryParseDigest(Reference, out HashDigest Digest))
                Manifest = await this.manifestManager.FindManifest(Digest);
            else
                Manifest = await this.manifestManager.FindManifest(Repository.RepositoryName, Reference);

            if (Manifest is null)
                throw new NotFoundException(new DockerError(DockerErrorCode.MANIFEST_UNKNOWN, "Manifest unknown."), apiHeader);

            Request.Header.AcceptEncoding = null;

            await Response.Return(Manifest);
        }

        public async Task DELETE(HttpRequest Request, HttpResponse Response, DockerActor Actor, DockerRepository Repository, string Reference)
        {
            await AssertRepositoryPrivilages(Actor, Repository, DockerRepository.RepositoryAction.Delete, Request);

            DockerActor Owner = await Repository.GetOwner();
            await using WritableStorageHandle Handle = await Owner.GetWritableStorage();

            if (!HashDigest.TryParseDigest(Reference, out HashDigest Digest))
                throw new BadRequestException(new DockerErrors(DockerErrorCode.DIGEST_INVALID, "Invalid manifest digest reference."), apiHeader);
            await this.manifestManager.DeleteManifest(Digest, Handle);

            Response.StatusCode = 202;
            Response.StatusMessage = "Accepted";
            Response.ContentLength = 0;
            Response.SetHeader("Docker-Content-Digest", Digest.ToString());

            await Response.SendResponse();
        }

        public async Task PUT(HttpRequest Request, HttpResponse Response, DockerActor Actor, DockerRepository Repository, string Reference)
        {
            await AssertRepositoryPrivilages(Actor, Repository, DockerRepository.RepositoryAction.Push, Request);

            ContentResponse ManifestContentResponse = await Request.DecodeDataAsync();
            string Tag = null;

            IManifest Manifest;

            if (ManifestContentResponse.Decoded is OCIImageManifest OciManifest)
                Manifest = OciManifest;
            else if (ManifestContentResponse.Decoded is DockerImageManifestV2 DockerManifestV2)
                Manifest = DockerManifestV2;
            else if (ManifestContentResponse.Decoded is OciImageIndex OciImageIndex)
                Manifest = OciImageIndex;
            else
                throw new BadRequestException(new DockerErrors(DockerErrorCode.MANIFEST_INVALID, "Manifest invalid."), apiHeader);

            if (Manifest is IImageManifest Image)
            {
                foreach (IImageLayer Layer in Image.GetLayers())
                {
                    DockerBlob Blob = await Database.FindFirstIgnoreRest<DockerBlob>(new FilterAnd(
                        new FilterFieldEqualTo("Digest", Layer.Digest)));

                    if (Blob is null)
                        throw new BadRequestException(new DockerErrors(DockerErrorCode.BLOB_UNKNOWN,
                        "BLOB unknown to registry.", new Dictionary<string, object>()
                        {
                                    { "digest", Layer.Digest.ToString() }
                        }), apiHeader);
                }
            }

            if (HashDigest.TryParseDigest(Reference, out HashDigest Digest))
            {
                if (Digest != new HashDigest(HashFunction.SHA256, Manifest.Raw))
                    throw new BadRequestException(new DockerErrors(DockerErrorCode.MANIFEST_INVALID, "Manifest invalid. Digest mismatch."), apiHeader);
            }
            else
                Tag = Reference;

            DockerManifest Old = await Database.FindFirstIgnoreRest<DockerManifest>(new FilterAnd(
                new FilterFieldEqualTo(nameof(DockerManifest.Digest), Manifest.Digest)
            ));

            DockerActor Owner = await Repository.GetOwner();
            await using WritableStorageHandle StorageHandle = await Owner.GetWritableStorage();

            if (Old is null && !await this.manifestManager.TryCreateManifest(Manifest, StorageHandle))
                throw new ForbiddenException(new DockerErrors(DockerErrorCode.DENIED, "Storage quota exceeded."), apiHeader);

            if (!string.IsNullOrEmpty(Tag))
                await this.manifestManager.CreateManifestTag(Manifest.Digest, Repository.RepositoryName, Tag, StorageHandle);

            Log.Informational("Docker image uploaded.", Manifest.Digest.ToString());
            Response.StatusCode = 201;
            Response.StatusMessage = "Created";
            Response.SetHeader("Docker-Content-Digest", new HashDigest(HashFunction.SHA256, Manifest.Raw).ToString());
            await Response.SendResponse();
        }
    }
}
