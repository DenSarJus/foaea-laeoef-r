using DBHelper;
using FileBroker.Model;
using FileBroker.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileBroker.Data.DB;

public class DBFileTable : IFileTableRepository
{
    public IDBToolsAsync MainDB { get; }

    public DBFileTable(IDBToolsAsync mainDB)
    {
        MainDB = mainDB;
    }

    public async Task<FileTableData> GetFileTableDataForFileName(string fileNameNoExt)
    {
        var fileTableData = await MainDB.GetAllDataAsync<FileTableData>("FileTable", FillFileTableDataFromReader);

        return fileTableData.AsParallel().Where(f => f.Name.ToUpper() == fileNameNoExt.ToUpper()).FirstOrDefault();
    }

    public async Task<FileTableFlagData> GetAuditFileFormatForProcessId(int processId)
    {

        var parameters = new Dictionary<string, object> {
                { "PrcId",  processId }
            };

        var fileTableFlagData = await MainDB.GetDataFromStoredProcAsync<FileTableFlagData>("MessageBrokerConfigGetIncludeAudit", parameters, FillFileFlagTableDataFromReader);

        return fileTableFlagData.AsParallel().FirstOrDefault();
    }

    private void FillFileFlagTableDataFromReader(IDBHelperReader rdr, FileTableFlagData data)
    {
        if (rdr.ColumnExists("PrcId")) data.PrcId = (int)rdr["PrcId"];
        data.IncludeAudit = rdr["IncludeAudit"] as string;
    }

    public async Task<List<FileTableData>> MessageBrokerSchedulerGetDueProcess(string frequency)
    {
        var parameters = new Dictionary<string, object>
        {
            { "sFrequency", frequency }
        };

        var fileTableData = await MainDB.GetDataFromStoredProcAsync<FileTableData>("MessageBrokerSchedulerGetDueProcess", parameters, FillFileTableDataFromReader);

        return fileTableData;
    }

    public async Task DisableFileProcess(int processId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "nProcessID", processId },
            { "bActive", false }
        };

        await MainDB.ExecProcAsync("MessageBrokerEnableDisableFileProcess", parameters);
    }

    public async Task EnableFileProcess(int processId)
    {
        var parameters = new Dictionary<string, object>
        {
            { "nProcessID", processId },
            { "bActive", true }
        };

        await MainDB.ExecProcAsync("MessageBrokerEnableDisableFileProcess", parameters);
    }

    public async Task<List<FileTableData>> GetFileTableDataForCategory(string category)
    {
        var fileTableData = await MainDB.GetAllDataAsync<FileTableData>("FileTable", FillFileTableDataFromReader);

        return fileTableData.AsParallel().Where(f => f.Category == category).ToList();
    }

    public async Task<List<FileTableData>> GetAllActive()
    {
        var fileTableData = await MainDB.GetAllDataAsync<FileTableData>("FileTable", FillFileTableDataFromReader);

        return fileTableData.AsParallel().Where(f => f.Active is true).ToList();
    }

    public async Task SetNextCycleForFileType(FileTableData fileData, int length = 6)
    {

        int newCycle = fileData.Cycle + 1;
        string newCycleStr = newCycle.ToString();
        if (newCycleStr.Length > length)
            newCycle = 1;

        var parameters = new Dictionary<string, object>
        {
            {"nProcessID", fileData.PrcId},
            {"nCycle", newCycle}
        };

        _ = await MainDB.ExecProcAsync("MessageBrokerConfigSetCycle", parameters);

    }

    public async Task<bool> IsFileLoading(int processId)
    {
        var parameters = new Dictionary<string, object>
        {
            {"nProcessID", processId}
        };

        return await MainDB.GetDataFromProcSingleValueAsync<bool>("MessageBrokerConfigIsFileLoading", parameters);
    }

    public async Task SetIsFileLoadingValue(int processId, bool newValue)
    {
        var parameters = new Dictionary<string, object>
        {
            {"nProcessID", processId},
            {"isLoading", newValue}
        };

        await MainDB.ExecProcAsync("MessageBrokerConfigSetFileLoadingValue", parameters);
    }

    public static void FillFileTableDataFromReader(IDBHelperReader rdr, FileTableData data)
    {
        data.PrcId = (int)rdr["PrcId"];
        data.Type = rdr["type"] as string; // can be null 
        data.Name = rdr["name"] as string; // can be null 
        data.Cycle = (int)rdr["cycle"];
        data.Transform = (bool)rdr["Transform"];
        data.Meduim = rdr["meduim"] as string; // can be null 
        data.Address = rdr["address"] as string; // can be null 
        data.Path = rdr["path"] as string; // can be null 
        data.Frequency = rdr["frequency"] as int?; // can be null 
        data.Nextrun = rdr["nextrun"] as DateTime?; // can be null 
        data.Category = rdr["Category"] as string; // can be null 
        data.Active = rdr["active"] as bool?; // can be null 
        data.IsXML = (bool)rdr["IsXML"];
        data.IsReg = (bool)rdr["IsReg"];
        data.UsePADRSource = (bool)rdr["UsePADRSource"];
        data.StartDate = rdr["StartDate"] as DateTime?; // can be null 
        data.UseFixedTag = (short)rdr["UseFixedTag"];
        data.IsLoading = (bool)rdr["IsLoading"];
    }
}
