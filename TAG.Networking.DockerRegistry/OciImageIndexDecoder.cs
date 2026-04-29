using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Model.OciImages;
using Waher.Content;
using Waher.Runtime.Inventory;
using Waher.Runtime.IO;

namespace TAG.Networking.DockerRegistry
{
    public class OciImageIndexDecoder : IContentDecoder
    {
        public const string DefaultContentType = OciImageIndex.MediaTypeValue;

        string[] IInternetContent.ContentTypes => new string[]
        {
            DefaultContentType,
        };

        string[] IInternetContent.FileExtensions => new string[] { };

        public OciImageIndexDecoder() { }

        Task<ContentResponse> IContentDecoder.DecodeAsync(string ContentType, byte[] Data, Encoding Encoding, KeyValuePair<string, string>[] Fields, Uri BaseUri, ICodecProgress Progress)
        {
            string Raw = Strings.GetString(Data, Encoding ?? Encoding.UTF8);
            object JsonObj = JSON.Parse(Raw);

            if (!(JsonObj is Dictionary<string, object> Dict))
                throw new Exception("Invalid Image Index.");

            try
            {
                OciImageIndex ParsedIndex = new OciImageIndex(Dict);
                ParsedIndex.Raw = Data;
                return Task.FromResult(new ContentResponse(ContentType, ParsedIndex, Data));
            }
            catch (Exception ex)
            {
                return Task.FromResult(new ContentResponse(ex));
            }
        }
        bool IContentDecoder.Decodes(string ContentType, out Grade Grade)
        {
            if (ContentType == DefaultContentType)
            {
                Grade = Grade.Excellent;
                return true;
            }

            Grade = Grade.NotAtAll;
            return false;
        }

        bool IInternetContent.TryGetContentType(string FileExtension, out string ContentType)
        {
            ContentType = null;
            return false;
        }

        bool IInternetContent.TryGetFileExtension(string ContentType, out string FileExtension)
        {
            FileExtension = null;
            return false;
        }
    }
}
