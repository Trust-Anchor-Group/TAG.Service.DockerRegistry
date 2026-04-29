
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Waher.Persistence;
using Waher.Persistence.Attributes;
using Waher.Persistence.Filters;
using Waher.Runtime.Threading;

namespace TAG.Networking.DockerRegistry.Model
{
    [CollectionName("DockerActor")]
    [TypeName(TypeNameSerialization.FullName)]
    [Index("Guid")]
    [Index("StorageGuid")]
    public abstract class DockerActor
    {
        /// <summary>
        /// Object ID
        /// </summary>
        [ObjectId]
        public string ObjectId { get; set; }

        /// <summary>
        /// Actor Guid
        /// </summary>
        public Guid Guid { get; set; }

        /// <summary>
        /// Docker storage guid
        /// </summary>
        public Guid StorageGuid { get; set; }

        /// <summary>
        /// Docker storage guid
        /// </summary>
        public ActorOptions Options;

        public DockerActor()
        {
            this.Options = new ActorOptions();
        }

        public async Task<WritableStorageHandle> GetWritableStorage()
        {
            Waher.Runtime.Threading.Semaphore Semaphore = await Semaphores.BeginWrite("DockerRegistry_StorageAffecting_" + Guid);
            DockerStorage Storage = await Database.FindFirstIgnoreRest<DockerStorage>(new FilterAnd(new FilterFieldEqualTo("Guid", StorageGuid)));
            return new WritableStorageHandle(Storage, Semaphore);
        }

        public async Task<ReadOnlyStorageHandle> GetReadOnlyStorage()
        {
            Waher.Runtime.Threading.Semaphore Semaphore = await Semaphores.BeginRead("DockerRegistry_StorageAffecting_" + Guid);
            DockerStorage Storage = await Database.FindFirstIgnoreRest<DockerStorage>(new FilterAnd(new FilterFieldEqualTo("Guid", StorageGuid)));
            return new ReadOnlyStorageHandle(Storage, Semaphore);
        }

        public async Task<DockerStorage> GetStorageNonBlocking()
        {
            DockerStorage Storage = await Database.FindFirstIgnoreRest<DockerStorage>(new FilterAnd(new FilterFieldEqualTo("Guid", StorageGuid)));
            return Storage;
        }

        public async Task<DockerManifest[]> FindOwnedImages()
        {
            DockerRepository[] Repositories = (await Database.Find<DockerRepository>(new FilterAnd(new FilterFieldEqualTo("OwnerGuid", Guid)))).ToArray();

            List<DockerManifest> DockerImages = new List<DockerManifest>();
            foreach (DockerRepository Repository in Repositories)
            {
                DockerManifest[] Images = (await Database.Find<DockerManifest>(new FilterFieldEqualTo("RepositoryName", Repository.RepositoryName))).ToArray();
                DockerImages.AddRange(Images);
            }

            return DockerImages.ToArray();
        }

        public async Task ReSyncStorage()
        {
            await using WritableStorageHandle StorageHandle = await GetWritableStorage();
            if (StorageHandle is null)
                return;

            IImageManifest[] Images = (await FindOwnedImages())
                .Where(x => x.Manifest is IImageManifest)
                .Select(x => x.Manifest)
                .Cast<IImageManifest>()
                .ToArray();

            StorageHandle.Storage.UsedStorage = 0;
            StorageHandle.Storage.BlobCounter = new DigestReferenceCounter[0];

            foreach (IImageManifest Image in Images)
            {
                await StorageHandle.Storage.RegisterManifest(Image);
            }
        }
    }
}
