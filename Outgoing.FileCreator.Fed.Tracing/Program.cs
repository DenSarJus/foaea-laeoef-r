using System.Threading.Tasks;

namespace Outgoing.FileCreator.Fed.Tracing;

class Program
{
    static async Task Main(string[] args)
    {
        await OutgoingFileCreatorFedTracing.Run(args);
    }
}
