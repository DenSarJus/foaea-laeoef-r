using DBHelper;
using System.Threading.Tasks;

namespace FileBroker.Model.Interfaces;

public interface ISettingsRepository
{
    IDBToolsAsync MainDB { get; }
    Task<SettingsData> GetSettingsDataForFileName(string fileNameNoExt);
}
