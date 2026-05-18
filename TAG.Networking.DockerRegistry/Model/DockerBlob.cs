using System;
using Waher.Networking.XMPP.StanzaErrors;
using Waher.Persistence.Attributes;

namespace TAG.Networking.DockerRegistry.Model
{
    /// <summary>
    /// A Docker BLOB reference
    /// </summary>
    [TypeName(TypeNameSerialization.FullName)]
    public class DockerBlob : ReferenceCounted, IComparable
    {
        /// <summary>
        /// A Docker BLOB reference
        /// </summary>
        public DockerBlob()
        {

        }

        /// <summary>
        /// File path
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// Size of blob file in bytes
        /// </summary>
        public long Size { get; set; }

        /// <summary>
        /// Amount of images using this blob
        /// </summary>
        public int CompareTo(object obj)
        {
            if (obj is DockerBlob Other)
                return Digest.CompareTo(Other.Digest);
            else
                throw new ArgumentException("Object is not a DigestReferenceCounter.", nameof(obj));
        }
    }
}
