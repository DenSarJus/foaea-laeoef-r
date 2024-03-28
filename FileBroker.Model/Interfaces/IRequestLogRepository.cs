using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileBroker.Model.Interfaces;

public interface IRequestLogRepository
{
    Task<int> Add(RequestLogData requestLogData);
    Task<List<RequestLogData>> GetAll();
    Task DeleteAll();
}
