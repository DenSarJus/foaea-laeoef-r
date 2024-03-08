using System.Collections.Generic;
using System.Threading.Tasks;

namespace FOAEA3.Model.Interfaces.Broker
{
    public interface ITraceResponseAPIBroker
    {
        IAPIBrokerHelper ApiHelper { get; }
        string Token { get; set; }

        Task InsertBulkData(List<TraceResponseData> responseData);
        Task AddTraceFinancialResponseData(TraceFinancialResponseData traceFinancialResultData);
        Task MarkTraceResultsAsViewed(string recipientSubmCd);
        Task<List<TraceResponseData>> GetTraceResponseForApplication(string appl_EnfSrvCd, string appl_CtrlCd);
    }
}
