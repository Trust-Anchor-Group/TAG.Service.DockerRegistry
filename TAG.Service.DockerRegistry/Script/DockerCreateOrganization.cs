using System;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry;
using TAG.Networking.DockerRegistry.Model;
using Waher.Persistence;
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
    public class DockerCreateOrganization : FunctionMultiVariate
    {
        /// <summary>
        /// Creates a docker user on the docker registry.
        /// </summary>
        /// <param name="OrganizationName">Name of the broker account owning the docker user</param>
        /// <param name="MaxStorage">Max storage of unique blob storage</param>
        /// <param name="Start">Start position in script expression.</param>
        /// <param name="Length">Length of expression covered by node.</param>
        /// <param name="Expression">Expression.</param>
        public DockerCreateOrganization(ScriptNode OrganizationName, ScriptNode MaxStorage, int Start, int Length, Expression Expression)
            : base(new ScriptNode[] { OrganizationName, MaxStorage }, argumentTypes2Normal, Start, Length, Expression)
        {
        }

        /// <summary>
        /// Name of the function
        /// </summary>
        public override string FunctionName => "DockerCreateOrganization";

        /// <summary>
        /// Name of the function
        /// </summary>
        public override string[] DefaultArgumentNames => new string[] { "Organization Name", "Max storage (bytes)", };

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

            if (!(Arguments[0].AssociatedObjectValue is string Name))
                throw new ScriptRuntimeException("First argument should be a string.", this);

            if (!(Arguments[1].AssociatedObjectValue is double MaxStorageValue && MaxStorageValue == Math.Floor(MaxStorageValue)))
                throw new ScriptRuntimeException("Second argument should be an integer.", this);


            long MaxStorage = (long)MaxStorageValue;

            DockerOrganization NewOrganization = new DockerOrganization()
            {
                OrganizationName = Name,
                Guid = Guid.NewGuid(),
                StorageGuid = Guid.NewGuid(),
            };

            DockerStorage Storage = new DockerStorage()
            {
                MaxStorage = MaxStorage,
                Guid = NewOrganization.StorageGuid,
                BlobCounter = new DigestReferenceCounter[] { },
                UsedStorage = 0
            };

            await Database.Insert(Storage);
            await Database.Insert(NewOrganization);

            return new ObjectValue(NewOrganization);
        }
    }
}
