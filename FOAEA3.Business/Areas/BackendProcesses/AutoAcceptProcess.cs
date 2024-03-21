using FOAEA3.Business.Areas.Application;
using System.Security.Claims;

namespace FOAEA3.Business.Areas.BackendProcesses;

internal class AutoAcceptProcess
{
    private readonly IFoaeaConfigurationHelper Config;
    private readonly IRepositories DB;
    private readonly IRepositories_Finance DBfinance;
    private readonly ClaimsPrincipal User;

    public AutoAcceptProcess(IRepositories repositories, IRepositories_Finance repositories_finance,
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

        await prodAudit.Insert("Auto Accept Process", $"Auto Accept Process Started", "O");

        var interceptionManager = new InterceptionManager(DB, DBfinance, Config, User);

        foreach (string enfService in Config.AutoAccept)
            await interceptionManager.AutoAcceptVariations(enfService);

        await prodAudit.Insert("Auto Accept Process", $"Auto Accept Process Completed", "O");
    }

    public async Task Run(string enfService)
    {
        var prodAudit = DB.ProductionAuditTable;

        await prodAudit.Insert("Auto Accept Process", $"Auto Accept Process for {enfService} Started", "O");

        if (Config.AutoAccept.Contains(enfService.ToUpper()))
        {
            var interceptionManager = new InterceptionManager(DB, DBfinance, Config, User);
            await interceptionManager.AutoAcceptVariations(enfService);
        }
        else
            await prodAudit.Insert("Auto Accept Process", $"Error: {enfService} not in valid auto accept list", "O");

        await prodAudit.Insert("Auto Accept Process", $"Auto Accept Process for {enfService} Completed", "O");
    }
}
