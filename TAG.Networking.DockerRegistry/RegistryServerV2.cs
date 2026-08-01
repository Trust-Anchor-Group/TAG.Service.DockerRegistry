using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Endpoints;
using TAG.Networking.DockerRegistry.Errors;
using TAG.Networking.DockerRegistry.Model;
using Waher.Events;
using Waher.IoTGateway;
using Waher.Networking;
using Waher.Networking.HTTP;
using Waher.Networking.Sniffers;
using Waher.Persistence;
using Waher.Persistence.Filters;
using Waher.Runtime.Threading;
using Waher.Script;
using Waher.Script.Functions.Vectors;
using Waher.Security;

namespace TAG.Networking.DockerRegistry
{
    /// <summary>
    /// Docker Registry API v2.
    /// 
    /// Reference:
    /// https://docs.docker.com/registry/spec/api/
    /// </summary>
    public class RegistryServerV2 : HttpSynchronousResource, IHttpGetMethod, IHttpGetRangesMethod, IHttpPostMethod,
        IHttpDeleteMethod, IHttpPatchMethod, IHttpPatchRangesMethod, IHttpPutMethod, IHttpPutRangesMethod, IDisposable
    {
        private static readonly Regex regexName = new Regex("[a-z0-9]+(?:[._-][a-z0-9]+)*", RegexOptions.Compiled | RegexOptions.Singleline);
        private static readonly KeyValuePair<string, string> apiHeader = new KeyValuePair<string, string>("Docker-Distribution-API-Version", "registry/2.0");

        /// <summary>
        /// Sniffable object that can be sniffed on dynamically.
        /// </summary>
        private static readonly CommunicationLayer observable = new CommunicationLayer(false);

        /// <summary>
        /// Sniffer proxy, forwarding sniffer events to <see cref="observable"/>.
        /// </summary>
        private static readonly SnifferProxy snifferProxy = new SnifferProxy(observable);

        private readonly HttpAuthenticationScheme[] authenticationSchemes;
        private readonly string dockerRegistryFolder;
        private readonly BlobManager blobManager;
        private readonly ManifestManager manifestManager;


        private readonly ManifestEndpoints manifestEndpoints;
        private readonly BlobEndpoints blobEndpoints;
        private readonly BlobUploadEndpoints blobUploadEndpoints;
        private readonly TagsEndpoints tagsEndpoints;
        /// <summary>
        /// Docker Registry API v2.
        /// </summary>
        /// <param name="DockerRegistryFolder">Docker Registry folder.</param>
        /// <param name="BlobManager">Blob manager.</param>
        /// <param name="ManifestManager">Manifest manager.</param>
        /// <param name="AuthenticationSchemes">Authentication schemes.</param>
        public RegistryServerV2(string DockerRegistryFolder, BlobManager BlobManager, ManifestManager ManifestManager, params HttpAuthenticationScheme[] AuthenticationSchemes)
            : base("/v2")
        {
            this.dockerRegistryFolder = DockerRegistryFolder;
            this.authenticationSchemes = AuthenticationSchemes;

            this.blobManager = BlobManager;
            this.manifestManager = ManifestManager;

            ISniffer[] Sniffers = new ISniffer[] { snifferProxy };

            this.manifestEndpoints = new ManifestEndpoints(this.manifestManager, this.blobManager, Sniffers);
            this.blobEndpoints = new BlobEndpoints(this.manifestManager, this.blobManager, Sniffers);
            this.blobUploadEndpoints = new BlobUploadEndpoints(this.dockerRegistryFolder, this.manifestManager, this.blobManager, Sniffers);
            this.tagsEndpoints = new TagsEndpoints(this.manifestManager, this.blobManager, Sniffers);
        }

        /// <summary>
        /// If resource handles sub-paths.
        /// </summary>
        public override bool HandlesSubPaths => true;

        /// <summary>
        /// If resource uses sessions (i.e. uses a session cookie).
        /// </summary>
        public override bool UserSessions => false;

        /// <summary>
        /// If GET method is supported.
        /// </summary>
        public bool AllowsGET => true;

        /// <summary>
        /// If POST method is supported.
        /// </summary>
        public bool AllowsPOST => true;

        /// <summary>
        /// If DELETE method is supported.
        /// </summary>
        public bool AllowsDELETE => true;

        /// <summary>
        /// If PUT method is supported.
        /// </summary>
        public bool AllowsPUT => true;

        /// <summary>
        /// If PATCH method is supported.
        /// </summary>
        public bool AllowsPATCH => true;

        /// <summary>
        /// Auto create repositories.
        /// </summary>
        public bool AutoCreateRepositories => true;

        /// <summary>
        /// Auto create users.
        /// </summary>
        public bool AutoCreateUsers => true;

        /// <summary>
        /// Gets available authentication schemes
        /// </summary>
        /// <param name="Request">Request object.</param>
        /// <returns>Array of authentication schemes.</returns>
        public override HttpAuthenticationScheme[] GetAuthenticationSchemes(HttpRequest Request)
        {
            return this.authenticationSchemes;
        }


        /// <summary>
        /// Folder where validated uploaded BLOBs are stored.
        /// </summary>
        public string BlobFolder
        {
            get
            {
                string BlobFolder = Path.Combine(this.dockerRegistryFolder, "BLOBs");

                if (!Directory.Exists(BlobFolder))
                    Directory.CreateDirectory(BlobFolder);

                return BlobFolder;
            }
        }

        /// <summary>
        /// Checks if a Name is a valid Docker name.
        /// </summary>
        /// <param name="Name">Name</param>
        /// <returns>If <paramref name="Name"/> is a valid Docker name.</returns>
        public static bool IsName(string Name)
        {
            Match M = regexName.Match(Name);
            return M.Success && M.Index == 0 && M.Length == Name.Length;
        }

        /// <summary>
        /// Executes a GET method.
        /// </summary>
        /// <param name="Request">Request object.</param>
        /// <param name="Response">Response object.</param>
        public async Task GET(HttpRequest Request, HttpResponse Response)
        {
            await this.GET(Request, Response, null);
        }

        /// <summary>
        /// Executes a GET method.
        /// </summary>
        /// <param name="Request">Request object.</param>
        /// <param name="Response">Response object.</param>
        /// <param name="Interval">Range interval.</param>
        public async Task GET(HttpRequest Request, HttpResponse Response, ByteRangeInterval Interval)
        {
            try
            {
                SetApiHeader(Response);

                string Resource = Request.SubPath;

                if (Resource == "/" || string.IsNullOrEmpty(Resource))  // API Version Checkapplication/vnd.oci.image.manifest.v1+json
                {
                    Response.StatusCode = 200;
                    await Response.SendResponse();
                    return;
                }

                Prepare(Request, out string RepositoryName, out string ApiResource, out string ReferenceString);

                if (ApiResource == "/_catalog")
                {
                    List<DockerRepository> ListableRepositories = new List<DockerRepository>();
                    DockerActor[] Actors = await this.GetActors(Request);

                    foreach (DockerActor DockerActor in Actors)
                    {
                        ListableRepositories.AddRange(await Database.Find<DockerRepository>(new FilterAnd(new FilterFieldEqualTo("OwnerGuid", DockerActor.Guid))));

                        DockerRepositoryPrivilege[] Privilages = (await Database.Find<DockerRepositoryPrivilege>(new FilterAnd(new FilterFieldEqualTo("ActorGuid", DockerActor.Guid)))).ToArray();
                        foreach (DockerRepositoryPrivilege Privilege in Privilages)
                        {
                            ListableRepositories.Add(await Database.FindFirstIgnoreRest<DockerRepository>(new FilterAnd(new FilterFieldEqualTo("Guid", Privilege.RepositoryGuid))));
                        }
                    }

                    for (int i = ListableRepositories.Count() - 1; i >= 0; i--)
                    {
                        for (int j = i - 1; j >= 0; j--)
                        {
                            if (ListableRepositories[j].RepositoryName == ListableRepositories[i].RepositoryName)
                            {
                                ListableRepositories.RemoveAt(i);
                                break;
                            }
                        }
                    }

                    if (IsPaginated(Request, out int First, out int Count))
                    {
                        ListableRepositories.RemoveRange(0, First);
                        ListableRepositories.RemoveRange(First, Count);
                    }

                    await Response.Return(new Dictionary<string, object>()
                        {
                            { "repositories", ListableRepositories.Select(x => x.RepositoryName) }
                        });
                    return;
                }

                DockerRepository Repository = await this.GetRepository(Request, RepositoryName);

                DockerActor Actor;
                if (Repository == null)
                    (Actor, Repository) = await this.GetEffectiveActor(Request, RepositoryName);
                else
                    Actor = await this.GetEffectiveActor(Request, Repository);

                if (Repository == null)
                    throw new NotFoundException(new DockerErrors(DockerErrorCode.NAME_UNKNOWN, "Repository name not known to registry."), apiHeader);

                switch (ApiResource)
                {
                    case "/blobs":
                        await this.blobEndpoints.GET(Request, Response, Interval, Actor, Repository, ReferenceString);
                        return;
                    case "/blobs/uploads":
                        await this.blobUploadEndpoints.GET(Request, Response, Interval, Actor, Repository, ReferenceString);
                        return;
                    case "/tags/list":
                        await this.tagsEndpoints.GET(Request, Response, Actor, Repository, ReferenceString);
                        return;
                    case "/manifests":
                        await this.manifestEndpoints.GET(Request, Response, Actor, Repository, ReferenceString);
                        return;
                    default:
                        throw new BadRequestException(new DockerErrors(DockerErrorCode.UNSUPPORTED, "The operation is unsupported."), apiHeader);
                }

                throw new BadRequestException(new DockerErrors(DockerErrorCode.UNSUPPORTED, "The operation is unsupported."), apiHeader);
            }
            catch (HttpException ex)
            {
                await Response.SendResponse(ex);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                throw new InternalServerErrorException();
            }
        }

        /// <summary>
        /// Executes a POST method.
        /// </summary>
        /// <param name="Request">Request object.</param>
        /// <param name="Response">Response object.</param>
        public async Task POST(HttpRequest Request, HttpResponse Response)
        {
            try
            {
                SetApiHeader(Response);

                Prepare(Request, out string RepositoryName, out string ApiResource, out string ReferenceString);
                DockerRepository Repository = await this.GetRepository(Request, RepositoryName);
                DockerActor Actor;
                if (Repository == null)
                    (Actor, Repository) = await this.GetEffectiveActor(Request, RepositoryName);
                else
                    Actor = await this.GetEffectiveActor(Request, Repository);

                if (Repository == null)
                    throw new NotFoundException(new DockerErrors(DockerErrorCode.NAME_UNKNOWN, "Repository name not known to registry."), apiHeader);

                switch (ApiResource)
                {
                    case "/blobs/uploads":
                        await this.blobUploadEndpoints.POST(Request, Response, Actor, Repository, ReferenceString);
                        return;
                    default:
                        throw new BadRequestException(new DockerErrors(DockerErrorCode.UNSUPPORTED, "The operation is unsupported."), apiHeader);
                }

                throw new BadRequestException(new DockerErrors(DockerErrorCode.UNSUPPORTED, "The operation is unsupported."), apiHeader);
            }
            catch (HttpException ex)
            {
                await Response.SendResponse(ex);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                throw new InternalServerErrorException();
            }
        }

        /// <summary>
        /// Executes a DELETE method.
        /// </summary>
        /// <param name="Request">Request object.</param>
        /// <param name="Response">Response object.</param>
        public async Task DELETE(HttpRequest Request, HttpResponse Response)
        {
            try
            {
                SetApiHeader(Response);

                Prepare(Request, out string RepositoryName, out string ApiResource, out string ReferenceString);
                DockerRepository Repository = await this.GetRepository(Request, RepositoryName);
                DockerActor Actor;
                if (Repository == null)
                    (Actor, Repository) = await this.GetEffectiveActor(Request, RepositoryName);
                else
                    Actor = await this.GetEffectiveActor(Request, Repository);

                if (Repository == null)
                    throw new NotFoundException(new DockerErrors(DockerErrorCode.NAME_UNKNOWN, "Repository name not known to registry."), apiHeader);

                switch (ApiResource)
                {
                    case "/blobs/uploads":
                        await this.blobUploadEndpoints.DELETE(Request, Response, Actor, Repository, ReferenceString);
                        return;
                    case "/blobs":
                        await this.blobEndpoints.DELETE(Request, Response, Actor, Repository, ReferenceString);
                        return;
                    case "/manifests":
                        await this.manifestEndpoints.DELETE(Request, Response, Actor, Repository, ReferenceString);
                        return;
                    case "/tags":
                    // TODO
                    default:
                        throw new BadRequestException(new DockerErrors(DockerErrorCode.UNSUPPORTED, "The operation is unsupported."), apiHeader);
                }

                throw new BadRequestException(new DockerErrors(DockerErrorCode.UNSUPPORTED, "The operation is unsupported."), apiHeader);
            }
            catch (HttpException ex)
            {
                await Response.SendResponse(ex);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                throw new InternalServerErrorException();
            }
        }

        /// <summary>
        /// Executes a PATCH method.
        /// </summary>
        /// <param name="Request">Request object.</param>
        /// <param name="Response">Response object.</param>
        public Task PATCH(HttpRequest Request, HttpResponse Response)
        {
            return this.PATCH(Request, Response, null);
        }

        /// <summary>
        /// Executes a PATCH method.
        /// </summary>
        /// <param name="Request">Request object.</param>
        /// <param name="Response">Response object.</param>
        /// <param name="Interval">Range interval.</param>
        public async Task PATCH(HttpRequest Request, HttpResponse Response, ContentByteRangeInterval Interval)
        {
            try
            {
                SetApiHeader(Response);

                Prepare(Request, out string RepositoryName, out string ApiResource, out string ReferenceString);
                DockerRepository Repository = await this.GetRepository(Request, RepositoryName);
                DockerActor Actor;
                if (Repository == null)
                    (Actor, Repository) = await this.GetEffectiveActor(Request, RepositoryName);
                else
                    Actor = await this.GetEffectiveActor(Request, Repository);

                if (Repository == null)
                    throw new NotFoundException(new DockerErrors(DockerErrorCode.NAME_UNKNOWN, "Repository name not known to registry."), apiHeader);

                switch (ApiResource)
                {
                    case "/blobs/uploads":
                        await this.blobUploadEndpoints.PATCH(Request, Response, Interval, Actor, Repository, ReferenceString);
                        return;
                    case "/blobs":
                    // TODO
                    case "/manifests":
                    // TODO
                    case "/_catalog":
                    // TODO
                    case "/tags":
                    // TODO
                    default:
                        throw new BadRequestException(new DockerErrors(DockerErrorCode.UNSUPPORTED, "The operation is unsupported."), apiHeader);
                }

                throw new BadRequestException(new DockerErrors(DockerErrorCode.UNSUPPORTED, "The operation is unsupported."), apiHeader);
            }
            catch (HttpException ex)
            {
                await Response.SendResponse(ex);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                throw new InternalServerErrorException();
            }
        }

        /// <summary>
        /// Executes a PUT method.
        /// </summary>
        /// <param name="Request">Request object.</param>
        /// <param name="Response">Response object.</param>
        public Task PUT(HttpRequest Request, HttpResponse Response)
        {
            return this.PUT(Request, Response, null);
        }

        /// <summary>
        /// Executes a PUT method.
        /// </summary>
        /// <param name="Request">Request object.</param>
        /// <param name="Response">Response object.</param>
        /// <param name="Interval">Range interval.</param>
        public async Task PUT(HttpRequest Request, HttpResponse Response, ContentByteRangeInterval Interval)
        {
            try
            {
                SetApiHeader(Response);

                Prepare(Request, out string RepositoryName, out string ApiResource, out string ReferenceString);

                DockerRepository Repository = await this.GetRepository(Request, RepositoryName);
                DockerActor Actor;
                if (Repository == null)
                {
                    (Actor, Repository) = await this.GetEffectiveActor(Request, RepositoryName);
                }
                else
                    Actor = await this.GetEffectiveActor(Request, Repository);

                if (Repository == null)
                    throw new NotFoundException(new DockerErrors(DockerErrorCode.NAME_UNKNOWN, "Repository name not known to registry."), apiHeader);

                switch (ApiResource)
                {
                    case "/blobs/uploads":
                        await this.blobUploadEndpoints.PUT(Request, Response, Interval, Actor, Repository, ReferenceString);
                        return;
                    case "/manifests":
                        await this.manifestEndpoints.PUT(Request, Response, Actor, Repository, ReferenceString);
                        return;
                    case "/_catalog":
                    // TODO
                    case "/tags":
                    // TODO
                    default:
                        throw new BadRequestException(new DockerErrors(DockerErrorCode.UNSUPPORTED, "The operation is unsupported."), apiHeader);
                }

                throw new BadRequestException(new DockerErrors(DockerErrorCode.UNSUPPORTED, "The operation is unsupported."), apiHeader);
            }
            catch (HttpException ex)
            {
                await Response.SendResponse(ex);
            }
            catch (Exception ex)
            {
                Log.Exception(ex);
                throw new InternalServerErrorException();
            }
        }

        #region Docker Data Retrival
        private Task<DockerUser> GetDockerUser(IUser User)
        {
            return Database.FindFirstIgnoreRest<DockerUser>(new FilterAnd(new FilterFieldEqualTo("AccountName", User.UserName)));
        }

        private Task<DockerOrganization> GetOrganizationActor(ILegalIdentityUser LegalId)
        {
            string OrgName = "";

            foreach (ILegalIdentityProperty Property in LegalId.LegalIdentity.Properties)
            {
                if (Property.Name == "ORGNAME")
                {
                    OrgName = Property.Value;
                }
            }

            return Database.FindFirstIgnoreRest<DockerOrganization>(new FilterAnd(new FilterFieldEqualTo("OrganizationName", OrgName)));
        }

        private async Task<DockerActor[]> GetActors(HttpRequest Request)
        {
            if (Request.User is null)
                throw new ForbiddenException(new DockerErrors(DockerErrorCode.DENIED, "Requested access to the resource is denied."), apiHeader);

            List<DockerActor> Actors = new List<DockerActor>();

            DockerUser User = await this.GetDockerUser(Request.User);
            if (!(User is null))
                Actors.Add(User);

            if (Request.User is ILegalIdentityUser LegalIdentityUser)
            {
                DockerOrganization Organization = await this.GetOrganizationActor(LegalIdentityUser);
                if (!(Organization is null))
                    Actors.Add(Organization);
            }

            return Actors.ToArray();
        }

        private async Task<DockerActor> GetEffectiveActor(HttpRequest Request, DockerRepository Repository)
        {
            DockerActor[] Actors = await this.GetActors(Request);

            if (Actors.Length == 0)
                throw new ForbiddenException(new DockerErrors(DockerErrorCode.DENIED, "Requested access to the resource is denied."), apiHeader);

            if (Actors.Length == 1)
                return Actors[0];

            DockerActor Chosen = Actors[0];

            for (int i = 1; i < Actors.Length; i++)
            {
                DockerActor Other = Actors[i];
                if (Repository.OwnerGuid == Other.Guid)
                    Chosen = Other;
            }

            return Chosen;
        }

        /// <summary>
        /// Gets the effective actor for a repository that does not exist yet
        /// </summary>
        /// <param name="Request"></param>
        /// <param name="RepositoryName"></param>
        /// <returns></returns>
        /// <exception cref="ForbiddenException"></exception>
        private async Task<(DockerActor, DockerRepository)> GetEffectiveActor(HttpRequest Request, CaseInsensitiveString RepositoryName)
        {
            List<DockerActor> Actors = (await this.GetActors(Request)).ToList();

            for (int i = Actors.Count() - 1; i >= 0; i--)
            {
                if (!Actors[i].Options.IsOptionTrue(ActorOptions.CanAutoCreateRepository))
                    Actors.RemoveAt(i);
            }

            foreach (DockerActor Actor in Actors)
            {
                using Semaphore RepositorySemaphore = await Semaphores.BeginWrite("DockerRegistry_Repository_" + RepositoryName);
                DockerRepository Prev = await Database.FindFirstIgnoreRest<DockerRepository>(new FilterAnd(new FilterFieldEqualTo("RepositoryName", RepositoryName)));

                if (Prev is null)
                {
                    DockerRepository Repository = await this.TryAutoCreateRepository(Actor, RepositoryName);
                    if (!(Repository is null))
                        return (Actor, Repository);
                }

                return (Actor, Prev);
            }

            return (null, null);
        }

        private async Task<DockerRepository> TryAutoCreateRepository(DockerActor Actor, CaseInsensitiveString RepositoryName)
        {
            if (!(Actor.Options.TryGetOption(ActorOptions.AutoCreateRepositoryRoot, out object RootNameObj) && RootNameObj is string RootName))
                return null;

            if (!RepositoryName.StartsWith(RootName))
                return null;

            DockerRepository Repository = await DockerRepository.CreateInsertRepository(RepositoryName, true, Actor.Guid);
            return Repository;
        }

        private async Task<DockerRepository> GetRepository(HttpRequest Request, string RepositoryName)
        {
            DockerRepository Repository = await Database.FindFirstIgnoreRest<DockerRepository>(new FilterAnd(new FilterFieldEqualTo("RepositoryName", RepositoryName)));
            return Repository;
        }
        #endregion

        #region Cleanup Methods
        public async Task<int> CleanUnusedBlobs()
        {
            return 0;
        }

        public async Task<int> CleanUnmanagedRepositories()
        {
            Log.Informational("Cleaning unmanaged repositories...");

            List<DockerRepository> Repositories = (await Database.Find<DockerRepository>()).ToList();

            for (int i = Repositories.Count - 1; i >= 0; i--)
            {
                DockerRepository Repository = Repositories[i];
                if (!((await Repository.GetOwner()) is null))
                {
                    Repositories.RemoveAt(i);
                }
            }

            Task[] DeletionTasks = new Task[Repositories.Count];

            for (int i = 0; i < Repositories.Count; i++)
            {
                try
                {
                    int ci = i;
                    DeletionTasks[i] = Task.Run(async () =>
                    {
                        await Database.FindDelete<DockerManifest>(new FilterAnd(new FilterFieldEqualTo("RepositoryName", Repositories[ci].RepositoryName)));
                        await Database.Delete(Repositories[ci]);
                    });
                }
                catch (Exception ex)
                {
                    Log.Error(ex);
                    continue;
                }
            }

            int Deletions = 0;

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

            Log.Informational("Docker Registry cleaned, " + Deletions + " repositories removed");
            return Deletions;
        }
        #endregion

        #region Http Helpers
        private static void Prepare(HttpRequest Request, out string RepositoryName, out string ApiResource, out string ReferenceString)
        {
            List<string> Portions = Request.SubPath.Split("/", StringSplitOptions.RemoveEmptyEntries).ToList();
            List<string> ApiResourceList = new List<string>();

            RepositoryName = null;

            if (Portions.Count == 0)
            {
                ApiResource = "/";
                ReferenceString = null;
                return;
            }

            if (Portions.Count == 1 && Portions[0] == "_catalog")
            {
                ApiResource = "/_catalog";
                ReferenceString = null;
                return;
            }

            List<string> RepositoryNames = new List<string>();

            // Get repository name
            while (Portions.Count() > 0)
            {
                if (Portions[0] == "manifests" || Portions[0] == "blobs" || Portions[0] == "tags")
                {
                    if (RepositoryName == String.Empty)
                        throw new BadRequestException(new DockerError(DockerErrorCode.NAME_INVALID, "Repository name cannot start with \"manifests\", \"blobs\", or \"tags\""), apiHeader);
                    break;
                }
                RepositoryNames.Add(Portions[0]);
                Portions.RemoveAt(0);
            }

            RepositoryName = string.Join("/", RepositoryNames);

            if (string.IsNullOrEmpty(RepositoryName))
                throw new NotFoundException(new DockerErrors(DockerErrorCode.NAME_UNKNOWN, "Repository name not known to registry."), apiHeader);

            if (!DockerRepository.ValidateRepositoryName(RepositoryName))
                throw new NotFoundException(new DockerErrors(DockerErrorCode.NAME_INVALID, "Invalid repository name."), apiHeader);

            // get resource name
            while (Portions.Count() > 0)
            {
                if (ApiResourceList.Count() == 0)
                {
                    if (Portions[0] == "manifests" || Portions[0] == "blobs" || Portions[0] == "tags")
                    {
                        ApiResourceList.Add(Portions[0]);
                        Portions.RemoveAt(0);
                        continue;
                    }
                }
                else if (ApiResourceList.Count() == 1)
                {
                    if (Portions[0] == "uploads" || Portions[0] == "list")
                    {
                        ApiResourceList.Add(Portions[0]);
                        Portions.RemoveAt(0);
                        continue;
                    }
                }
                break;
            }

            ApiResource = "/" + String.Join('/', ApiResourceList);

            if (
                ApiResource != "/manifests" &&
                ApiResource != "/blobs" &&
                ApiResource != "/blobs/uploads" &&
                ApiResource != "/tags/list"
                )
                throw new BadRequestException(new DockerError(DockerErrorCode.UNSUPPORTED, "The operation is unsupported."), apiHeader);


            if (Portions.Count() > 1)
                throw new BadRequestException(new DockerError(DockerErrorCode.UNSUPPORTED, "The operation is unsupported."), apiHeader);

            ReferenceString = Portions.Count() > 0 ? Portions[0] : null; // either tag, digest or upload uuid
        }
        private static void SetApiHeader(HttpResponse Response)
        {
            Response.SetHeader(apiHeader.Key, apiHeader.Value);
        }
        public static void SetLastHeader(HttpResponse Response, string BaseQuery, object Result, Variables Pagination)
        {
            if (Result is Array A)
            {
                int i = A.Length;
                if (i > 0)
                {
                    object LastItem = A.GetValue(i - 1);
                    StringBuilder sb = new StringBuilder();

                    sb.Append(Gateway.GetUrl(BaseQuery));

                    if (Pagination.TryGetVariable("N", out Variable v))
                    {
                        sb.Append("n=");
                        sb.Append(Expression.ToExpressionString(v.ValueObject));
                        sb.Append('&');
                    }

                    sb.Append("last=");
                    sb.Append(LastItem.ToString());
                    sb.Append("; rel=\"next\"");

                    Response.SetHeader("Link", sb.ToString());
                }
            }
        }
        public static bool IsPaginated(HttpRequest Request, out int First, out int Count)
        {
            Count = -1;
            First = 0;
            if (Request.Header.TryGetQueryParameter("n", out string NStr))
            {
                if (!int.TryParse(NStr, out int N) || N < 0)
                    throw new BadRequestException(new DockerErrors(DockerErrorCode.PAGINATION_NUMBER_INVALID, "Invalid number of results requested."), apiHeader);
                Count = N;
            }

            if (Request.Header.TryGetQueryParameter("last", out string LastStr))
            {
                if (!int.TryParse(LastStr, out int Last) || Last < 0)
                    throw new BadRequestException(new DockerErrors(DockerErrorCode.PAGINATION_NUMBER_INVALID, "Invalid last "), apiHeader);

                First = Last;
            }

            if (Count < 0)
                return false;

            return true;
        }
        #endregion

        #region Sniffers

        // <summary>
        /// Registers a web sniffer on the registry.
        /// </summary>
        /// <param name="SnifferId">Sniffer ID</param>
        /// <param name="Request">HTTP Request for sniffer page.</param>
        /// <param name="UserVariable">Name of user variable.</param>
        /// <param name="Privileges">Privileges required to view content.</param>
        /// <returns>Code to embed into page.</returns>
        public static string RegisterSniffer(string SnifferId, HttpRequest Request,
            string UserVariable, params string[] Privileges)
        {
            return Gateway.AddWebSniffer(SnifferId, Request, observable, UserVariable, Privileges);
        }

        #endregion

        /// <summary>
        /// Disposes of the resource.
        /// </summary>
        public void Dispose()
        {
            this.DisposeAsync().Wait();
        }

        /// <summary>
        /// Disposes of the resource.
        /// </summary>
        public async Task DisposeAsync()
        {
            DanglingDockerBlob[] DanglingBlobs = (await Database.Find<DanglingDockerBlob>()).ToArray();

            if (DanglingBlobs is null)
                return;

            for (int i = 0; i < DanglingBlobs.Length; i++)
            {
                await Database.Delete(DanglingBlobs[i]);
                await this.blobManager.DeleteBlob(DanglingBlobs[i].Digest);
            }
        }

        /// <summary>
        /// Returns Docker Registry error content for common HTTP error codes (if not provided by resource).
        /// </summary>
        /// <param name="StatusCode">HTTP Status code to return.</param>
        /// <returns>Custom content, or null if none.</returns>
        public override Task<object> DefaultErrorContent(int StatusCode)
        {
            return StatusCode switch
            {
                429 => Task.FromResult<object>(new DockerErrors(DockerErrorCode.DENIED, "Requested access to the resource is denied due to rate limitations.")),
                401 => Task.FromResult<object>(new DockerErrors(DockerErrorCode.UNAUTHORIZED, "Authentication required.")),
                _ => base.DefaultErrorContent(StatusCode),
            };
        }
    }
}
