using System;
using System.Collections.Generic;
using Waher.Security;

namespace TAG.Networking.DockerRegistry.Model.OciImages
{
	public class OCIImageManifest : IImageManifest
	{
		public const int SchemaVersionValue = 2;
		public const string MediaTypeValue = "application/vnd.oci.image.manifest.v1+json";
		public int SchemaVersion => SchemaVersionValue;
		public string MediaType => MediaTypeValue;
		public OciImageConfig Config { get; set; }
		public OciImageLayer[] Layers { get; set; }
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

        public OCIImageManifest()
		{
		}

		public OCIImageManifest(Dictionary<string, object> Dict)
		{
			if (!Dict.TryGetValue("mediaType", out object MediaTypeObj) ||
				!(MediaTypeObj is string MediaType) || MediaType != MediaTypeValue)
				throw new Exception("Unsupported media type. Only 'application/vnd.docker.distribution.manifest.v2+json' is supported.");

			if (!(Dict.TryGetValue("schemaVersion", out object SchemaVersionObj) &&
				SchemaVersionObj is int SchemaVersion &&
				SchemaVersion == SchemaVersionValue))
				throw new Exception("Unsupported schema version. Only version 2 is supported.");

			// config
			if (!(Dict.TryGetValue("config", out object ConfigObj) &&
				ConfigObj is Dictionary<string, object> DictConfig))
				throw new Exception("Invalid config.");

			OciImageConfig ParsedConfig = new OciImageConfig(DictConfig);
			Config = ParsedConfig;

			if (!(Dict.TryGetValue("layers", out object LayersObj) && LayersObj is object[] DictLayers))
				throw new Exception("Invalid layers.");

			List<OciImageLayer> ParsedLayers = new List<OciImageLayer>();

			foreach (object LayerObj in DictLayers)
			{
				if (!(LayerObj is Dictionary<string, object> LayerDict))
					throw new Exception("Invalid layer.");

				ParsedLayers.Add(new OciImageLayer(LayerDict));
			}

			Layers = ParsedLayers.ToArray();
		}

		public IImageLayer[] GetLayers()
		{
			return Layers;
		}
		public IImageConfig GetConfig()
		{
			return Config;
		}	
	}
}
