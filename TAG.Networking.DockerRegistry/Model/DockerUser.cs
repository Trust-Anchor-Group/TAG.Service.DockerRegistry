using System;
using System.Threading.Tasks;
using Waher.Persistence;
using Waher.Persistence.Attributes;
using Waher.Security;

namespace TAG.Networking.DockerRegistry.Model
{
    [TypeName(TypeNameSerialization.FullName)]
    [Index("AccountName")]
    public class DockerUser : DockerActor, IDashboardAuthorizable
    {
        /// <summary>
        /// A Docker User
        /// </summary>
        public DockerUser()
        {

        }

        /// <summary>
        /// The username of the broker account
        /// </summary>
		public CaseInsensitiveString AccountName { get; set; }

        // TODO: Fine grain permissions
        public async Task<bool> IsAuthorized(IUser User, string Privilege)
        {
            if (User.HasPrivilege(DashboardPrivileges.Admin))
                return true;

            return false;
        }
    }
}
