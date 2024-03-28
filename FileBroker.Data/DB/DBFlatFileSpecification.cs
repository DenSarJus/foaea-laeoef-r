using DBHelper;
using FileBroker.Model;
using FileBroker.Model.Interfaces;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileBroker.Data.DB;

public class DBFlatFileSpecification : IFlatFileSpecificationRepository
{

    private IDBToolsAsync MainDB { get; }

    public DBFlatFileSpecification(IDBToolsAsync mainDB)
    {
        MainDB = mainDB;
    }

    public async Task<List<FlatFileSpecificationData>> GetFlatFileSpecificationsForFile(int processId)
    {
        var parameters = new Dictionary<string, object>
        {
            {"PrcsProcess_Cd", processId}
        };

        return await MainDB.GetDataFromStoredProcAsync<FlatFileSpecificationData>("GetPrcsImpExp", parameters, FillDataFromReader);
    }

    private void FillDataFromReader(IDBHelperReader rdr, FlatFileSpecificationData data)
    {
        data.PrcsProcess_Cd = (short)rdr["PrcsProcess_Cd"];
        data.Val_Direction = (byte)rdr["Val_Direction"];
        data.Val_FileSect = rdr["Val_FileSect"] as string; // can be null 
        data.Val_SortOrder = (int)rdr["Val_SortOrder"];
        data.Field_Name = rdr["Field_Name"] as string; // can be null 
        data.Val_Pos_Start = (int)rdr["Val_Pos_Start"];
        data.Val_Pos_End = (int)rdr["Val_Pos_End"];
        data.Val_Include = (byte)rdr["Val_Include"];
        data.Val_Required = (byte)rdr["Val_Required"];
        data.Val_Reas_Cd = rdr["Val_Reas_Cd"] as int?; // can be null 
        data.FormatDate = rdr["FormatDate"] as string; // can be null 
        data.PrcsType_Cd = rdr["PrcsType_Cd"] as string; // can be null 
        data.Val_DfltApply = (byte)rdr["Val_DfltApply"];
        data.Val_DfltVal = rdr["Val_DfltVal"] as string; // can be null 
        data.ActvSt_Cd = rdr["ActvSt_Cd"] as string; // can be null 
    }
}
