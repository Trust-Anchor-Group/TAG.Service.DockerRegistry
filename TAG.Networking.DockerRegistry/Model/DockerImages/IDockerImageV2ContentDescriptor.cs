namespace TAG.Networking.DockerRegistry.Model.DockerImages
{
	internal interface IDockerImageV2ContentDescriptor
	{
		public string MediaType { get; set; }
		public long Size { get; set; }
		public HashDigest Digest { get; set; }
	}
}
