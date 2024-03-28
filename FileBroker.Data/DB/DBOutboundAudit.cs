using DBHelper;
using FileBroker.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileBroker.Data.DB;

public class DBOutboundAudit : IOutboundAuditRepository
{
    private IDBToolsAsync MainDB { get; }

    public DBOutboundAudit(IDBToolsAsync mainDB)
    {
        MainDB = mainDB;
    }

    public async Task InsertIntoOutboundAudit(string fileName, DateTime fileDate, bool fileCreated, string message)
    {
        var parameters = new Dictionary<string, object>
        {
            {"fileName", fileName },
            {"fileDate", fileDate },
            {"fileCreated", fileCreated },
            {"message", message }
        };

        await MainDB.ExecProcAsync("MessageBrokerInsertOutboundAudit", parameters);
    }

}
