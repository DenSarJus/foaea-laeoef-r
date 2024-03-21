using FOAEA3.Model.IVR;

namespace FOAEA3.Business.Areas.IVR;

public class IVRManager
{
    private IIVRRepository DB { get; }

    public IVRManager(IIVRRepository db)
    {
        DB = db;
    }

    public async Task<CheckSinReturnData> GetSinCount(CheckSinGetData data)
    {
        return await DB.CheckSinCount(data);
    }

    public async Task<CheckCreditorIdReturnData> CheckCreditorId(CheckCreditorIdGetData data)
    {
        return await DB.CheckCreditorId(data);
    }

    public async Task<CheckControlCodeReturnData> CheckControlCode(CheckControlCodeGetData data)
    {
        return await DB.CheckControlCode(data);
    }

    public async Task<CheckDebtorIdReturnData> CheckDebtorId(CheckDebtorIdGetData data)
    {
        return await DB.CheckDebtorId(data);
    }

    public async Task<CheckDebtorLetterReturnData> CheckDebtorLetter(CheckDebtorLetterGetData data)
    {
        return await DB.CheckDebtorLetter(data);
    }

    public async Task<GetAgencyReturnData> GetAgency(GetAgencyGetData data)
    {
        return await DB.GetAgency(data);
    }

    public async Task<GetAgencyDebReturnData> GetAgencyDeb(GetAgencyDebGetData data)
    {
        return await DB.GetAgencyDeb(data);
    }

    public async Task<GetApplControlCodeReturnData> GetApplControlCode(GetApplControlCodeGetData data)
    {
        return await DB.GetApplControlCode(data);
    }

    public async Task<GetApplEnforcementCodeReturnData> GetApplEnforcementCode(GetApplEnforcementCodeGetData data)
    {
        return await DB.GetApplEnforcementCode(data);
    }

    public async Task<GetHoldbackConditionReturnData> GetHoldbackCondition(GetHoldbackConditionGetData data)
    {
        return await DB.GetHoldbackCondition(data);
    }

    public async Task<GetL01AgencyReturnData> GetL01Agency(GetL01AgencyGetData data)
    {
        return await DB.GetL01Agency(data);
    }

    public async Task<List<GetPaymentsReturnData>> GetPayments(GetPaymentsGetData data)
    {
        return await DB.GetPayments(data);
    }

    public async Task<GetSummonsReturnData> GetSummons(GetSummonsGetData data)
    {
        return await DB.GetSummons(data);
    }
}
