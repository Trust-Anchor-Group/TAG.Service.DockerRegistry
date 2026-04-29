using System;
using System.Collections.Generic;
using Waher.Security;

namespace TAG.Networking.DockerRegistry.Model.OciImages
{
    /// <summary>
    /// OCI Image Index (multi-platform manifest list)
    /// https://github.com/opencontainers/image-spec/blob/main/image-index.md
    /// </summary>
    public class OciImageIndex : IIndexManifest
    {
        public const int SchemaVersionValue = 2;
        public const string MediaTypeValue = "application/vnd.oci.image.index.v1+json";
        public int SchemaVersion => SchemaVersionValue;
        public string MediaType => MediaTypeValue;
        public OciContentDescriptor[] Manifests { get; set; }
        public Dictionary<string, string> Annotations { get; set; }
        private byte[] raw;
        public byte[] Raw
        {
            get { return this.raw; }
            set
            {
                this.digest = null;
                this.raw = value;
            }
        }

        private HashDigest digest;

        public HashDigest Digest
        {
            get
            {
                if (this.digest is null)
                    this.digest = new HashDigest(HashFunction.SHA256, this.Raw);
                return this.digest;
            }

            set { this.digest = value; }
        }

        public OciImageIndex() { }

        public OciImageIndex(Dictionary<string, object> dict)
        {
            if (!dict.TryGetValue("mediaType", out object mediaTypeObj) || !(mediaTypeObj is string mediaType) || mediaType != MediaTypeValue)
                throw new Exception($"Unsupported media type. Only '{MediaTypeValue}' is supported.");

            if (!(dict.TryGetValue("schemaVersion", out object schemaVersionObj) && schemaVersionObj is int schemaVersion && schemaVersion == SchemaVersionValue))
                throw new Exception("Unsupported schema version. Only version 2 is supported.");

            if (!(dict.TryGetValue("manifests", out object manifestsObj) && manifestsObj is object[] manifestsArray))
                throw new Exception("Invalid manifests array.");

            var manifests = new List<OciContentDescriptor>();
            foreach (object manifestObj in manifestsArray)
            {
                if (manifestObj is Dictionary<string, object> manifestDict)
                    manifests.Add(OciContentDescriptor.Parse(manifestDict));
                else
                    throw new Exception("Invalid manifest entry.");
            }
            Manifests = manifests.ToArray();

            if (dict.TryGetValue("annotations", out object annotationsObj) && annotationsObj is Dictionary<string, string> annotationsDict)
                Annotations = annotationsDict;
            else
                Annotations = null;
        }

        public IContentDescriptor[] GetManifests()
        {
            return this.Manifests;
        }
    }
}
