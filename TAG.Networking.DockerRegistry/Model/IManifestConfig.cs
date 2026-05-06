namespace TAG.Networking.DockerRegistry.Model
{
	public interface IImageConfig
	{
		public string MediaType { get; }
		public long Size { get; }
		public HashDigest Digest { get; }
	}
}
