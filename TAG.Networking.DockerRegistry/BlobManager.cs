using System.IO;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Model;
using Waher.Persistence;
using Waher.Persistence.Filters;
using Waher.Security;

namespace TAG.Networking.DockerRegistry
{
    public class BlobManager
    {
        private string blobFolder;

        public BlobManager(string BlobFolder)
        {
            this.blobFolder = BlobFolder;
        }

        public async Task<FileStream?> ReadBlob(HashDigest Digest)
        {
            if (!await GetBlob(Digest))
                return null;

            string FileName = GetFilePath(Digest);
            return File.OpenRead(FileName);
        }

        // <summary>	
        // Creates a blob both in database and in folder.
        // <summary>	
        public async Task<bool> UploadComplete(BlobUpload Upload)
        {
            HashDigest Digest = Upload.ComputeDigestLocked(HashFunction.SHA256);

            if ((await GetBlob(Digest)))
                return false;

            Upload.Blob.Digest = Digest;
            Upload.Blob.FilePath = GetFilePath(Digest);

            using (FileStream Content = File.Create(Upload.Blob.FilePath))
            {
                Upload.File.Position = 0;
                Upload.Blob.Size = Upload.File.Length;
                await Upload.File.CopyToAsync(Content);
            }

            await Database.Insert(Upload.Blob);
            return true;
        }

        // <summary>	
        // Deletes a blob both from database and from folder.
        // <summary>	
        public async Task DeleteBlob(HashDigest Digest)
        {
            string FileName = GetFilePath(Digest);
            await Database.FindDelete<DockerBlob>(new FilterAnd(new FilterFieldEqualTo("Digest", Digest)));
            File.Delete(FileName);
        }

        // <summary>	
        // Checks whether a blob exists both in database and in folder, and repairs inconsistencies if any.
        // <summary>	
        public async Task<bool> GetBlob(HashDigest Digest)
        {
            Task<bool> InDbTask = ExistsInDatabase(Digest);
            bool InFolder = ExistsInFolder(Digest);
            bool InDb = await InDbTask;

            if (InDb && !InFolder)
            {
                // record exists but no blob file
                await Database.FindDelete<DockerBlob>(new FilterAnd(new FilterFieldEqualTo("Digest", Digest)));
                return false;
            }

            if (!InDb && InFolder)
            {
                // blob exists but the db record was removed, restore the db record
                DockerBlob blob = new DockerBlob()
                {
                    Digest = Digest,
                    FilePath = GetFilePath(Digest),
                };
                blob.Size = new FileInfo(blob.FilePath).Length;
                await Database.Insert(blob);
                return true;
            }

            if (!InDb && !InFolder)
            {
                // neither exists
                return false;
            }

            return true;
        }

        // <summary>
        // Checks whether a blob file exists in the folder.
        // <summary>
        private bool ExistsInFolder(HashDigest Digest)
        {
            return File.Exists(GetFilePath(Digest));
        }

        // <summary>
        // Checks whether a blob record exists in the database.
        // <summary>
        private async Task<bool> ExistsInDatabase(HashDigest Digest)
        {
            return !(await Database.FindFirstIgnoreRest<DockerBlob>(
                new FilterAnd(new FilterFieldEqualTo("Digest", Digest))
                ) is null);
        }

        // <summary>
        // Gets the file path for a blob.
        // <summary>
        private string GetFilePath(HashDigest Digest)
        {
            return Path.Combine(this.blobFolder, Hashes.BinaryToString(Digest.Hash) + ".bin");
        }
    }
}
