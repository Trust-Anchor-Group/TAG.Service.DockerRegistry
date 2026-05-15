using System;

namespace TAG.Networking.DockerRegistry.Model
{
    public class ReadOnlyDockerStorage
    {
        private DockerStorage storage;
        public string ObjectId => storage.ObjectId;
        public Guid Guid => storage.Guid;
        public ReferenceCounter[] BlobCounter => storage.BlobCounter;
        public long MaxStorage => storage.MaxStorage;
        public long UsedStorage => storage.UsedStorage;
        public ReadOnlyDockerStorage(DockerStorage Storage)
        {
            this.storage = Storage;
        }
    }
}