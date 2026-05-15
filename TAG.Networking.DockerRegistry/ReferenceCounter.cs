using System;
using TAG.Networking.DockerRegistry.Model;
using Waher.Persistence.Attributes;

namespace TAG.Networking.DockerRegistry
{
    [TypeName(TypeNameSerialization.FullName)]
    public class ReferenceCounter : IComparable
    {
        public HashDigest Digest { get; set; }
        public int ReferenceCount { get; set; }

        public int CompareTo(object obj)
        {
            if (obj is ReferenceCounter Other)
                return Digest.CompareTo(Other.Digest);
            else
                throw new ArgumentException("Object is not a DigestReferenceCounter.", nameof(obj));
        }
        public ReferenceCounter()
        {

        }

        public ReferenceCounter(HashDigest Digest)
        {
            this.Digest = Digest;
            ReferenceCount = 0;
        }

        public ReferenceCounter(HashDigest Digest, int Count)
        {
            this.Digest = Digest;
            ReferenceCount = Count;
        }
    }
}
