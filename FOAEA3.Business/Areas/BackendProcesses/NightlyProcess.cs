using System.Security.Claims;

namespace FOAEA3.Business.Areas.BackendProcesses;

public class NightlyProcess
{
    private readonly IRepositories DB;
    private readonly IRepositories_Finance DBfinance;
    private readonly IFoaeaConfigurationHelper Config;
    private readonly ClaimsPrincipal User;

    public NightlyProcess(IRepositories repositories, IRepositories_Finance repositoriesFinance,
                          IFoaeaConfigurationHelper config, ClaimsPrincipal user)
    {
        DB = repositories;
        DBfinance = repositoriesFinance;
        Config = config;
        User = user;
    }

    public async Task Run()
    {
        var prodAudit = DB.ProductionAuditTable;

        await prodAudit.Insert("Nightly Process", "Nightly Process Started", "O");

        var completeI01process = new CompletedInterceptionsProcess(DB, DBfinance, Config, User);
        await completeI01process.Run();

        var amountOwedProcess = new AmountOwedProcess(DB, DBfinance);
        await amountOwedProcess.Run();

        var divertFundProcess = new DivertFundsProcess(DB, DBfinance);
        await divertFundProcess.Run();

        ChequeReqProcess.Run();

        await prodAudit.Insert("Nightly Process", "Nightly Process Completed", "O");
    }
}
