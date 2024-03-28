using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileBroker.Model.Interfaces;

public interface IApiTransferLogRepository
{
    Task<ApiTransferLogData> GetApiTransferLog(int apiTransferLogId);
    Task<ApiTransferLogData> GetNextActiveApiTransferLog();
    Task<List<ApiTransferLogDetailData>> GetApiTransferLogDetailsForApiTransferLog(int apiTransferLogId);
    Task<int> CreateApiTransferLog(ApiTransferLogData log, List<ApiTransferLogDetailData> details);
    Task UpdateApiTransferDetailLog(ApiTransferLogDetailData detail);
}
