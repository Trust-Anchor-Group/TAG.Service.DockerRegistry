using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using TAG.Networking.DockerRegistry.Errors;
using TAG.Networking.DockerRegistry.Model;
using Waher.Networking.HTTP;
using Waher.Networking.Sniffers;
using Waher.Persistence;
using Waher.Runtime.Cache;

namespace TAG.Networking.DockerRegistry.Endpoints
{
    internal class BlobUploadEndpoints : DockerEndpoints
    {
        private readonly Cache<Guid, BlobUpload> uploads = new Cache<Guid, BlobUpload>(int.MaxValue, TimeSpan.MaxValue, TimeSpan.FromHours(1));
        private readonly string dockerRegistryFolder;

        /// <summary>
        /// Folder where BLOBs are uploaded to.
        /// </summary>
        public string UploadFolder
        {
            get
            {
                string UploadFolder = Path.Combine(this.dockerRegistryFolder, "Uploads");

                if (!Directory.Exists(UploadFolder))
                    Directory.CreateDirectory(UploadFolder);

                return UploadFolder;
            }
        }

        public BlobUploadEndpoints(string DockerRegistryFolder, ManifestManager ManifestManager, BlobManager BlobManager, ISniffer[] Sniffers)
            : base(ManifestManager, BlobManager, Sniffers)
        {
            this.uploads.Removed += this.Uploads_Removed;
            this.dockerRegistryFolder = DockerRegistryFolder;
        }

        public async Task GET(HttpRequest Request, HttpResponse Response, ByteRangeInterval Interval, DockerActor Actor, DockerRepository Repository, string Reference)
        {
            await AssertRepositoryPrivilages(Actor, Repository, DockerRepository.RepositoryAction.Push, Request);

            Guid Uuid = Guid.Parse(Reference);

            if (!this.uploads.TryGetValue(Uuid, out BlobUpload UploadRecord))
                throw new NotFoundException(new DockerErrors(DockerErrorCode.BLOB_UPLOAD_UNKNOWN, "BLOB upload unknown to registry."), apiHeader);

            if (UploadRecord.UserName != Request.User.UserName)
                throw new ForbiddenException(new DockerErrors(DockerErrorCode.DENIED, "Requested access to the resource is denied."), apiHeader);

            await UploadRecord.Lock();
            try
            {
                Response.SetHeader("Location", "/v2/" + Repository.RepositoryName + "blobs/uploads/" + Uuid.ToString());
                Response.SetHeader("Docker-Upload-UUID", Uuid.ToString());

                if (UploadRecord.File is null || UploadRecord.File.Length == 0)
                {
                    Response.StatusCode = 204;
                    Response.StatusMessage = "No Content";
                    Response.SetHeader("Content-Range", "0-0/0");
                }
                else
                {
                    long Offset = Interval?.First ?? 0L;
                    long Count;

                    Count = UploadRecord.File.Length;

                    Response.StatusCode = 200;
                    Response.SetHeader("Content-Range", Offset.ToString() + "-" +
                        (Offset + Count - 1).ToString() + "/" + UploadRecord.File.Length.ToString());

                    await WriteToResponse(Response, UploadRecord.File, Offset, Count);
                }
            }
            finally
            {
                UploadRecord.Release();
            }

            await Response.SendResponse();
            return;
        }

        public async Task POST(HttpRequest Request, HttpResponse Response, DockerActor Actor, DockerRepository Repository, string Reference)
        {
            await AssertRepositoryPrivilages(Actor, Repository, DockerRepository.RepositoryAction.Push, Request);

            using ReadOnlyStorageHandle Handle = await Actor.GetReadOnlyStorage();

            if (Handle.Storage.UsedStorage >= Handle.Storage.MaxStorage)
                throw new ForbiddenException(new DockerErrors(DockerErrorCode.DENIED, "Storage quota exceeded."), apiHeader);

            Guid Uuid = Guid.NewGuid();

            this.uploads[Uuid] = new BlobUpload(Uuid, Request.User.UserName);

            Response.StatusCode = 202;
            Response.StatusMessage = "Accepted";
            Response.SetHeader("Location", "/v2/" + Repository.RepositoryName + "/blobs/uploads/" + Uuid.ToString());
            Response.SetHeader("Range", "0-0");
            Response.SetHeader("Docker-Upload-UUID", Uuid.ToString());
            await Response.SendResponse();
        }

        public async Task DELETE(HttpRequest Request, HttpResponse Response, DockerActor Actor, DockerRepository Repository, string Reference)
        {
            await AssertRepositoryPrivilages(Actor, Repository, DockerRepository.RepositoryAction.Delete, Request);

            if (string.IsNullOrEmpty(Reference) || !Guid.TryParse(Reference, out Guid Uuid))
                throw new BadRequestException(new DockerErrors(DockerErrorCode.BLOB_UPLOAD_INVALID, "BLOB upload invalid."), apiHeader);

            if (!this.uploads.TryGetValue(Uuid, out BlobUpload UploadRecord))
                throw new NotFoundException(new DockerErrors(DockerErrorCode.BLOB_UPLOAD_UNKNOWN, "BLOB upload unknown to registry."), apiHeader);

            this.uploads.Remove(Uuid);

            Response.StatusCode = 200;
            await Response.SendResponse();
            return;
        }

        public async Task PATCH(HttpRequest Request, HttpResponse Response, ContentByteRangeInterval Interval, DockerActor Actor, DockerRepository Repository, string Reference)
        {
            await AssertRepositoryPrivilages(Actor, Repository, DockerRepository.RepositoryAction.Push, Request);

            if (!Guid.TryParse(Reference, out Guid Uuid) || !Request.HasData)
                throw new BadRequestException(new DockerErrors(DockerErrorCode.BLOB_UPLOAD_INVALID, "BLOB upload invalid."), apiHeader);

            if (!this.uploads.TryGetValue(Uuid, out BlobUpload UploadRecord))
                throw new NotFoundException(new DockerErrors(DockerErrorCode.BLOB_UPLOAD_UNKNOWN, "BLOB upload unknown to registry."), apiHeader);

            await UploadRecord.Lock();
            try
            {
                await this.CopyToBlobLocked(Request, Interval, UploadRecord, Uuid);

                Response.StatusCode = 202;
                Response.StatusMessage = "Accepted";
                Response.SetHeader("Location", "/v2/" + Repository.RepositoryName + "/blobs/uploads/" + Uuid.ToString());
                Response.SetHeader("Range", "0-" + UploadRecord.File.Length.ToString());
                Response.SetHeader("Docker-Upload-UUID", Uuid.ToString());
                await Response.SendResponse();
                return;
            }
            finally
            {
                UploadRecord.Release();
            }
        }

        public async Task PUT(HttpRequest Request, HttpResponse Response, ContentByteRangeInterval Interval, DockerActor Actor, DockerRepository Repository, string Reference)
        {
            await AssertRepositoryPrivilages(Actor, Repository, DockerRepository.RepositoryAction.Push, Request);

            if (string.IsNullOrEmpty(Reference) || !Guid.TryParse(Reference, out Guid Uuid))
                throw new BadRequestException(new DockerErrors(DockerErrorCode.BLOB_UPLOAD_INVALID, "BLOB upload invalid."), apiHeader);

            if (!this.uploads.TryGetValue(Uuid, out BlobUpload UploadRecord))
                throw new NotFoundException(new DockerErrors(DockerErrorCode.BLOB_UPLOAD_UNKNOWN, "BLOB upload unknown to registry."), apiHeader);

            await using WritableStorageHandle Handle = await Actor.GetWritableStorage();

            HashDigest Digest;

            await UploadRecord.Lock();
            try
            {
                if (Request.HasData)
                    await this.CopyToBlobLocked(Request, Interval, UploadRecord, Uuid);

                if (!Request.Header.TryGetQueryParameter("digest", out string DigestStr) ||
                    !HashDigest.TryParseDigest(DigestStr = HttpUtility.UrlDecode(DigestStr), out Digest))
                    throw new BadRequestException(new DockerErrors(DockerErrorCode.DIGEST_INVALID, "Provided digest did not match uploaded content."), apiHeader);


                HashDigest ComputedDigest = UploadRecord.ComputeDigestLocked(Digest.HashFunction);
                if (ComputedDigest != Digest)
                    throw new BadRequestException(new DockerErrors(DockerErrorCode.DIGEST_INVALID, "Provided digest did not match uploaded content."), apiHeader);

                DanglingDockerBlob Dangling = new DanglingDockerBlob()
                {
                    Created = DateTime.Now,
                    Digest = Digest,
                    Owner = Actor.Guid,
                    Size = UploadRecord.File.Length
                };

                await Database.Insert(Dangling);

                bool Created = await this.blobManager.UploadComplete(UploadRecord);

                if (!Created)
                {
                    await Database.Delete(Dangling);
                    throw new ForbiddenException(new DockerErrors(DockerErrorCode.DENIED, "BLOB already exists."), apiHeader);
                }

                await Handle.Storage.RegisterDanglingBlob(Dangling);

                Response.StatusCode = 201;
                Response.StatusMessage = "Created";
                Response.SetHeader("Location", "/v2/" + Repository.RepositoryName + "/blobs/uploads/" + Uuid.ToString());
                Response.SetHeader("Docker-Content-Digest", Digest.ToString());
                await Response.SendResponse();
            }
            finally
            {
                UploadRecord.Release();
            }

            this.uploads.Remove(Uuid);

            Response.StatusCode = 201;
            Response.StatusMessage = "Created";
            Response.SetHeader("Location", "/v2/" + Repository.RepositoryName + "/blobs/uploads/" + Uuid.ToString());
            Response.SetHeader("Docker-Content-Digest", Digest.ToString());
            await Response.SendResponse();
            return;
        }

        private async Task CopyToBlobLocked(HttpRequest Request, ContentByteRangeInterval Interval, BlobUpload UploadRecord, Guid Uuid)
        {
            Request.DataStream.Position = 0;

            long Offset = Interval?.First ?? 0L;
            long Count = Interval is null ? Request.DataStream.Length : Interval.Last - Interval.First + 1;

            if (UploadRecord.Blob is null)
            {
                UploadRecord.Blob = new DockerBlob();
                UploadRecord.FileName = Path.Combine(this.UploadFolder, Uuid.ToString() + ".bin");

                if (File.Exists(UploadRecord.FileName))
                    UploadRecord.File = File.OpenWrite(UploadRecord.FileName);
                else
                    UploadRecord.File = File.Create(UploadRecord.FileName);
            }

            if (UploadRecord.File.Length < Offset)
            {
                byte[] Buf = new byte[65536];

                UploadRecord.File.Position = UploadRecord.File.Length;

                while (UploadRecord.File.Length < Offset)
                {
                    int NrBytes = (int)Math.Min(65536, Offset - UploadRecord.File.Length);
                    await UploadRecord.File.WriteAsync(Buf, 0, NrBytes);
                }
            }
            else
                UploadRecord.File.Position = Offset;

            while (Count > 0)
            {
                int NrBytes = (int)Math.Min(65536, Count);
                await Request.DataStream.CopyToAsync(UploadRecord.File, NrBytes);
                Count -= NrBytes;
            }
        }

        private Task Uploads_Removed(object Sender, CacheItemEventArgs<Guid, BlobUpload> e)
        {
            e.Value.Dispose();
            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            this.uploads.Clear();
            this.uploads.Dispose();
        }
    }
}