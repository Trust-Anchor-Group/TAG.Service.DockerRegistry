using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Model;
using Waher.Runtime.Threading;

namespace TAG.Networking.DockerRegistry
{
    internal static class RegistrySemaphores
    {
        public static Task<Semaphore> BeginRead(HashDigest Digest)
        {
            return Semaphores.BeginRead($"TAG.Networking.DockerRegistry.{Digest}");
        }

        public static Task<Semaphore> BeginRead(string Repository, string Tag)
        {
            return Semaphores.BeginRead($"TAG.Networking.DockerRegistry.{Repository}.{Tag}");
        }

        public static Task<Semaphore> BeginWrite(HashDigest Digest)
        {
            return Semaphores.BeginWrite($"TAG.Networking.DockerRegistry.{Digest}");
        }

        public static Task<Semaphore> BeginWrite(string Repository, string Tag)
        {
            return Semaphores.BeginWrite($"TAG.Networking.DockerRegistry.{Repository}.{Tag}");
        }
    }
}