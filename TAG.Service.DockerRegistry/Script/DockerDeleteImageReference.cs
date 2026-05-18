using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Model;
using Waher.Script;
using Waher.Script.Abstraction.Elements;
using Waher.Script.Exceptions;
using Waher.Script.Model;
using Waher.Script.Objects;

namespace TAG.Service.DockerRegistry.Script
{
    /// <summary>
    /// Deletes a docker image reference.
    /// </summary>
    public class DockerDeleteImageReference : FunctionMultiVariate
    {
        /// <summary>
        /// Deletes an image reference from a repository.
        /// </summary>
        /// <param name="Repository">Repository owning the image reference.</param>
        /// <param name="Tag">Tag of the image reference.</param>
        /// <param name="Start">Start position in script expression.</param>
        /// <param name="Length">Length of expression covered by node.</param>
        /// <param name="Expression">Expression.</param>
        public DockerDeleteImageReference(ScriptNode Repository, ScriptNode Tag, int Start, int Length, Expression Expression)
            : base(new ScriptNode[] { Repository, Tag }, argumentTypes2Normal, Start, Length, Expression)
        {
        }

        /// <summary>
        /// Name of the function
        /// </summary>
        public override string FunctionName => "DockerDeleteImageReference";

        /// <summary>
        /// Default argument names.
        /// </summary>
        public override string[] DefaultArgumentNames => new string[] { "Repository", "Tag" };

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
                throw new ScriptRuntimeException("Expected 2 arguments.", this);

            if (!(Arguments[0].AssociatedObjectValue is DockerRepository Repository))
                throw new ScriptRuntimeException("First argument should be a docker repository.", this);

            if (!(Arguments[1].AssociatedObjectValue is string Tag) || string.IsNullOrWhiteSpace(Tag))
                throw new ScriptRuntimeException("Second argument should be a tag string.", this);

            RegistryService Service = RegistryService.Instance;
            if (Service is null || Service.ManifestManager is null)
                throw new ScriptRuntimeException("Docker registry service is not available.", this);

            DockerActor Owner = await Repository.GetOwner();
            if (Owner is null)
                return new BooleanValue(false);

            await using WritableStorageHandle Handle = await Owner.GetWritableStorage();
            bool Deleted = await Service.ManifestManager.DeleteImageReference(Repository.RepositoryName, Tag, Handle);
            return new BooleanValue(Deleted);
        }
    }
}