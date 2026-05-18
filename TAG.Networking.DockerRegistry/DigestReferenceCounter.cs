using System;
using System.Collections.Generic;
using System.Text;
using TAG.Networking.DockerRegistry.Model;

namespace TAG.Networking.DockerRegistry
{
    public class DigestReferenceCounter : IComparable
    {
        public HashDigest Digest { get; set; }
        public int ReferenceCount { get; set; }

        public DigestReferenceCounter()
        {

        }

        public DigestReferenceCounter(HashDigest Digest)
        {
            this.Digest = Digest;
            ReferenceCount = 0;
        }

        public DigestReferenceCounter(HashDigest Digest, int Count)
        {
            this.Digest = Digest;
            ReferenceCount = Count;
        }


        public int CompareTo(object obj)
        {
            if (obj is DigestReferenceCounter Other)
                return Digest.CompareTo(Other.Digest);
            else
                throw new ArgumentException("Object is not a DigestReferenceCounter.", nameof(obj));
        }
    }
}
