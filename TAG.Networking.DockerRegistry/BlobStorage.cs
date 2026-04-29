using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Model;
using Waher.Events;
using Waher.Persistence;
using Waher.Persistence.Filters;
using Waher.Security;

namespace TAG.Networking.DockerRegistry
{
    internal class BlobStorage
    {
        private string blobFolder;

        public BlobStorage(string BlobFolder)
        {
            this.blobFolder = BlobFolder;
        }

        public async Task<FileStream> TryGetBlobFile(HashDigest Digest)
        {
            if (!await ExistsSafe(Digest))
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

            if ((await ExistsSafe(Digest)))
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
        public async Task<bool> ExistsSafe(HashDigest Digest)
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
        public bool ExistsInFolder(HashDigest Digest)
        {
            return File.Exists(GetFilePath(Digest));
        }

        // <summary>
        // Checks whether a blob record exists in the database.
        // <summary>
        public async Task<bool> ExistsInDatabase(HashDigest Digest)
        {
            return !(await Database.FindFirstIgnoreRest<DockerBlob>(
                new FilterAnd(new FilterFieldEqualTo("Digest", Digest))
                ) is null);
        }

        // <summary>
        // Gets the file path for a blob.
        // <summary>
        public string GetFilePath(HashDigest Digest)
        {
            return Path.Combine(this.blobFolder, Hashes.BinaryToString(Digest.Hash) + ".bin");
        }
        public async Task<int> CleanUnusedBlobs()
        {
            Log.Informational("Cleaning turned off");
            return 0;
            Log.Informational("Cleaning unused Docker Registry blobs...");
            List<HashDigest> AllBlobDigests = (await Database.Find<DockerBlob>()).Select(Blob => Blob.Digest).ToList();
            AllBlobDigests.Sort();

            string[] files = Directory.GetFiles(blobFolder);

            for (int i = 0; i < files.Length; i++)
            {
                string s = Path.GetFileNameWithoutExtension(files[i]);
                HashDigest Digest = new HashDigest()
                {
                    HashFunction = HashFunction.SHA256,
                    Hash = Hashes.StringToBinary(s)
                };
                if (AllBlobDigests.BinarySearch(Digest) < 0)
                {
                    AllBlobDigests.Add(Digest);
                }
            }

            AllBlobDigests.Sort();

            DanglingDockerBlob[] DanglingBlobs = (await Database.FindDelete<DanglingDockerBlob>()).ToArray();

            int index = -1;
            for (int i = 0; i < DanglingBlobs.Length; i++)
            {
                index = AllBlobDigests.BinarySearch(DanglingBlobs[i].Digest);

                if (index > -1)
                    AllBlobDigests.RemoveAt(index);
            }

            IImageManifest[] AllImages = (await Database.Find<DockerManifest>())
                .Where(x => x.Manifest is IImageManifest)
                .Select(x => x.Manifest)
                .Cast<IImageManifest>()
                .ToArray();

            for (int i = 0; i < AllImages.Length; i++)
            {
                index = AllBlobDigests.BinarySearch(AllImages[i].GetConfig().Digest);

                if (index > -1)
                    AllBlobDigests.RemoveAt(index);

                foreach (IImageLayer Layer in AllImages[i].GetLayers())
                {
                    index = AllBlobDigests.BinarySearch(Layer.Digest);
                    if (index > -1)
                        AllBlobDigests.RemoveAt(index);
                }
            }

            Task[] DeletionTasks = new Task[AllBlobDigests.Count];
            int Deletions = 0;

            for (int i = 0; i < AllBlobDigests.Count; i++)
            {
                try
                {
                    DeletionTasks[i] = DeleteBlob(AllBlobDigests[i]);
                }
                catch (Exception e)
                {
                    Log.Exception(e);
                }
            }

            for (int i = 0; i < DeletionTasks.Length; i++)
            {
                Task DeletionTask = DeletionTasks[i];
                if (DeletionTask is null)
                    continue;

                try
                {
                    await DeletionTask;
                    Deletions++;
                }
                catch (Exception ex)
                {
                    Log.Exception(ex);
                }

            }

            Log.Informational("Docker Registry cleaned, " + Deletions + " blobs removed");
            return Deletions;
        }
    }
}
