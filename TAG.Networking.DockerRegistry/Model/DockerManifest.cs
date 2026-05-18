using System;
using Waher.Persistence.Attributes;

namespace TAG.Networking.DockerRegistry.Model
{
    /// <summary>
    /// A Docker manifest
    /// </summary>
    [TypeName(TypeNameSerialization.FullName)]
    public class DockerManifest : ReferenceCounted
    {
        /// <summary>
        /// A Docker Image reference
        /// </summary>
        public DockerManifest()
        {
        }

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
