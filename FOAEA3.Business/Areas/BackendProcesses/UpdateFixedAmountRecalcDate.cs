using FOAEA3.Business.Areas.Application;
using System.Security.Claims;

namespace FOAEA3.Business.Areas.BackendProcesses;

public class UpdateFixedAmountRecalcDate
{
    private readonly IRepositories DB;
    private readonly IRepositories_Finance DBfinance;
    private readonly IFoaeaConfigurationHelper Config;
    private readonly ClaimsPrincipal User;

    public UpdateFixedAmountRecalcDate(IRepositories repositories, IRepositories_Finance repositoriesFinance,
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

        await prodAudit.Insert("Update Fixed Amount Date", "Update Fixed Amount Recalc Date Started", "O");

        var interceptionManager = new InterceptionManager(DB, DBfinance, Config, User);
        var summSmryApplications = await interceptionManager.GetFixedAmountRecalcDateRecords();

        var amountOwedProcess = new AmountOwedProcess(DB, DBfinance);
        await amountOwedProcess.Run(summSmryApplications);

        await prodAudit.Insert("Update Fixed Amount Date", "Update Fixed Amount Recalc Date Completed", "O");
    }
}
