using System;
using System.Threading;

namespace TAG.Networking.DockerRegistry.Model
{
    public class ReadOnlyStorageHandle : IDisposable
    {
        // Semaphore coming from Waher.Runtime.Threading.Semaphore
        private Waher.Runtime.Threading.Semaphore semaphore;
        public ReadOnlyDockerStorage Storage { get; }

        public ReadOnlyStorageHandle(DockerStorage Storage, Waher.Runtime.Threading.Semaphore Semaphore)
        {
            this.Storage = new ReadOnlyDockerStorage(Storage);
            this.semaphore = Semaphore;
        }

        // Standard dispose pattern with finalizer to avoid leaving semaphore held.
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            Waher.Runtime.Threading.Semaphore s = Interlocked.Exchange(ref this.semaphore, null);
            s?.Dispose();
        }

        ~ReadOnlyStorageHandle()
        {
            Dispose(false);
        }
    }
}
