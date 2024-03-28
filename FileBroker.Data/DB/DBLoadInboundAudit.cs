using DBHelper;
using FileBroker.Model.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileBroker.Data.DB;

public class DBLoadInboundAudit : ILoadInboundAuditRepository
{
    private IDBToolsAsync MainDB { get; }

    public DBLoadInboundAudit(IDBToolsAsync mainDB)
    {
        MainDB = mainDB;
    }

    public async Task AddRow(string applEnfSrvCd, string applCtrlCd, string sourceRefNumber, string inboundFileName)
    {
        var parameters = new Dictionary<string, object>
        {
            {"Appl_EnfSrv_Cd", applEnfSrvCd },
            {"Appl_CtrlCd", applCtrlCd },
            {"Appl_Source_RfrNr", sourceRefNumber },
            {"InboundFilename", inboundFileName }
        };

        await MainDB.ExecProcAsync("LoadInboundAuditInsert", parameters);
    }

    public async Task<bool> RowExists(string applEnfSrvCd, string applCtrlCd, string sourceRefNumber, string inboundFileName)
    {
        var parameters = new Dictionary<string, object>
        {
            {"Appl_EnfSrv_Cd", applEnfSrvCd },
            {"Appl_CtrlCd", applCtrlCd },
            {"Appl_Source_RfrNr", sourceRefNumber },
            {"InboundFilename", inboundFileName }
        };

        int count = await MainDB.GetDataFromStoredProcViaReturnParameterAsync<int>("LoadInboundAuditSearch", parameters, "found");

        return (count > 0);
    }

    public async Task MarkRowAsCompleted(string applEnfSrvCd, string applCtrlCd, string sourceRefNumber, string inboundFileName)
    {
        var parameters = new Dictionary<string, object>
        {
            {"Appl_EnfSrv_Cd", applEnfSrvCd },
            {"Appl_CtrlCd", applCtrlCd },
            {"Appl_Source_RfrNr", sourceRefNumber },
            {"InboundFilename", inboundFileName }
        };

        await MainDB.ExecProcAsync("LoadInboundAuditUpdate", parameters);
    }
}
