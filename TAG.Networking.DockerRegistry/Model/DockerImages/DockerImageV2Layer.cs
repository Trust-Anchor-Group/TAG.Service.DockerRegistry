using System;
using System.Collections.Generic;

namespace TAG.Networking.DockerRegistry.Model.DockerImages
{
    public class DockerImageV2Layer : IImageLayer
    {
        public string MediaType { get; set; }

        public long Size { get; set; }

        public HashDigest Digest { get; set; }

        public string[] Urls { get; set; }

        public DockerImageV2Layer()
        {

        }

        public DockerImageV2Layer(Dictionary<string, object> Dict)
        {
            DockerImageV2ContentDescriptor Descriptor = DockerImageV2ContentDescriptor.Parse(Dict);

            if (!(
                Descriptor.MediaType == "application/vnd.docker.image.rootfs.diff.tar" ||
                Descriptor.MediaType == "application/vnd.docker.image.rootfs.diff.tar.gzip" ||
                Descriptor.MediaType == "application/vnd.docker.image.rootfs.foreign.diff.tar.gzip"
            ))
                throw new Exception("Invalid media type");

            this.MediaType = Descriptor.MediaType;
            this.Size = Descriptor.Size;
            this.Digest = Descriptor.Digest;

            if (Dict.TryGetValue("urls", out object UrlsObj) && UrlsObj is object[] UrlsArray)
            {
                List<string> UrlsList = new List<string>();
                foreach (object Url in UrlsArray)
                {
                    if (Url is string s)
                        UrlsList.Add(s);
                }
                Urls = UrlsList.ToArray();
            }
        }
    }
}
