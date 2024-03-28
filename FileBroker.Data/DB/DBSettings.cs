using DBHelper;
using FileBroker.Model;
using FileBroker.Model.Interfaces;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileBroker.Data.DB;

public class DBSettings : ISettingsRepository
{
    public IDBToolsAsync MainDB { get; }

    public DBSettings(IDBToolsAsync mainDB)
    {
        MainDB = mainDB;
    }

    public async Task<SettingsData> GetSettingsDataForFileName(string fileNameNoExt)
    {
        var parameters = new Dictionary<string, object> {
                { "thisName",  fileNameNoExt}
            };
        var settingsData = await MainDB.GetDataFromStoredProcAsync<SettingsData>("Settings_SelectForFileName", parameters, FillSettingsDataFromReader);

        return settingsData.AsParallel().Where(f => f.Name.ToUpper() == fileNameNoExt.ToUpper()).FirstOrDefault();
    }

    private void FillSettingsDataFromReader(IDBHelperReader rdr, SettingsData data)
    {
        data.Name = rdr["name"] as string; // can be null 
        data.Paths = rdr["paths"] as string; // can be null  
    }
}
