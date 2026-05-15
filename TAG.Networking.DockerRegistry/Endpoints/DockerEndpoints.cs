using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Errors;
using TAG.Networking.DockerRegistry.Model;
using Waher.Networking.HTTP;
using Waher.Networking.Sniffers;

namespace TAG.Networking.DockerRegistry.Endpoints
{
    internal abstract class DockerEndpoints : IDockerEndpoints
    {
        protected static readonly KeyValuePair<string, string> apiHeader = new KeyValuePair<string, string>("Docker-Distribution-API-Version", "registry/2.0");
        protected ManifestManager manifestManager;
        protected BlobManager blobManager;

        private ISniffer[] sniffers;
        public DockerEndpoints(ManifestManager ManifestManager, BlobManager BlobManager, ISniffer[] Sniffers)
        {
            this.blobManager = BlobManager;
            this.manifestManager = ManifestManager;
            this.sniffers = Sniffers;
        }

        protected async Task AssertRepositoryPrivilages(DockerActor Actor, DockerRepository Repository, DockerRepository.RepositoryAction Action, HttpRequest Request)
        {
            bool HasPermission = await Repository.HasPermission(Actor, Action);
            if (!HasPermission)
                throw new ForbiddenException(new DockerErrors(DockerErrorCode.DENIED, "Requested access to the resource is denied."), apiHeader);
            Sniff(Request, Actor);
        }

        protected static async Task WriteToResponse(HttpResponse Response, FileStream File, long Offset, long Count)
        {
            if (!Response.OnlyHeader)
            {
                File.Position = Offset;

                byte[] Buf = new byte[65536];
                int NrBytes;

                while (Count > 0)
                {
                    NrBytes = (int)Math.Min(65536, Count);

                    await File.ReadAsync(Buf, 0, NrBytes);
                    await Response.Write(false, Buf, 0, NrBytes);

                    Count -= NrBytes;
                }
            }
        }

        protected void Sniff(HttpRequest Request, DockerActor Actor)
        {
            if (Request.Header.Method == "HEAD")
                return;

            if (this.sniffers != null)
            {
                foreach (ISniffer Sniffer in this.sniffers)
                {
                    StringBuilder Builder = new StringBuilder();
                    Builder.Append("Docker Registry Request: ");
                    Builder.AppendLine();
                    Builder.Append("Resource: ");
                    Builder.Append(Request.SubPath);
                    Builder.AppendLine();
                    Builder.Append("Method: ");
                    Builder.Append(Request.Header.Method);
                    Builder.AppendLine();

                    Sniffer.ReceiveText(Builder.ToString());
                }
            }
        }

        public virtual void Dispose()
        {

        }
    }
}
