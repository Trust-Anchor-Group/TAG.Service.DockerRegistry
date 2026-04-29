using TAG.Networking.DockerRegistry.Model;

namespace TAG.Networking.DockerRegistry
{
    public class ReferencedManifest
    {
        public IManifest Manifest;
        public long Size;
        public string RepositoryName;
        public string Tag;
        public HashDigest Digest;
    }
}
