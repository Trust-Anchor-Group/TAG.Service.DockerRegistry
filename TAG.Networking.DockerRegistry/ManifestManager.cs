using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Model;
using Waher.Persistence;
using Waher.Persistence.Filters;
using Waher.Runtime.Threading;

namespace TAG.Networking.DockerRegistry
{
    internal class ManifestManager
    {
        private BlobManager blobManager;

        public ManifestManager(BlobManager BlobManager)
        {
            this.blobManager = BlobManager;
        }

        public async Task<IManifest?> FindManifest(string RepositoryName, string Tag)
        {
            ImageReference? ImageReference = await Database.FindFirstDeleteRest<ImageReference>(new FilterAnd(
                new FilterFieldEqualTo(nameof(ImageReference.RepositoryName), RepositoryName),
                new FilterFieldEqualTo(nameof(ImageReference.Tag), Tag)));

            if (ImageReference is null)
                return null;

            IManifest? Found = await this.FindManifest(ImageReference.Digest);

            if (Found is null)
            {
                await Database.Delete(ImageReference);
            }

            return Found;
        }

        public async Task<IManifest?> FindManifest(HashDigest Digest)
        {
            using Semaphore Semaphore = await RegistrySemaphores.BeginRead(Digest);
            DockerManifest? Manifest = await Database.FindFirstIgnoreRest<DockerManifest>(new FilterAnd(
                new FilterFieldEqualTo(nameof(DockerManifest.Digest), Digest)
            ));

            return Manifest?.Manifest;
        }

        public async Task CreateManifestTag(HashDigest ManifestDigest, string Repository, string Tag, WritableStorageHandle StorageWriter)
        {
            using Semaphore Semaphore = await RegistrySemaphores.BeginWrite(Repository, Tag);
            IManifest? Manifest = await this.FindManifest(Repository, Tag);

            if (!(Manifest is null))
            {
                await DeleteManifest(Manifest.Digest, StorageWriter);
            }

            ImageReference ImageReference = new ImageReference()
            {
                Digest = ManifestDigest,
                RepositoryName = Repository,
                Tag = Tag,
            };

            await this.IncrementReference(ManifestDigest);

            await Database.Insert(ImageReference);

            return;
        }

        public async Task<bool> TryCreateManifest(IManifest Manifest, WritableStorageHandle StorageWriter)
        {
            using Semaphore Semaphore = await RegistrySemaphores.BeginWrite(Manifest.Digest);

            HashDigest[] References = this.GetReferences(Manifest);

            if (!await StorageWriter.Storage.TryIncrementReferences(References))
                return false;

            DockerManifest Stored = new DockerManifest()
            {
                Digest = Manifest.Digest,
                Manifest = Manifest,
                ReferenceCount = 0
            };

            foreach (HashDigest Digest in References)
                await Database.FindDelete<DanglingDockerBlob>(new FilterAnd(new FilterFieldEqualTo("Digest", Digest)));


            await Database.Insert(Stored);
            return true;
        }

        public async Task DeleteManifest(HashDigest Digest, WritableStorageHandle StorageWriter)
        {
            using Semaphore Semaphore = await RegistrySemaphores.BeginWrite(Digest);

            DockerManifest? Manifest = await Database.FindFirstIgnoreRest<DockerManifest>(new FilterAnd(
                new FilterFieldEqualTo(nameof(DockerManifest.Digest), Digest)
            ));

            if (Manifest is null)
                return;

            HashDigest[] References = this.GetReferences(Manifest.Manifest);
            Task.WaitAll(References.Select(x => this.DecrementReference(x)).ToArray());

            await StorageWriter.Storage.DecrementReferences(References);

            await Database.Delete(Manifest);
        }

        private async Task ChangeReference(HashDigest Digest, int Count)
        {
            using Semaphore Semaphore = await RegistrySemaphores.BeginWrite(Digest);

            ReferenceCounter? Counter = await Database.FindFirstIgnoreRest<ReferenceCounter>(new FilterAnd(
                new FilterFieldEqualTo(nameof(ReferenceCounter.Digest), Digest)
            ));

            if (Counter is null)
                return;

            Counter.ReferenceCount += Count;

            await Database.Update(Counter);
        }

        private Task DecrementReference(HashDigest Digest)
        {
            return this.ChangeReference(Digest, -1);
        }

        private Task IncrementReference(HashDigest Digest)
        {
            return this.ChangeReference(Digest, 1);
        }

        private HashDigest[] GetReferences(IManifest Manifest)
        {
            if (Manifest is IImageManifest ImageManifest)
            {
                return this.GetReferences(ImageManifest);
            }
            else if (Manifest is IIndexManifest IndexManifest)
            {
                return this.GetReferences(IndexManifest);

            }
            return new HashDigest[0];
        }

        private HashDigest[] GetReferences(IImageManifest Manifest)
        {
            List<HashDigest> References = Manifest.GetLayers().Select(x => x.Digest).ToList();
            References.Add(Manifest.GetConfig().Digest);
            return References.ToArray();
        }

        private HashDigest[] GetReferences(IIndexManifest Manifest)
        {
            return Manifest.GetManifests().Select(x => x.Digest).ToArray();
        }
    }
}
