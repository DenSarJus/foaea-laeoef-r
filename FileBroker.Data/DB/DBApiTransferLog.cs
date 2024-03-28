using DBHelper;
using FileBroker.Model;
using FileBroker.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileBroker.Data.DB;

public class DBApiTransferLog : IApiTransferLogRepository
{
    private IDBToolsAsync MainDB { get; }

    public DBApiTransferLog(IDBToolsAsync mainDB)
    {
        MainDB = mainDB;
    }

    public async Task<ApiTransferLogData> GetApiTransferLog(int apiTransferLogId)
    {
        var parameters = new Dictionary<string, object> {
            { "ApiTransferLogId", apiTransferLogId }
        };

        var data = (await MainDB.GetDataFromStoredProcAsync<ApiTransferLogData>("ApiTransferLog_Select", parameters, FillApiTransferLogDataFromReader))
                     .FirstOrDefault();

        return data;
    }

    public async Task<ApiTransferLogData> GetNextActiveApiTransferLog()
    {
        var data = (await MainDB.GetDataFromStoredProcAsync<ApiTransferLogData>("ApiTransferLog_SelectNextActive", FillApiTransferLogDataFromReader))
                     .FirstOrDefault();

        return data;
    }

    public async Task<List<ApiTransferLogDetailData>> GetApiTransferLogDetailsForApiTransferLog(int apiTransferLogId)
    {
        var parameters = new Dictionary<string, object> {
            { "ApiTransferLogId", apiTransferLogId }
        };

        var data = await MainDB.GetDataFromStoredProcAsync<ApiTransferLogDetailData>("ApiTransferLogDetail_Select", parameters, FillApiTransferLogDetailDataFromReader);

        return data;
    }

    public async Task<int> CreateApiTransferLog(ApiTransferLogData log, List<ApiTransferLogDetailData> details)
    {
        int newId = await MainDB.CreateDataAsync<ApiTransferLogData, int>("ApiTransferLog", log, "ApiTransferLogId", 
                                                                          SetApiTransferLogParameters, 0);

        foreach (var detail in details)
        {
            var detailParameters = new Dictionary<string, object>
            {
                {"ApiTransferLogId", detail.ApiTransferLogId},
                {"Appl_EnfSrvCd", detail.Appl_EnfSrvCd},
                {"Appl_CtrlCd", detail.Appl_CtrlCd},
                {"OutgoingJsonData", detail.OutgoingJsonData},
                {"ApiRequestSent", detail.ApiRequestSent},
                {"IsWaiting", detail.IsWaiting},
                {"ApiResponseReceived", detail.ApiResponseReceived},
                {"IncomingJsonData", detail.IncomingJsonData},
                {"IsStoredLocally", detail.IsStoredLocally}
            };

            await MainDB.ExecProcAsync("ApiTransferLogDetail_Insert", detailParameters);
        }

        return newId;        
    }

    private void SetApiTransferLogParameters(ApiTransferLogData data, Dictionary<string, object> parameters)
    {
        parameters.Add("FileCycle", data.FileCycle);
        parameters.Add("FileCreated", data.FileCreated);
        parameters.Add("FileName", data.FileName);
        parameters.Add("ApiUrl", data.ApiUrl);
    }

    public async Task UpdateApiTransferDetailLog(ApiTransferLogDetailData detail)
    {
        var detailParameters = new Dictionary<string, object>
            {
                {"ApiTransferLogId", detail.ApiTransferLogId},
                {"Appl_EnfSrvCd", detail.Appl_EnfSrvCd},
                {"Appl_CtrlCd", detail.Appl_CtrlCd},
                {"OutgoingJsonData", detail.OutgoingJsonData},
                {"ApiRequestSent", detail.ApiRequestSent},
                {"IsWaiting", detail.IsWaiting},
                {"ApiResponseReceived", detail.ApiResponseReceived},
                {"IncomingJsonData", detail.IncomingJsonData},
                {"IsStoredLocally", detail.IsStoredLocally}
            };

        await MainDB.ExecProcAsync("ApiTransferLogDetail_Update", detailParameters);
    }

    public static void FillApiTransferLogDataFromReader(IDBHelperReader rdr, ApiTransferLogData data)
    {
        data.ApiTransferLogId = (int)rdr["ApiTransferLogId"];
        data.FileCycle = (int)rdr["FileCycle"];
        data.FileCreated = (DateTime)rdr["FileCreated"];
        data.FileName = rdr["FileName"] as string;
        data.ApiUrl = rdr["ApiUrl"] as string;
    }

    public static void FillApiTransferLogDetailDataFromReader(IDBHelperReader rdr, ApiTransferLogDetailData data)
    {
        data.ApiTransferLogId = (int)rdr["ApiTransferLogId"];
        data.Appl_EnfSrvCd = rdr["Appl_EnfSrvCd"] as string;
        data.Appl_CtrlCd = rdr["Appl_CtrlCd"] as string;
        data.OutgoingJsonData = rdr["OutgoingJsonData"] as string;
        data.ApiRequestSent = rdr["ApiRequestSent"] as DateTime?; // can be null 
        data.IsWaiting = (bool)rdr["IsWaiting"];
        data.ApiResponseReceived = rdr["ApiResponseReceived"] as DateTime?; // can be null 
        data.IncomingJsonData = rdr["IncomingJsonData"] as string; // can be null 
        data.IsStoredLocally = (bool)rdr["IsStoredLocally"];
    }
}
