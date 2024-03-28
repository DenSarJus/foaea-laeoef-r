using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileBroker.Model.Interfaces;

public interface IFileAuditRepository
{
    Task<List<FileAuditData>> GetFileAuditDataForFile(string fileName);
    Task InsertFileAuditData(FileAuditData data);
    Task MarkFileAuditCompletedForFile(string fileName);
    Task MarkFileAuditCompletedForItem(FileAuditData data);
}
