
namespace FileBroker.Business;

public class ApiBrokerManager
{
    private RepositoryList DB { get; init; }
    private IFileBrokerConfigurationHelper Config { get; init; }

    public ApiBrokerManager(RepositoryList repositories, IFileBrokerConfigurationHelper config)
    {
        DB = repositories;
        Config = config;
    }

    public  async void Run()
    {

        const int MAX_LOOP = 4;
        int count = 1;

        var nextActive = await DB.ApiTransferLog.GetNextActiveApiTransferLog();

        do
        {
            if (nextActive != null)
            {
                var details = await DB.ApiTransferLog.GetApiTransferLogDetailsForApiTransferLog(nextActive.ApiTransferLogId);

                var outstanding = details.Where(m => !m.IsStoredLocally).ToList();
                if (outstanding.Count == 0)
                    return;

                foreach (var detail in details)
                {
                    if (!detail.IsStoredLocally)
                    {
                        // TODO: check where it is at and continue work
                    }
                }

                outstanding = details.Where(m => !m.IsStoredLocally).ToList();
                if (outstanding.Count == 0)
                    GenerateIncomingFile(details);
            }

            nextActive = await DB.ApiTransferLog.GetNextActiveApiTransferLog();

            count++;

        } while ((nextActive != null) && (count <= MAX_LOOP));

    }

    private void GenerateIncomingFile(List<ApiTransferLogDetailData> details)
    {
        throw new NotImplementedException();
    }
}
