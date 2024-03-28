using DBHelper;
using FileBroker.Model;
using FileBroker.Model.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileBroker.Data.DB;

public class DBFundsAvailable : IFundsAvailableIncomingRepository
{
    private IDBToolsAsync MainDB { get; }

    public DBFundsAvailable(IDBToolsAsync mainDB)
    {
        MainDB = mainDB;
    }

    public async Task<List<FundsAvailableIncomingTrainingData>> GetFundsAvailableIncomingTrainingData(string batchId)
    {
        var parameters = new Dictionary<string, object> {
            {"BatchID", batchId },
            {"tableType", "TR" }
        };

        var data = await MainDB.GetDataFromStoredProcAsync<FundsAvailableIncomingTrainingData>("MessageBrokerGetIncomingFATableData", parameters, FillFundsAvailableIncomingTrainingData);
        return data;
    }

    private void FillFundsAvailableIncomingTrainingData(IDBHelperReader rdr, FundsAvailableIncomingTrainingData data)
    {
        data.BatchId = rdr["BatchId"] as string; // can be null 
        data.Ordinal = rdr["ordinal"] as int?; // can be null 
        data.Payment_Id = rdr["Payment_Id"] as string; // can be null 
        data.XRefSin = rdr["XRefSin"] as string; // can be null 
        data.Process_State = rdr["Process_State"] as string; // can be null
    }

    public async Task UpdateFundsAvailableIncomingTraining(List<FundsAvailableIncomingTrainingData> data)
    {
        await MainDB.BulkUpdateAsync(data, "FA_Incoming_TR");
    }

}
