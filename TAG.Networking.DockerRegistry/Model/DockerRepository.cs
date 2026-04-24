using System;
using System.Data;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Waher.Persistence;
using Waher.Persistence.Attributes;
using Waher.Persistence.Filters;
using Waher.Security;

namespace TAG.Networking.DockerRegistry.Model
{
    [CollectionName("DockerRepository")]
    [TypeName(TypeNameSerialization.None)]
    [Index("RepositoryName")]
    [Index("OwnerGuid")]
    [Index("IsPrivate")]
    public class DockerRepository : IDashboardAuthorizable
    {
        private static readonly Regex RootSegmentPattern = new Regex(@"^[A-Za-z0-9._-]+$", RegexOptions.Compiled);

        private string objectId;
        private string repositoryName;
        private Guid ownerGuid;
        private bool isPrivate;

        /// <summary>
        /// Object ID
        /// </summary>
        [ObjectId]
        public string ObjectId
        {
            get => this.objectId;
            set => this.objectId = value;
        }

        /// <summary>
        /// Guid of the repository
        /// </summary>
        public Guid Guid { get; set; }

        /// <summary>
        /// Name of the repository.
        /// </summary>
        public string RepositoryName
        {
            get => this.repositoryName;
            set => this.repositoryName = value;
        }

        /// <summary>
        /// Is the repository private?
        /// </summary>
        public bool IsPrivate
        {
            get => this.isPrivate;
            set => this.isPrivate = value;
        }

        /// <summary>
        /// Repository owner Guid
        /// </summary>
        public Guid OwnerGuid
        {
            get => this.ownerGuid;
            set => this.ownerGuid = value;
        }

        public DockerRepository()
        {

        }

        public DockerRepository(string RepositoryName, DockerActor Actor)
        {
            this.repositoryName = RepositoryName;
            this.ownerGuid = Actor.Guid;
        }

        // TODO: Fine grain permissions
        public async Task<bool> IsAuthorized(IUser User, string Privilege)
        {
            if (User.HasPrivilege(DashboardPrivileges.Admin))
                return true;

            DockerActor Owner = await Database.FindFirstIgnoreRest<DockerActor>(new FilterAnd(new FilterFieldEqualTo("Guid", OwnerGuid)));

            if (Owner is DockerUser DockerUser)
                return await DockerUser.IsAuthorized(User, Privilege);

            if (Owner is DockerOrganization Org)
                return await Org.IsAuthorized(User, Privilege);

            return false;
        }

        public async Task<bool> HasPermission(DockerActor Actor, RepositoryAction Action)
        {
            if (Actor == null)
                return false;

            if (Actor.Guid == OwnerGuid)
                return true;

            switch (Action)
            {
                case RepositoryAction.Pull:
                    if (!IsPrivate)
                        return true;
                    break;
                case RepositoryAction.Delete:
                case RepositoryAction.Push:
                    if (Actor.Guid == OwnerGuid)
                        return true;
                    break;
            }

            DockerRepositoryPrivilege Privileges = await GetPrivileges(Actor);

            if (Privileges is null)
                return false;

            switch (Action)
            {
                case RepositoryAction.Pull:
                    if (Privileges.AllowRead)
                        return true;
                    break;
                case RepositoryAction.Delete:
                case RepositoryAction.Push:
                    if (Privileges.AllowWrite)
                        return true;
                    break;
            }

            return false;
        }

        public async Task<DockerActor> GetOwner()
        {
            return await Database.FindFirstDeleteRest<DockerActor>(new FilterAnd(new FilterFieldEqualTo("Guid", OwnerGuid)));
        }

        public async Task<DockerRepositoryPrivilege> GetPrivileges(DockerActor Actor)
        {
            return await Database.FindFirstIgnoreRest<DockerRepositoryPrivilege>(
                new FilterAnd(
                    new FilterFieldEqualTo("ActorGuid", Actor.Guid),
                    new FilterFieldEqualTo("RepositoryGuid", this.Guid)
                )
            );
        }

        public async Task CreatePrivileges(DockerActor Actor, bool AllowRead, bool AllowWrite)
        {
            DockerRepositoryPrivilege Prev = await Database.FindFirstDeleteRest<DockerRepositoryPrivilege>(
                new FilterAnd(
                    new FilterFieldEqualTo("ActorGuid", Actor.Guid),
                    new FilterFieldEqualTo("RepositoryGuid", this.Guid)
                ));

            if (!(Prev is null))
            {
                Prev.AllowRead = AllowRead;
                Prev.AllowWrite = AllowWrite;
                await Database.Update(Prev);
                return;
            }

            DockerRepositoryPrivilege NewPrivilege = new DockerRepositoryPrivilege()
            {
                ActorGuid = Actor.Guid,
                RepositoryGuid = this.Guid,
                AllowWrite = AllowWrite,
                AllowRead = AllowRead,
            };

            await Database.Insert(NewPrivilege);
        }

        public static bool ValidateRepositoryName(string name)
        {
            Regex ComponentRegex = new Regex("^[a-z0-9]+(?:[._-][a-z0-9]+)*$", RegexOptions.Compiled);

            if (string.IsNullOrEmpty(name))
                return false;

            if (name.Length > 255)
                return false;

            string[] components = name.Split('/');
            foreach (var component in components)
            {
                if (string.IsNullOrEmpty(component))
                    return false;

                if (!ComponentRegex.IsMatch(component))
                    return false;
            }

            return true;
        }

        public static bool IsValidRootName(string Name)
        {
            if (string.IsNullOrWhiteSpace(Name))
                return false;

            if (!Name.EndsWith('/'))
                return false;

            var segment = Name.Substring(0, Name.Length - 1); // drop trailing '/'

            if (segment.Length == 0)
                return false;

            if (segment.Contains("/"))
                return false;

            if (!RootSegmentPattern.IsMatch(segment))
                return false;

            return true;
        }

        public async static Task<DockerRepository> CreateInsertRepository(string Name, bool IsPrivate, Guid OwnerGuid)
        {
            DockerRepository Prev = await Database.FindFirstIgnoreRest<DockerRepository>(new FilterAnd(new FilterFieldEqualTo("RepositoryName", Name)));

            if (!(Prev is null))
                throw new DuplicateNameException("Repository with name " + Name + " already exists.");

            DockerRepository NewRepo = new DockerRepository()
            {
                Guid = Guid.NewGuid(),
                RepositoryName = Name,
                IsPrivate = IsPrivate,
                OwnerGuid = OwnerGuid,
            };

            await Database.Insert(NewRepo);

            return NewRepo;
        }

        public enum RepositoryAction
        {
            Pull,
            Push,
            Delete,
        }
    }
}
