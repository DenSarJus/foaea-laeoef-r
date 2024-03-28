using System.Threading.Tasks;

namespace Outgoing.FileCreator.Fed.LicenceDenial;

class Program
{
    static async Task Main(string[] args)
    {
        await OutgoingFileCreatorFedLicenceDenial.Run(args);
    }
}
