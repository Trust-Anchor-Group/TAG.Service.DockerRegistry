using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Model;
using Waher.Networking.HTTP;
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
    public class DockerDashboardHasPermisions : FunctionMultiVariate
    {
        /// <summary>
        /// Creates a docker user on the docker registry.
        /// </summary>
        /// <param name="DockerResource">What docker resource is needed access for</param>
        /// <param name="AdminPrivileges">What admin privileges are needed on the docker resource</param>
        /// <param name="Start">Start position in script expression.</param>
        /// <param name="Length">Length of expression covered by node.</param>
        /// <param name="Expression">Expression.</param>
        public DockerDashboardHasPermisions(ScriptNode DockerResource, ScriptNode AdminPrivileges, int Start, int Length, Expression Expression)
            : base(new ScriptNode[] { DockerResource, AdminPrivileges }, argumentTypes2Normal, Start, Length, Expression)
        {
        }

        /// <summary>
        /// Name of the function
        /// </summary>
        public override string FunctionName => "DockerDashboardHasPermisions";

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
            if (Arguments.Length != 2)
                throw new ScriptRuntimeException("2 arguments are needed", this);

            IUser User = null;

            if ((Variables.TryGetVariable("QuickLoginUser", out Variable QuickLoginUserVariable) && QuickLoginUserVariable.ValueElement.AssociatedObjectValue is IUser QuickLoginUser))
                User = QuickLoginUser;
            else if (Variables.TryGetVariable("User", out Variable UserVariable) && UserVariable.ValueObject is IUser IUser)
                User = IUser;

            if (User is null)
                return new BooleanValue(false);

            if (!(Arguments[0].AssociatedObjectValue is IDashboardAuthorizable Resource))
                throw new ScriptException($"Argument {DefaultArgumentNames[0]} needs to be an authorizable resource (Implementing IDashboardAuthorizable.");

            if (!(Arguments[1].AssociatedObjectValue is string PrivilegeString))
                throw new InternalServerErrorException($"Argument {DefaultArgumentNames[1]} needs to be string.");

            bool Authorized = await Resource.IsAuthorized(User, PrivilegeString);

            if (!Authorized)
                return new BooleanValue(false);

            return new BooleanValue(true);
        }
    }
}
