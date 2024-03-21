using FOAEA3.Business.Areas.Application;
using System.Security.Claims;

namespace FOAEA3.Business.Areas.BackendProcesses;

public class DeleteReconciliationDataProcess
{
    private readonly IFoaeaConfigurationHelper Config;
    private readonly IRepositories DB;
    private readonly IRepositories_Finance DBfinance;
    private readonly ClaimsPrincipal User;

    public DeleteReconciliationDataProcess(IRepositories repositories, IRepositories_Finance repositories_finance,
                                    IFoaeaConfigurationHelper config, ClaimsPrincipal user)
    {
        Config = config;
        DB = repositories;
        DBfinance = repositories_finance;
        User = user;
    }

    public async Task Run()
    {
        var prodAudit = DB.ProductionAuditTable;

        await prodAudit.Insert("Delete Reconciliation Process", $"Delete Reconciliation Process Started", "O");

        var interceptionManager = new InterceptionManager(DB, DBfinance, Config, User);

        await interceptionManager.MessageBrokerCRAReconciliation();

        await prodAudit.Insert("Delete Reconciliation Process", $"Delete Reconciliation Process Completed", "O");
    }
}
