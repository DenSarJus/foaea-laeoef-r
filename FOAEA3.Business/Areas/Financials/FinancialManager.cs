namespace FOAEA3.Business.Areas.Financials;

public class FinancialManager
{
    private IRepositories DB { get; }
    private IRepositories_Finance DBfinance { get; }

    public FinancialManager(IRepositories repositories, IRepositories_Finance repositoriesFinance)
    {
        DB = repositories;
        DBfinance = repositoriesFinance;
    }

    public async Task<List<CR_PADReventData>> GetActiveCR_PADRevents(string enfSrv)
    {
        return await DBfinance.FinancialRepository.GetActiveCR_PADRevents(enfSrv);
    }

    public async Task CloseCR_PADRevents(string batchId, string enfSrv)
    {
        await DBfinance.FinancialRepository.CloseCR_PADRevents(batchId, enfSrv);
    }

    public async Task<List<BlockFundData>> GetBlockFundsData(string enfSrv)
    {
        return await DBfinance.FinancialRepository.GetBlockFundsData(enfSrv);
    }

    public async Task<List<DivertFundData>> GetDivertFundsData(string enfSrv, string batchId)
    {
        return await DBfinance.FinancialRepository.GetDivertFundsData(enfSrv, batchId);
    }

    public async Task<List<IFMSdata>> GetIFMSdata(string batchId)
    {
        return await DBfinance.FinancialRepository.GetIFMSdata(batchId);
    }

}
