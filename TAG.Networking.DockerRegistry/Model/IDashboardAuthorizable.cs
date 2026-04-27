using System;
using System.Threading.Tasks;
using Waher.Persistence;
using Waher.Persistence.Filters;
using Waher.Security;

namespace TAG.Networking.DockerRegistry.Model
{
    public static class DashboardPrivileges
    {
        public static readonly string Read = "DockerRegistry.Read";
        public static readonly string Create = "DockerRegistry.Create";
        public static readonly string Update = "DockerRegistry.Update";
        public static readonly string Delete = "DockerRegistry.Delete";

        public static readonly string All = "DockerRegistry";
        public static readonly string Admin = "Administrator";

        public static async Task<CaseInsensitiveString> GetUsersOrganizationName(IUser User)
        {
            if (User is ILegalIdentityUser LegalIdUser)
            {
                ILegalIdentityProperty Property = Array.Find(LegalIdUser.LegalIdentity.Properties, p => p.Name == "ORGNAME");
                if (!(Property is null))
                    return Property.Value;
            }
            
            return null;
        }
    }

    public interface IDashboardAuthorizable
    {
        public Task<bool> IsAuthorized(IUser User, string Privilege);
    }
}
