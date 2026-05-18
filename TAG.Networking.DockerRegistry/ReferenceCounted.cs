using System;
using TAG.Networking.DockerRegistry.Model;
using Waher.Persistence.Attributes;

namespace TAG.Networking.DockerRegistry
{
    [CollectionName("TAG.Networking.DockerRegistry.ReferenceCounted")]
    [TypeName(TypeNameSerialization.FullName)]
    [Index("Digest")]
    public abstract class ReferenceCounted
    {
        /// <summary>
        /// Object ID
        /// </summary>
        [ObjectId]
        public string ObjectId { get; set; }
        public HashDigest Digest { get; set; }
        public int ReferenceCount { get; set; }

        public bool HasReferences => this.ReferenceCount > 0;

        public ReferenceCounted()
        {

        }

        public ReferenceCounted(HashDigest Digest)
        {
            this.Digest = Digest;
            ReferenceCount = 0;
        }

        public ReferenceCounted(HashDigest Digest, int Count)
        {
            this.Digest = Digest;
            ReferenceCount = Count;
        }

        public bool ChangeReferenceCount(int count)
        {
            if (count == 0)
                return !this.HasReferences;

            int nextCount = this.ReferenceCount + count;

            if (nextCount < 0)
                throw new InvalidOperationException("Reference count cannot be negative.");

            this.ReferenceCount = nextCount;
            return !this.HasReferences;
        }
    }
}
