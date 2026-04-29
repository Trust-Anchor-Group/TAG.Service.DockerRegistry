namespace TAG.Networking.DockerRegistry.Model
{
    public interface IManifest
    {
        public int SchemaVersion { get; }
        public string MediaType { get; }
        public byte[] Raw { get; set; }
        public HashDigest Digest { get; set; }
    }
}
