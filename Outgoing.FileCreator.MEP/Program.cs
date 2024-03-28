using System.Threading.Tasks;

namespace Outgoing.FileCreator.MEP;

class Program
{
    static async Task Main(string[] args)
    {
        await OutgoingFileCreatorMEP.Run(args);
    }
}
