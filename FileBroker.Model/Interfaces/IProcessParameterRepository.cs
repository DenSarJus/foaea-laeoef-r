using System.Threading.Tasks;

namespace FileBroker.Model.Interfaces;

public interface IProcessParameterRepository
{
    Task<string> GetValueForParameter(int processId, string parameter);
    Task<ProcessCodeData> GetProcessCodes(int processId);
}
