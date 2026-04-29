using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TAG.Networking.DockerRegistry.Model;
using Waher.Networking.HTTP;
using Waher.Networking.Sniffers;
using Waher.Networking.XMPP.PubSub;
using Waher.Persistence;
using Waher.Persistence.Filters;


namespace TAG.Networking.DockerRegistry.Endpoints
{
    internal class TagsEndpoints : DockerEndpoints
    {
        public TagsEndpoints(string DockerRegistryFolder, ISniffer[] Sniffers)
             : base(DockerRegistryFolder, Sniffers)
        {

        }

        public async Task GET(HttpRequest Request, HttpResponse Response, DockerActor Actor, DockerRepository Repository, string Reference)
        {
            await AssertRepositoryPrivilages(Actor, Repository, DockerRepository.RepositoryAction.Pull, Request);

            ImageReference[] Images;
            if (RegistryServerV2.IsPaginated(Request, out int First, out int Count))
                Images = (await Database.Find<ImageReference>(First, Count, new FilterAnd(new FilterFieldEqualTo(nameof(ImageReference.RepositoryName), Repository.RepositoryName)))).ToArray();
            else
                Images = (await Database.Find<ImageReference>(new FilterAnd(new FilterFieldEqualTo(nameof(ImageReference.RepositoryName), Repository.RepositoryName)))).ToArray();
            
            Response.StatusCode = 200;
            await Response.Return(new Dictionary<string, object>()
            {
                { "name", Repository.RepositoryName },
                { "tags", Images.Select(Image => Image.Tag) }
            });
        }
    }
}
