using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Errors;
using TAG.Networking.DockerRegistry.Model;
using TAG.Networking.DockerRegistry.Model.DockerImages;
using TAG.Networking.DockerRegistry.Model.OciImages;
using Waher.Content;
using Waher.Events;
using Waher.Networking.HTTP;
using Waher.Networking.Sniffers;
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

            DockerImage Image;

            if (HashDigest.TryParseDigest(Reference, out HashDigest Digest))
            {
                Image = await Database.FindFirstIgnoreRest<DockerImage>(new FilterAnd(
                    new FilterFieldEqualTo("RepositoryName", Repository.RepositoryName),
                    new FilterFieldEqualTo("Digest", Digest)));
            }
            else
            {
                Image = await Database.FindFirstIgnoreRest<DockerImage>(new FilterAnd(
                    new FilterFieldEqualTo("RepositoryName", Repository.RepositoryName),
                    new FilterFieldEqualTo("Tag", Reference)));
            }

            if (Image is null)
                throw new NotFoundException(new DockerError(DockerErrorCode.MANIFEST_UNKNOWN, "Manifest unknown."), apiHeader);

            Request.Header.AcceptEncoding = null;

            await Response.Return(Image.Manifest);
        }
        public async Task DELETE(HttpRequest Request, HttpResponse Response, DockerActor Actor, DockerRepository Repository, string Reference)
        {
            await AssertRepositoryPrivilages(Actor, Repository, DockerRepository.RepositoryAction.Delete, Request);
            DockerActor Owner = await Repository.GetOwner();
            await using WritableStorageHandle Handle = await Owner.GetWritableStorage();

            if (!HashDigest.TryParseDigest(Reference, out HashDigest Digest))
                throw new BadRequestException(new DockerErrors(DockerErrorCode.DIGEST_INVALID, "Invalid manifest digest reference."), apiHeader);

            DockerImage Image = await Database.FindFirstIgnoreRest<DockerImage>(new FilterAnd(
                new FilterFieldEqualTo("Digest", Digest)));


            if (Image is null)
                throw new NotFoundException(new DockerErrors(DockerErrorCode.NAME_INVALID, "Manifest unknown."), apiHeader);

            await Handle.Storage.UnregisterImage(Image.Manifest);

            await Database.Delete(Image);

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

            IImageManifest Manifest;

            if (ManifestContentResponse.Decoded is OCIImageManifest OciManifest)
                Manifest = OciManifest;
            else if (ManifestContentResponse.Decoded is DockerImageManifestV2 DockerManifestV2)
                Manifest = DockerManifestV2;
            else
                throw new BadRequestException(new DockerErrors(DockerErrorCode.MANIFEST_INVALID, "Manifest invalid."), apiHeader);

            foreach (IImageLayer Layer in Manifest.GetLayers())
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

            if (HashDigest.TryParseDigest(Reference, out HashDigest Digest))
            {
                if (!(Digest != Manifest.GetConfig().Digest))
                    throw new BadRequestException(new DockerErrors(DockerErrorCode.MANIFEST_INVALID, "Manifest invalid. Digest mismatch."), apiHeader);
            }
            else
                Tag = Reference;

            DockerImage Image;

            if (string.IsNullOrEmpty(Tag))
            {
                Image = await Database.FindFirstIgnoreRest<DockerImage>(new FilterAnd(
                    new FilterFieldEqualTo("RepositoryName", Repository.RepositoryName),
                    new FilterFieldEqualTo("Digest", Digest)));

                if (!(Image is null))
                    Tag = Image.Tag;
                else
                    throw new BadRequestException(new DockerErrors(DockerErrorCode.MANIFEST_INVALID, "Manifest invalid. Missing tag."), apiHeader);
            }
            else
            {
                Image = await Database.FindFirstIgnoreRest<DockerImage>(new FilterAnd(
                    new FilterFieldEqualTo("RepositoryName", Repository.RepositoryName),
                    new FilterFieldEqualTo("Tag", Tag)));
            }

            DockerActor Owner = await Repository.GetOwner();
            await using WritableStorageHandle Handle = await Owner.GetWritableStorage();

            if (Image is null)
            {
                Image = new DockerImage()
                {
                    RepositoryName = Repository.RepositoryName,
                    Tag = Tag,
                    Manifest = Manifest,
                    Digest = new HashDigest(HashFunction.SHA256, Manifest.Raw),
                };

                await Database.Insert(Image);
            }
            else
            {
                await Handle.Storage.UnregisterImage(Image.Manifest);

                if (string.IsNullOrEmpty(Tag))
                    Tag = Image.Tag;
                else
                    Image.Tag = Tag;

                byte[] Data = await Request.ReadDataAsync();

                Image.Manifest = Manifest;
                Image.Digest = new HashDigest(HashFunction.SHA256, Manifest.Raw);

                await Database.Update(Image);
            }

            await Handle.Storage.RegistrerImage(Image.Manifest);

            if (Handle.Storage.MaxStorage - Handle.Storage.UsedStorage < 0)
            {
                await Handle.Storage.UnregisterImage(Image.Manifest);
                await Database.Delete(Image);
                throw new ForbiddenException(new DockerErrors(DockerErrorCode.DENIED, "Storage quota exceeded."), apiHeader);
            }


            await Database.FindDelete<DanglingDockerBlob>(new FilterAnd(new FilterFieldEqualTo("Digest", Image.Manifest.GetConfig().Digest)));

            foreach (IImageLayer Layer in Manifest.GetLayers())
            {
                await Database.FindDelete<DanglingDockerBlob>(new FilterAnd(new FilterFieldEqualTo("Digest", Layer.Digest)));
            }

            Log.Informational("Docker image uploaded.", Image.RepositoryName, Request.User.UserName,
                await LoginAuditor.Annotate(Request.RemoteEndPoint,
                new KeyValuePair<string, object>("Tag", Image.Tag),
                new KeyValuePair<string, object>("Digest", Image.Digest.ToString()),
                new KeyValuePair<string, object>("RemoteEndPoint", Request.RemoteEndPoint)));

            Response.StatusCode = 201;
            Response.StatusMessage = "Created";
            Response.SetHeader("Docker-Content-Digest", new HashDigest(HashFunction.SHA256, Image.Manifest.Raw).ToString());
            await Response.SendResponse();
            return;
        }
    }
}
