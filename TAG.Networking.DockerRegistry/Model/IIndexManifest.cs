namespace TAG.Networking.DockerRegistry.Model
{
    public interface IIndexManifest : IManifest
    {
        public IContentDescriptor[] GetManifests();
    }
}
