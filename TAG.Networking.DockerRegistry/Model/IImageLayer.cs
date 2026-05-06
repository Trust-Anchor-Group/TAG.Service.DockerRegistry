namespace TAG.Networking.DockerRegistry.Model
{
	public interface IImageLayer
	{
		public string MediaType { get; }
		public long Size { get; }
		public HashDigest Digest { get; }
		public string[] Urls { get; }
	}
}
