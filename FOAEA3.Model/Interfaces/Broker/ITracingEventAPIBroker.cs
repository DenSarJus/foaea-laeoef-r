using System.Collections.Generic;
using System.Threading.Tasks;

namespace FOAEA3.Model.Interfaces.Broker
{
    public interface ITracingEventAPIBroker
    {
        IAPIBrokerHelper ApiHelper { get; }
        string Token { get; set; }

        Task CloseNETPTraceEvents();
        Task<ApplicationEventsList> GetRequestedTRCINEvents(string enfSrvCd, string fileCycle);
        Task<ApplicationEventDetailsList> GetActiveTracingEventDetails(string enfSrvCd, string fileCycle);
        Task<ApplicationEventDetailsList> GetTraceEventDetails(string appl_EnfSrvCd, string appl_CtrlCd, bool activeOnly = true);
    }
}
