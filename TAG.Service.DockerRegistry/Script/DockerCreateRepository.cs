using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Model;
using Waher.Persistence;
using Waher.Persistence.Filters;
using Waher.Script;
using Waher.Script.Abstraction.Elements;
using Waher.Script.Exceptions;
using Waher.Script.Model;
using Waher.Script.Objects;

namespace TAG.Service.DockerRegistry.Script
{
    /// <summary>
    /// Creates a docker user.
    /// </summary>
    public class DockerCreateRepository : FunctionMultiVariate
    {
        /// <summary>
        /// Creates a repository on the docker registry
        /// </summary>
        /// <param name="RepositoryName">Repository name</param>
        /// <param name="Owner">Guid of the owner of the repository</param>
        /// <param name="IsPrivate">Is it private</param>
        /// <param name="Start">Start position in script expression.</param>
        /// <param name="Length">Length of expression covered by node.</param>
        /// <param name="Expression">Expression.</param>
        public DockerCreateRepository(ScriptNode RepositoryName, ScriptNode Owner, ScriptNode IsPrivate, int Start, int Length, Expression Expression)
            : base(new ScriptNode[] { RepositoryName, Owner, IsPrivate }, argumentTypes3Normal, Start, Length, Expression)
        {
        }

        /// <summary>
        /// Name of the function
        /// </summary>
        public override string FunctionName => "DockerCreateRepository";

        /// <summary>
        /// Name of the function
        /// </summary>
        public override string[] DefaultArgumentNames => new string[] { "Repository name", "Owner GUID", "Is private" };

        /// <summary>
        /// Evaluates the function.
        /// </summary>
        /// <param name="Arguments">Function arguments.</param>
        /// <param name="Variables">Variables collection.</param>
        /// <returns>Function result.</returns>
        public override IElement Evaluate(IElement[] Arguments, Variables Variables)
        {
            return this.EvaluateAsync(Arguments, Variables).Result;
        }


        /// <summary>
        /// Evaluates the function.
        /// </summary>
        /// <param name="Arguments">Function arguments.</param>
        /// <param name="Variables">Variables collection.</param>
        /// <returns>Function result.</returns>
        public override async Task<IElement> EvaluateAsync(IElement[] Arguments, Variables Variables)
        {
            if (Arguments.Length != 3)
                throw new ScriptRuntimeException("Expected 3 arguments.", this);

            if (!(Arguments[0].AssociatedObjectValue is string Name))
                throw new ScriptRuntimeException("First argument should be a string.", this);

            DockerRepository Prev = await Database.FindFirstIgnoreRest<DockerRepository>(new FilterAnd(new FilterFieldEqualTo("RepositoryName", Name)));
            if (!(Prev is null))
                throw new ScriptRuntimeException("There already is a Repository with the name " + Name, this);

            if (!(Arguments[1].AssociatedObjectValue is string GuidString && Guid.TryParse(GuidString, out Guid OwnerGuid)))
                throw new ScriptRuntimeException("Second argument should be a guid string.", this);

            if (!(Arguments[2].AssociatedObjectValue is bool IsPrivate))
                throw new ScriptRuntimeException("Third agrument should be a bool.", this);

            DockerActor Actor = await Database.FindFirstIgnoreRest<DockerActor>(new FilterAnd(new FilterFieldEqualTo("Guid", OwnerGuid)));

            if (Actor is null)
                throw new ScriptRuntimeException("No owner with Guid " + OwnerGuid.ToString(), this);

            DockerRepository NewRepo = await DockerRepository.CreateInsertRepository(Name, IsPrivate, OwnerGuid);

            return new ObjectValue(NewRepo);
        }
    }
}
