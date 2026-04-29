using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Waher.Content.Json;
using Waher.Events;
using Waher.Persistence;
using Waher.Persistence.Attributes;
using Waher.Persistence.Filters;
using Waher.Runtime.Inventory;
using Waher.Security;
namespace TAG.Networking.DockerRegistry.Model
{
    [CollectionName("DockerStorage")]
    [Index("Guid")]
    public class DockerStorage : IJsonEncodingHint, IDashboardAuthorizable
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
        /// Blob reference counters
        /// </summary>
        public DigestReferenceCounter[] BlobCounter { get; set; }

        /// <summary>
        /// Max amount unique blob data in bytes
        /// </summary>
        public long MaxStorage { get; set; }

        /// <summary>
        /// Amount of unique blob data referenced in bytes
        /// </summary>
        public long UsedStorage { get; set; }

        public Grade CanEncodeJson => Grade.Perfect;

        /// <summary>
        /// A blob reference counter and storage tracker
        /// </summary>
        public DockerStorage()
        {

        }
        public DockerStorage(long MaxStorage)
        {
            this.MaxStorage = MaxStorage;
        }

        public async Task RegisterManifest(IImageManifest Image)
        {
            await RecordDigestReference(Image.GetConfig().Digest);

            foreach (IImageLayer Layer in Image.GetLayers())
            {
                await RecordDigestReference(Layer.Digest);
            }
        }

        public async Task UnregisterImage(IImageManifest Image)
        {
            await DropDigestReference(Image.GetConfig().Digest);

            foreach (IImageLayer Layer in Image.GetLayers())
            {
                await DropDigestReference(Layer.Digest);
            }
        }

        public async Task RegisterDanglingBlob(DanglingDockerBlob blob)
        {
            if (!IncrementDigest(blob.Digest))
                return;

            UsedStorage += blob.Size;
        }

        public async Task UnregisterDanglingBlob(DanglingDockerBlob blob)
        {
            if (!DecrementDigest(blob.Digest))
                return;

            UsedStorage -= blob.Size;
        }

        private async Task RecordDigestReference(HashDigest Digest)
        {
            if (!IncrementDigest(Digest))
                return;
            
            DockerBlob Blob = await Database.FindFirstIgnoreRest<DockerBlob>(new FilterAnd(new FilterFieldEqualTo("Digest", Digest)));

            if (Blob == null)
            {
                Log.Critical("Tried to increment with blob which does not exist");
                return;
            }

            UsedStorage += Blob.Size;
        }

        private bool IncrementDigest(HashDigest Digest)
        {
            int index = Array.BinarySearch(BlobCounter, new DigestReferenceCounter() { Digest = Digest });

            if (index < 0)
            {
                List<DigestReferenceCounter> BlobCounterList = new List<DigestReferenceCounter>(this.BlobCounter ?? Array.Empty<DigestReferenceCounter>());
                BlobCounterList.Add(new DigestReferenceCounter() { Digest = Digest, ReferenceCount = 1 });
                BlobCounterList.Sort();
                BlobCounter = BlobCounterList.ToArray();
                return true;
            }

            BlobCounter[index].ReferenceCount++;
            return false;
        }

        private async Task DropDigestReference(HashDigest Digest)
        {
            if (!DecrementDigest(Digest))
                return;

            DockerBlob Blob = await Database.FindFirstIgnoreRest<DockerBlob>(new FilterAnd(new FilterFieldEqualTo("Digest", Digest)));
            if (Blob == null)
            {
                Log.Critical("Tried to decrement with blob which does not exist");
                return;
            }
            UsedStorage -= Blob.Size;
        }

        private bool DecrementDigest(HashDigest Digest)
        {
            int index = Array.BinarySearch(BlobCounter, new DigestReferenceCounter() { Digest = Digest });

            if (index < 0)
            {
                Log.Critical("Tried to decrement digest which was not registed");
                return false;
            }

            BlobCounter[index].ReferenceCount--;

            if (BlobCounter[index].ReferenceCount < 1)
            {
                List<DigestReferenceCounter> BlobCounterList = new List<DigestReferenceCounter>(this.BlobCounter ?? Array.Empty<DigestReferenceCounter>());
                BlobCounterList.RemoveAt(index);
                BlobCounter = BlobCounterList.ToArray();
                return true;
            }

            return false;
        }

        public static string UIString(DockerStorage Storage)
        {
            return ToMetricBytes(Storage.UsedStorage) + " of " + ToMetricBytes(Storage.MaxStorage);
        }

        private static string ToMetricBytes(double value)
        {
            return "TODO";
        }

        public async Task<bool> IsAuthorized(IUser User, string Privilege)
        {
            if (User.HasPrivilege(DashboardPrivileges.Admin))
                return true;

            DockerActor Owner = await Database.FindFirstIgnoreRest<DockerActor>(new FilterAnd(new FilterFieldEqualTo("StorageGuid", Guid)));

            if (Owner is DockerUser DockerUser)
                return await DockerUser.IsAuthorized(User, Privilege);

            if (Owner is DockerOrganization Org)
                return await Org.IsAuthorized(User, Privilege);

            return false;
        }
    }

    [Serializable]
    public class DigestReferenceCounter : IComparable
    {
        public HashDigest Digest;
        public int ReferenceCount;

        public int CompareTo(object obj)
        {
            if (obj is DigestReferenceCounter Other)
                return Digest.CompareTo(Other.Digest);
            else
                throw new ArgumentException("Object is not a DigestReferenceCounter.", nameof(obj));
        }
    }
}
