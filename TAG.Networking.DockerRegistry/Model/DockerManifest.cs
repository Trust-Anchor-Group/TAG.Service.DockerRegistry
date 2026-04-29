using System;
using Waher.Persistence.Attributes;

namespace TAG.Networking.DockerRegistry.Model
{
    /// <summary>
    /// A Docker manifest
    /// </summary>
    [CollectionName("DockerManifest")]
    [TypeName(TypeNameSerialization.None)]
    [Index("Digest")]
    public class DockerManifest
    {
        /// <summary>
        /// A Docker Image reference
        /// </summary>
        public DockerManifest()
        {
        }

        /// <summary>
        /// Object ID
        /// </summary>
        [ObjectId]
        public string ObjectId { get; set; }

        /// <summary>
        /// Manifest Digest
        /// </summary>
        public HashDigest Digest { get; set; }

        /// <summary>
        /// Manifest Digest
        /// </summary>
        public IManifest Manifest { get; set; }

        public long GetSize()
        {
            if (Manifest is IImageManifest ImageManifest)
            {
                long Size = 0;
                foreach (IImageLayer Layer in ImageManifest.GetLayers())
                {
                    Size += Layer.Size;
                }

                return Size;
            }

            return 0;
        }


    }
}
