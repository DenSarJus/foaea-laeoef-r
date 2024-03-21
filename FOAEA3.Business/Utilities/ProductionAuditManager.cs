namespace FOAEA3.Business.Utilities;

internal class ProductionAuditManager
{
    private IRepositories DB { get; }

    public ProductionAuditManager(IRepositories repositories)
    {
        DB = repositories;
    }

    public async Task Insert(string processName, string description, string audience, DateTime? completedDate = null)
    {
        await DB.ProductionAuditTable.Insert(processName, description, audience, completedDate);
    }

    public async Task Insert(ProductionAuditData productionAuditData)
    {
        await DB.ProductionAuditTable.Insert(productionAuditData);
    }
}
