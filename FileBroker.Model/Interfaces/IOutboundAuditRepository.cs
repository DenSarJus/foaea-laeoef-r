using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileBroker.Model.Interfaces;

public interface IOutboundAuditRepository
{
    Task InsertIntoOutboundAudit(string fileName, DateTime fileDate, bool fileCreated, string message);
}
