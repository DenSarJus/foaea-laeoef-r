using DBHelper;
using FileBroker.Model;
using FileBroker.Model.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileBroker.Data.DB;

public class DBProcessParameter : IProcessParameterRepository
{
    private IDBToolsAsync MainDB { get; }

    public DBProcessParameter(IDBToolsAsync mainDB)
    {
        MainDB = mainDB;
    }

    public async Task<string> GetValueForParameter(int processId, string parameter)
    {
        var parameters = new Dictionary<string, object>
        {
            {"rsntProcess_Cd", processId},
            {"rvchProps_Cd", parameter}
        };

        string outputParamFieldName = "dvchOutput";

        var outputParameter = new Dictionary<string, string>
        {
            {outputParamFieldName, "S100"}
        };

        var values = await MainDB.GetDataFromStoredProcViaReturnParametersAsync("MessageBrokerConfigGetProcessParamterValue", parameters, outputParameter);

        return values[outputParamFieldName] as string;
    }

    public async Task<ProcessCodeData> GetProcessCodes(int processId)
    {

        var parameters = new Dictionary<string, object>
        {
            { "rsntProcess_Cd", processId }
        };

        var outputParameters = new Dictionary<string, string>
        {
            { "EnfSrv_Cd",       "S4" },
            { "ProcessCategory", "S12" },
            { "ActvSt_Cd",       "S4" },
            { "SubmRecptCd",     "S8" },
            { "AppLiSt_Cd",      "I" },
            { "EnfSrv_Loc_Cd",   "S4" }
        };

        var values = await MainDB.GetDataFromStoredProcViaReturnParametersAsync("MessageBrokerConfigGetProcessCodes",
                                                                     parameters, outputParameters);

        var processCodes = new ProcessCodeData
        {
            EnfSrv_Cd = values["EnfSrv_Cd"] as string,
            ProcessCategory = values["ProcessCategory"] as string,
            ActvSt_Cd = values["ActvSt_Cd"] as string,
            SubmRecptCd = values["SubmRecptCd"] as string,
            AppLiSt_Cd = (int)values["AppLiSt_Cd"],
            EnfSrv_Loc_Cd = values["EnfSrv_Loc_Cd"] as string,
        };

        return processCodes;
    }

}
