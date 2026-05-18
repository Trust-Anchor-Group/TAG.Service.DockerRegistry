using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Model;
using Waher.Networking.HTTP;
using Waher.Persistence;
using Waher.Script;
using Waher.Script.Abstraction.Elements;
using Waher.Script.Exceptions;
using Waher.Script.Model;
using Waher.Script.Objects;
using Waher.Security;

namespace TAG.Service.DockerRegistry.Script
{
    /// <summary>
    /// Creates a docker user.
    /// </summary>
    public class DockerDashboardAssertPermisions : DockerDashboardHasPermisions
    {
        /// <summary>
        /// Creates a docker user on the docker registry.
        /// </summary>
        /// <param name="DockerResource">What docker resource is needed access for</param>
        /// <param name="AdminPrivileges">What admin privileges are needed on the docker resource</param>
        /// <param name="Start">Start position in script expression.</param>
        /// <param name="Length">Length of expression covered by node.</param>
        /// <param name="Expression">Expression.</param>
        public DockerDashboardAssertPermisions(ScriptNode DockerResource, ScriptNode AdminPrivileges, int Start, int Length, Expression Expression)
            : base(DockerResource, AdminPrivileges, Start, Length, Expression)
        {
        }

        /// <summary>
        /// Name of the function
        /// </summary>
        public override string FunctionName => "DockerDashboardAssertPermisions";

        /// <summary>
        /// Name of the function
        /// </summary>
        public override string[] DefaultArgumentNames => new string[] { "Docker Resource (Repository, Organization, Docker User, etc)", "Admin Privileges" };

        /// <summary>
        /// Evaluates the function.
        /// </summary>
        /// <param name="Arguments">Function arguments.</param>
        /// <param name="Variables">Variables collection.</param>
        /// <returns>Function result.</returns>
        public override IElement Evaluate(IElement[] Arguments, Variables Variables)
        {
            return Task.Run(async () => await this.EvaluateAsync(Arguments, Variables)).Result;
        }

        /// <summary>
        /// Evaluates the function.     
        /// </summary>
        /// <param name="Arguments">Function arguments.</param>
        /// <param name="Variables">Variables collection.</param>
        /// <returns>Function result.</returns>
        public override async Task<IElement> EvaluateAsync(IElement[] Arguments, Variables Variables)
        {
            if (!((bool)(await base.EvaluateAsync(Arguments, Variables)).AssociatedObjectValue))
                throw new ForbiddenException("Not authorized for this resource.");

            return new BooleanValue(true);
        }
    }
}
