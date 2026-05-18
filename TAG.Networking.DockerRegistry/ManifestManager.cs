using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Model;
using Waher.Persistence;
using Waher.Persistence.Filters;
using Waher.Runtime.Threading;

namespace TAG.Networking.DockerRegistry
{
    public class ManifestManager
    {
        private BlobManager blobManager;

        public ManifestManager(BlobManager BlobManager)
        {
            this.blobManager = BlobManager;
        }

        public async Task<IManifest?> FindManifest(string RepositoryName, string Tag)
        {
            using Semaphore Semaphore = await RegistrySemaphores.BeginRead(RepositoryName, Tag);
            ImageReference? ImageReference = await Database.FindFirstIgnoreRest<ImageReference>(new FilterAnd(
                new FilterFieldEqualTo(nameof(ImageReference.RepositoryName), RepositoryName),
                new FilterFieldEqualTo(nameof(ImageReference.Tag), Tag)));

            if (ImageReference is null)
                return null;

            return await this.FindManifest(ImageReference.Digest);
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
            ImageReference? ExistingReference = await this.FindImageReference(Repository, Tag);

            if (!(ExistingReference is null))
            {
                if (ExistingReference.Digest == ManifestDigest)
                    return;

                await this.DeleteImageReferenceLocked(ExistingReference, StorageWriter);
            }

            ImageReference ImageReference = new ImageReference()
            {
                Digest = ManifestDigest,
                RepositoryName = Repository,
                Tag = Tag,
            };

            await this.IncrementReference(ManifestDigest);

            await Database.Insert(ImageReference);
        }

        public async Task<bool> DeleteImageReference(string RepositoryName, string Tag, WritableStorageHandle StorageWriter)
        {
            using Semaphore RepositorySemaphore = await RegistrySemaphores.BeginWrite(RepositoryName, Tag);
            ImageReference? ImageReference = await this.FindImageReference(RepositoryName, Tag);

            if (ImageReference is null)
                return false;

            await this.DeleteImageReferenceLocked(ImageReference, StorageWriter);

            return true;
        }

        public async Task<bool> TryCreateManifest(IManifest Manifest, Guid Owner, WritableStorageHandle StorageWriter)
        {
            using Semaphore Semaphore = await RegistrySemaphores.BeginWrite(Manifest.Digest);

            if (!(await this.GetStoredManifest(Manifest.Digest) is null))
                return true;

            HashDigest[] References = this.GetReferences(Manifest);

            if (!await StorageWriter.Storage.TryIncrementReferences(References))
                return false;

            List<HashDigest> IncrementedReferences = new List<HashDigest>();

            try
            {
                foreach (HashDigest Digest in References)
                {
                    await this.IncrementReference(Digest);
                    IncrementedReferences.Add(Digest);
                }

                DockerManifest Stored = new DockerManifest()
                {
                    Digest = Manifest.Digest,
                    Manifest = Manifest,
                    ReferenceCount = 0
                };

                foreach (HashDigest Digest in References)
                    await this.DeleteDanglingBlobs(Digest, Owner);


                await Database.Insert(Stored);
                return true;
            }
            catch
            {
                await StorageWriter.Storage.DecrementReferences(References);
                await Task.WhenAll(IncrementedReferences.Select(Digest => this.DecrementReference(Digest, StorageWriter)));
                throw;
            }
        }

        public async Task DeleteManifest(HashDigest Digest, WritableStorageHandle StorageWriter)
        {
            while (true)
            {
                ImageReference? ImageReference = await Database.FindFirstIgnoreRest<ImageReference>(new FilterAnd(
                    new FilterFieldEqualTo(nameof(ImageReference.Digest), Digest)));

                if (ImageReference is null)
                    break;

                await this.DeleteImageReference(ImageReference.RepositoryName, ImageReference.Tag, StorageWriter);
            }

            using Semaphore Semaphore = await RegistrySemaphores.BeginWrite(Digest);

            DockerManifest? Manifest = await this.GetStoredManifest(Digest);

            if (Manifest is null)
                return;

            if (Manifest.HasReferences)
                return;

            await this.DeleteManifestLocked(Manifest, StorageWriter);
        }

        private async Task DeleteManifestLocked(DockerManifest Manifest, WritableStorageHandle StorageWriter)
        {
            HashDigest[] References = this.GetReferences(Manifest.Manifest);

            await StorageWriter.Storage.DecrementReferences(References);
            await Task.WhenAll(References.Select(Digest => this.DecrementReference(Digest, StorageWriter)));
            await Database.Delete(Manifest);
        }

        private async Task DeleteImageReferenceLocked(ImageReference ImageReference, WritableStorageHandle StorageWriter)
        {
            await Database.Delete(ImageReference);
            await this.DecrementReference(ImageReference.Digest, StorageWriter);
        }

        private async Task<DockerManifest?> GetStoredManifest(HashDigest Digest)
        {
            return await Database.FindFirstIgnoreRest<DockerManifest>(new FilterAnd(
                new FilterFieldEqualTo(nameof(DockerManifest.Digest), Digest)
            ));
        }

        private async Task<ImageReference?> FindImageReference(string RepositoryName, string Tag)
        {
            return await Database.FindFirstDeleteRest<ImageReference>(new FilterAnd(
                new FilterFieldEqualTo(nameof(ImageReference.RepositoryName), RepositoryName),
                new FilterFieldEqualTo(nameof(ImageReference.Tag), Tag)));
        }

        private async Task DeleteDanglingBlobs(HashDigest Digest, Guid Owner)
        {
            IEnumerable<DanglingDockerBlob> DanglingBlobs = await Database.Find<DanglingDockerBlob>(new FilterAnd(
                new FilterFieldEqualTo(nameof(DanglingDockerBlob.Digest), Digest),
                new FilterFieldEqualTo(nameof(DanglingDockerBlob.Owner), Owner)));

            foreach (DanglingDockerBlob DanglingBlob in DanglingBlobs)
                await Database.Delete(DanglingBlob);
        }

        private async Task ChangeReference(HashDigest Digest, int Count)
        {
            using Semaphore Semaphore = await RegistrySemaphores.BeginWrite(Digest);
            await this.ChangeReferenceLocked(Digest, Count, null);
        }

        private async Task ChangeReference(HashDigest Digest, int Count, WritableStorageHandle StorageWriter)
        {
            using Semaphore Semaphore = await RegistrySemaphores.BeginWrite(Digest);
            await this.ChangeReferenceLocked(Digest, Count, StorageWriter);
        }

        private async Task ChangeReferenceLocked(HashDigest Digest, int Count, WritableStorageHandle? StorageWriter)
        {

            ReferenceCounted? Counter = await Database.FindFirstIgnoreRest<ReferenceCounted>(new FilterAnd(
                new FilterFieldEqualTo(nameof(ReferenceCounted.Digest), Digest)
            ));

            if (Counter is null)
                throw new InvalidOperationException("Referenced object does not exist.");

            bool shouldDelete = Counter.ChangeReferenceCount(Count);

            if (!shouldDelete)
            {
                await Database.Update(Counter);
                return;
            }

            if (StorageWriter is null)
                throw new InvalidOperationException("Storage writer required when deleting an unreferenced object.");

            await this.DeleteReferenceCounterLocked(Counter, StorageWriter);
        }

        private async Task DeleteReferenceCounterLocked(ReferenceCounted Counter, WritableStorageHandle StorageWriter)
        {
            switch (Counter)
            {
                case DockerManifest Manifest:
                    await this.DeleteManifestLocked(Manifest, StorageWriter);
                    break;

                case DockerBlob Blob:
                    await this.blobManager.DeleteBlob(Blob.Digest);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown reference-counted object type: {Counter.GetType().FullName}");
            }
        }

        private async Task DecrementReference(HashDigest Digest, WritableStorageHandle StorageWriter)
        {
            await this.ChangeReference(Digest, -1, StorageWriter);
        }

        private async Task IncrementReference(HashDigest Digest)
        {
            await this.ChangeReference(Digest, 1);
        }

        private HashDigest[] GetReferences(IManifest Manifest)
        {
            if (Manifest is IImageManifest ImageManifest)
                return this.GetReferences(ImageManifest);
            else if (Manifest is IIndexManifest IndexManifest)
                return this.GetReferences(IndexManifest);

            return Array.Empty<HashDigest>();
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
