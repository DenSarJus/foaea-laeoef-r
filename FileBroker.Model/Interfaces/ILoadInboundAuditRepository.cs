using System.Threading.Tasks;

namespace FileBroker.Model.Interfaces;

public interface ILoadInboundAuditRepository
{
    Task MarkRowAsCompleted(string applEnfSrvCd, string applCtrlCd, string sourceRefNumber, string inboundFileName);
    Task<bool> RowExists(string applEnfSrvCd, string applCtrlCd, string sourceRefNumber, string inboundFileName);
    Task AddRow(string applEnfSrvCd, string applCtrlCd, string sourceRefNumber, string inboundFileName);
}
