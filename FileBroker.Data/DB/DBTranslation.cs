using DBHelper;
using FileBroker.Model;
using FileBroker.Model.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileBroker.Data.DB;

public class DBTranslation : ITranslationRepository
{
    private IDBToolsAsync MainDB { get; }

    public DBTranslation(IDBToolsAsync mainDB)
    {
        MainDB = mainDB;
    }

    public async Task<List<TranslationData>> GetTranslations()
    {
        return await MainDB.GetDataFromStoredProcAsync<TranslationData>("", FillTranslationDataFromReader);
    }

    private void FillTranslationDataFromReader(IDBHelperReader rdr, TranslationData data)
    {
        data.TranslationId = (int)rdr["TranslationId"];
        data.EnglishText = rdr["EnglishText"] as string;
        data.FrenchText = rdr["FrenchText"] as string;
    }
}
