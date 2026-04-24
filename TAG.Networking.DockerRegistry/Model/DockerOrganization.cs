
using System;
using System.Threading.Tasks;
using Waher.Persistence;
using Waher.Persistence.Attributes;
using Waher.Security;
namespace TAG.Networking.DockerRegistry.Model
{
    [TypeName(TypeNameSerialization.FullName)]
    [Index("OrganizationName")]
    public class DockerOrganization : DockerActor, IDashboardAuthorizable
    {
        /// <summary>
        /// A Docker User
        /// </summary>
        public DockerOrganization()
        {
        }

        /// <summary>
        /// The username of the broker account
        /// </summary>
		public CaseInsensitiveString OrganizationName { get; set; }

        // TODO: Fine grain permissions
        public async Task<bool> IsAuthorized(IUser User, string Privilege)
        {
            
            if (User.HasPrivilege(DashboardPrivileges.Admin))
                return true;

            CaseInsensitiveString Org = await DashboardPrivileges.GetUsersOrganizationName(User);

            if (OrganizationName == Org && User.HasPrivilege(Privilege))
                return true;

            return false;
        }
    }
}