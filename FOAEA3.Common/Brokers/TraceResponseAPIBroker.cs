using FOAEA3.Common.Helpers;
using FOAEA3.Model;
using FOAEA3.Model.Base;
using FOAEA3.Model.Interfaces;
using FOAEA3.Model.Interfaces.Broker;

namespace FOAEA3.Common.Brokers
{
    public class TraceResponseAPIBroker : ITraceResponseAPIBroker
    {
        public IAPIBrokerHelper ApiHelper { get; }
        public string Token { get; set; }

        public TraceResponseAPIBroker(IAPIBrokerHelper apiHelper, string token)
        {
            ApiHelper = apiHelper;
            Token = token;
        }

        public async Task InsertBulkData(List<TraceResponseData> responseData)
        {
            _ = await ApiHelper.PostData<TraceResponseData, List<TraceResponseData>>("api/v1/traceResponses/bulk",
                                                                                    responseData, token: Token);
        }

        public async Task AddTraceFinancialResponseData(TraceFinancialResponseData traceFinancialResultData)
        {
            string apiCall = "api/v1/traceFinancialResponses";
            _ = await ApiHelper.PostData<TraceFinancialResponseData, TraceFinancialResponseData>(apiCall,
                                                                                         traceFinancialResultData, token: Token);
        }

        public async Task MarkTraceResultsAsViewed(string recipientSubmCd)
        {
            await ApiHelper.SendCommand("api/v1/traceResponses/MarkResultsAsViewed?recipientSubmCd=" + recipientSubmCd, token: Token);
        }

        public async Task<List<TraceResponseData>> GetTraceResponseForApplication(string appl_EnfSrvCd, string appl_CtrlCd)
        {
            string key = ApplKey.MakeKey(appl_EnfSrvCd, appl_CtrlCd);
            string apiCall = $"api/v1/traceResponses/{key}";
            var data = await ApiHelper.GetData<DataList<TraceResponseData>>(apiCall, token: Token);
            return data.Items;
        }
    }
}
