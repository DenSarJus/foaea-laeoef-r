using DBHelper;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileBroker.Model.Interfaces;

public interface IFileTableRepository
{
    IDBToolsAsync MainDB { get; }

    Task<FileTableData> GetFileTableDataForFileName(string fileNameNoExt);
    Task<List<FileTableData>> MessageBrokerSchedulerGetDueProcess(string frequency);
    Task<List<FileTableData>> GetFileTableDataForCategory(string category);
    Task<List<FileTableData>> GetAllActive();
    Task<FileTableFlagData> GetAuditFileFormatForProcessId(int processId);

    Task SetNextCycleForFileType(FileTableData fileData, int length = 6);
    Task<bool> IsFileLoading(int processId);
    Task SetIsFileLoadingValue(int processId, bool newValue);

    Task DisableFileProcess(int processId);
    Task EnableFileProcess(int processId);
}
