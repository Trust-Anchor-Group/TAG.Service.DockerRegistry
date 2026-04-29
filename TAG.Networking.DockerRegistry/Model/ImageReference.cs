using System;
using System.Collections.Generic;
using System.Text;
using Waher.Persistence.Attributes;

namespace TAG.Networking.DockerRegistry.Model
{
    /// <summary>
    /// A Docker Image Reference
    /// </summary>
    [CollectionName("DockerImageReference")]
    [TypeName(TypeNameSerialization.None)]
    [Index("RepositoryName", "Tag")]
    [Index("Digest")]
    public class ImageReference
    {
        /// <summary>
        /// Object ID
        /// </summary>
        [ObjectId]
        public string ObjectId { get; set; }
        /// <summary>
        /// Name of image.
        /// </summary>
        public string RepositoryName { get; set; }

        /// <summary>
        /// Image Tag.
        /// </summary>
        public string Tag { get; set; }

        /// <summary>
        /// Manifest Digest
        /// </summary>
        public HashDigest Digest { get; set; }
    }
}
