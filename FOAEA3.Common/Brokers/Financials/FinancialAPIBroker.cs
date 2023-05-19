﻿using FOAEA3.Model;
using FOAEA3.Model.Interfaces;
using FOAEA3.Model.Interfaces.Broker;

namespace FOAEA3.Common.Brokers.Financials
{
    public class FinancialAPIBroker : IFinancialAPIBroker
    {
        public IAPIBrokerHelper ApiHelper { get; }
        public string Token { get; set; }

        public FinancialAPIBroker(IAPIBrokerHelper apiHelper, string token)
        {
            ApiHelper = apiHelper;
            Token = token;
        }

        public async Task<List<CR_PADReventData>> GetActiveCR_PADReventsAsync(string enfSrv)
        {
            string apiCall = $"api/v1/PADRevents/active?enfSrv={enfSrv}";
            return await ApiHelper.GetDataAsync<List<CR_PADReventData>>(apiCall, token: Token);
        }

        public async Task CloseCR_PADReventsAsync(string batchId, string enfSrv)
        {
            string apiCall = $"api/v1/PADRevents/close?batchId={batchId}&enfSrv={enfSrv}";
            await ApiHelper.SendCommandAsync(apiCall, token: Token);
        }

        public async Task<List<BlockFundData>> GetBlockFundsAsync(string enfSrv)
        {
            string apiCall = $"api/v1/BlockFunds?enfSrv={enfSrv}";
            return await ApiHelper.GetDataAsync<List<BlockFundData>>(apiCall, token: Token);
        }

        public async Task<List<DivertFundData>> GetDivertFundsAsync(string enfSrv, string batchId)
        {
            string apiCall = $"api/v1/DivertFunds?enfSrv={enfSrv}&batchId={batchId}";
            return await ApiHelper.GetDataAsync<List<DivertFundData>>(apiCall, token: Token);
        }

        public async Task<List<IFMSdata>> GetIFMSasync(string batchId)
        {
            string apiCall = $"api/v1/IFMS?batchId={batchId}";
            return await ApiHelper.GetDataAsync<List<IFMSdata>>(apiCall, token: Token);
        }       
    }
}
