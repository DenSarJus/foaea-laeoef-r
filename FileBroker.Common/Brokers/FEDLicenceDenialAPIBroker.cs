﻿using FileBroker.Model.Interfaces.Broker;
using FOAEA3.Model.Interfaces;
using FOAEA3.Model.Interfaces.Broker;
using System.Threading.Tasks;

namespace FileBroker.Common.Brokers
{
    public class FEDLicenceDenialAPIBroker : IFEDLicenceDenialAPIBroker, IVersionSupport
    {
        private IAPIBrokerHelper ApiHelper { get; }

        public FEDLicenceDenialAPIBroker(IAPIBrokerHelper apiHelper)
        {
            ApiHelper = apiHelper;
        }

        public async Task<string> GetVersionAsync()
        {
            string apiCall = $"api/v1/FederalLicenceDenialFiles/Version";
            return await ApiHelper.GetStringAsync(apiCall, maxAttempts: 1);
        }

        public async Task<string> GetConnectionAsync()
        {
            string apiCall = $"api/v1/FederalLicenceDenialFiles/DB";
            return await ApiHelper.GetStringAsync(apiCall, maxAttempts: 1);
        }
    }
}