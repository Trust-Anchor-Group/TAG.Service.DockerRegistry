namespace TAG.Networking.DockerRegistry.Model
{
	public interface IImageManifest : IManifest
	{
		public IImageLayer[] GetLayers();
		public IImageConfig GetConfig();
	}
}
