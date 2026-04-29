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
using Waher.Networking.XMPP.PubSub;
using Waher.Persistence;
using Waher.Persistence.Filters;
using Waher.Security;
using Waher.Security.LoginMonitor;

namespace TAG.Networking.DockerRegistry.Endpoints
{
    internal class ManifestEndpoints : DockerEndpoints
    {
        public ManifestEndpoints(string DockerRegistryFolder, ISniffer[] Sniffers)
            : base(DockerRegistryFolder, Sniffers)
        {

        }

        // <summary>
        // Fetch the manifest identified by name and reference where reference can be a tag or digest.
        // A HEAD request can also be issued to this endpoint to obtain resource information without receiving all data.
        // <summary>

        public async Task GET(HttpRequest Request, HttpResponse Response, DockerActor Actor, DockerRepository Repository, string Reference)
        {
            await AssertRepositoryPrivilages(Actor, Repository, DockerRepository.RepositoryAction.Pull, Request);

            DockerManifest Manifest;

            if (HashDigest.TryParseDigest(Reference, out HashDigest Digest))
            {
                Manifest = await Database.FindFirstIgnoreRest<DockerManifest>(new FilterAnd(new FilterFieldEqualTo("Digest", Digest)));
            }
            else
            {
                ImageReference ImageReference = await Database.FindFirstIgnoreRest<ImageReference>(new FilterAnd(
                    new FilterFieldEqualTo(nameof(ImageReference.RepositoryName), Repository.RepositoryName),
                    new FilterFieldEqualTo(nameof(ImageReference.Tag), Reference)));

                if (ImageReference is null)
                    throw new NotFoundException(new DockerError(DockerErrorCode.MANIFEST_UNKNOWN, "Manifest unknown."), apiHeader);

                Manifest = await Database.FindFirstIgnoreRest<DockerManifest>(new FilterAnd(new FilterFieldEqualTo("Digest", ImageReference.Digest)));
            }

            if (Manifest is null)
                throw new NotFoundException(new DockerError(DockerErrorCode.MANIFEST_UNKNOWN, "Manifest unknown."), apiHeader);

            Request.Header.AcceptEncoding = null;

            await Response.Return(Manifest.Manifest);
        }

        public async Task DELETE(HttpRequest Request, HttpResponse Response, DockerActor Actor, DockerRepository Repository, string Reference)
        {
            await AssertRepositoryPrivilages(Actor, Repository, DockerRepository.RepositoryAction.Delete, Request);
            DockerActor Owner = await Repository.GetOwner();
            await using WritableStorageHandle Handle = await Owner.GetWritableStorage();

            if (!HashDigest.TryParseDigest(Reference, out HashDigest Digest))
                throw new BadRequestException(new DockerErrors(DockerErrorCode.DIGEST_INVALID, "Invalid manifest digest reference."), apiHeader);

            DockerManifest Manifest = await Database.FindFirstIgnoreRest<DockerManifest>(new FilterAnd(
                new FilterFieldEqualTo(nameof(DockerManifest.Digest), Digest)));

            if (Manifest is null)
                throw new NotFoundException(new DockerErrors(DockerErrorCode.NAME_INVALID, "Manifest unknown."), apiHeader);

            await Database.FindDelete<ImageReference>(new FilterAnd(
                new FilterFieldEqualTo(nameof(ImageReference), Manifest.Digest)
            ));

            if (Manifest.Manifest is IImageManifest Image)
                await Handle.Storage.UnregisterImage(Image);

            await Database.Delete(Manifest);

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

            DockerManifest Old = await Database.FindFirstIgnoreRest<DockerManifest>(new FilterAnd(
                new FilterFieldEqualTo(nameof(DockerManifest.Digest), Manifest.Digest)
            ));

            if (!(Old is null))
            {
                Log.Informational("Docker image uploaded.", Old.Digest.ToString());
                Response.StatusCode = 201;
                Response.StatusMessage = "Created";
                Response.SetHeader("Docker-Content-Digest", new HashDigest(HashFunction.SHA256, Old.Manifest.Raw).ToString());
                await Response.SendResponse();
                return;
            }


            if (HashDigest.TryParseDigest(Reference, out HashDigest Digest))
            {
                if (Digest != new HashDigest(HashFunction.SHA256, Manifest.Raw))
                    throw new BadRequestException(new DockerErrors(DockerErrorCode.MANIFEST_INVALID, "Manifest invalid. Digest mismatch."), apiHeader);
            }
            else
                Tag = Reference;

            DockerActor Owner = await Repository.GetOwner();
            await using WritableStorageHandle Handle = await Owner.GetWritableStorage();

            DockerManifest NewManifest = new DockerManifest()
            {
                Digest = Manifest.Digest,
                Manifest = Manifest,
            };

            if (NewManifest.Manifest is IImageManifest ManifestImage)
            {
                await Handle.Storage.RegisterManifest(ManifestImage);

                if (Handle.Storage.MaxStorage - Handle.Storage.UsedStorage < 0)
                {
                    await Handle.Storage.UnregisterImage(ManifestImage);
                    throw new ForbiddenException(new DockerErrors(DockerErrorCode.DENIED, "Storage quota exceeded."), apiHeader);
                }
                
                foreach (IImageLayer Layer in ManifestImage.GetLayers())
                {
                    await Database.FindDelete<DanglingDockerBlob>(new FilterAnd(new FilterFieldEqualTo("Digest", Layer.Digest)));
                }
            }


            await Database.Insert(NewManifest);
            await Database.FindDelete<DanglingDockerBlob>(new FilterAnd(new FilterFieldEqualTo("Digest", NewManifest.Digest)));


            if (!string.IsNullOrEmpty(Tag))
            {
                ImageReference Ref = await Database.FindFirstIgnoreRest<ImageReference>(new FilterAnd(
                    new FilterFieldEqualTo(nameof(ImageReference.RepositoryName), Repository.RepositoryName),
                    new FilterFieldEqualTo(nameof(ImageReference.Tag), Tag)));

                if (Ref is null)
                {
                    Ref = new ImageReference()
                    {
                        Digest = Manifest.Digest,
                        RepositoryName = Repository.RepositoryName,
                        Tag = Tag
                    };
                    await Database.Insert(Ref);
                }
                else
                {
                    Ref.Digest = Manifest.Digest;
                    await Database.Update(Ref);
                }
            }


            Log.Informational("Docker image uploaded.", NewManifest.Digest.ToString());
            Response.StatusCode = 201;
            Response.StatusMessage = "Created";
            Response.SetHeader("Docker-Content-Digest", new HashDigest(HashFunction.SHA256, NewManifest.Manifest.Raw).ToString());
            await Response.SendResponse();
        }
    }
}
