namespace FOAEA3.Business.Areas.Financials;

public class ControlBatchManager
{
    private IRepositories DB { get; }
    private IRepositories_Finance DBfinance { get; }

    public ControlBatchManager(IRepositories db, IRepositories_Finance dbFinance)
    {
        DB = db;
        DBfinance = dbFinance;
    }

    public async Task<List<BatchSimpleData>> GetReadyDivertFundsBatches(string enfSrv_Cd, string enfSrv_Loc_Cd)
    {
        return await DBfinance.ControlBatchRepository.GetReadyDivertFundsBatches(enfSrv_Cd, enfSrv_Loc_Cd);
    }


    public async Task CloseControlBatch(string batchId)
    {
        await DBfinance.ControlBatchRepository.CloseControlBatch(batchId);
    }

    public async Task UpdateBatch(ControlBatchData batch)
    {
        await DBfinance.ControlBatchRepository.UpdateBatch(batch);
    }

    public async Task UpdateBatchStateFtpProcessed(string batchId, int recordCount)
    {
        await DBfinance.ControlBatchRepository.UpdateBatchStateFtpProcessed(batchId, recordCount);
    }

    public async Task<(string, string, string, string)> CreateXFControlBatch(ControlBatchData batchData)
    {
        var controlBatchDB = DBfinance.ControlBatchRepository;

        var values = new ControlBatchData()
        {
            Batch_Id = batchData.Batch_Id,
            EnfSrv_Src_Cd = batchData.EnfSrv_Src_Cd,
            DataEntryBatch_Id = batchData.DataEntryBatch_Id,
            BatchType_Cd = batchData.BatchType_Cd,
            Batch_Post_Dte = batchData.Batch_Post_Dte,
            Batch_Compl_Dte = batchData.Batch_Compl_Dte,
            Medium_Cd = batchData.Medium_Cd,
            SourceRecCnt = batchData.SourceRecCnt,
            DoJRecCnt = batchData.DoJRecCnt,
            SourceTtlAmt_Money = batchData.SourceTtlAmt_Money,
            DoJTtlAmt_Money = batchData.DoJTtlAmt_Money,
            BatchLiSt_Cd = batchData.BatchLiSt_Cd,
            Batch_Reas_Cd = batchData.Batch_Reas_Cd,
            Batch_Pend_Ind = batchData.Batch_Pend_Ind,
            PendTtlAmt_Money = batchData.PendTtlAmt_Money,
            FeesTtlAmt_Money = batchData.FeesTtlAmt_Money
        };

        return await controlBatchDB.CreateXFControlBatch(values);
    }

    public async Task<ControlBatchData> CreateControlBatch(ControlBatchData controlBatchData)
    {
        string batchID;
        string reasonCode;

        (_, batchID, reasonCode, _) = await CreateXFControlBatch(controlBatchData);

        controlBatchData.Batch_Id = batchID;

        if (int.TryParse(reasonCode, out int code))
            controlBatchData.Batch_Reas_Cd = code;

        return controlBatchData;
    }

    public async Task<ControlBatchData> GetControlBatch(string batchId)
    {
        var controlBatchDB = DBfinance.ControlBatchRepository;
        return await controlBatchDB.GetControlBatch(batchId);
    }

    public async Task<DateTime> GetDateLastUIBatchLoaded()
    {
        return await DB.InterceptionTable.GetDateLastUIBatchLoaded();
    }
}
