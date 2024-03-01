using FOAEA3.Common.Brokers;
using FOAEA3.Model;
using FOAEA3.Model.Interfaces.Repository;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TestData.TestDB
{
    class InMemorySubmitter : ISubmitterRepository
    {

        public InMemorySubmitter()
        {

        }

        public string CurrentSubmitter { get; set; }
        public string UserId { get; set; }

        public Task CreateSubmitter(SubmitterData newSubmitter)
        {
            newSubmitter.Subm_Create_Usr = "Test";
            newSubmitter.Subm_CourtUsr_Ind = true;

            return Task.CompletedTask;
        }

        public Task CreateSubmitterMessage(SubmitterMessageData submitterMessage)
        {
            throw new NotImplementedException();
        }

        public Task<List<CommissionerData>> GetCommissioners(string locationCode, string currentSubmitter)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetFOAEAOfficersEmail()
        {
            throw new NotImplementedException();
        }

        public Task<string> GetMaxSubmitterCode(string submCodePart)
        {
            return Task.FromResult("02");
        }

        public Task<string> GetSignAuthorityForSubmitter(string submCd)
        {
            throw new NotImplementedException();
        }

        public Task<List<SubmitterData>> GetSubmitter(string submCode = null, string submName = null,
                                                string enfOffCode = null, string enfServCode = null,
                                                string submFName = null, string submMName = null,
                                                string prov = null)
        {
            string submClass = "EO";

            if ((submCode == LoginsAPIBroker.SYSTEM_SUBMITTER) || (submCode.StartsWith("FO", StringComparison.CurrentCultureIgnoreCase)))
                submClass = "FC";
            else if (submCode[2] == '1')
                submClass = "ES";

            var submitter = new SubmitterData
            {
                Subm_SubmCd = submCode,
                ActvSt_Cd = "A",
                Subm_Class = submClass,
                Subm_Trcn_AccsPrvCd = true,
                Subm_Intrc_AccsPrvCd = true,
                Subm_Lic_AccsPrvCd = true,
                Subm_Fin_Ind = true,
                Subm_LglSgnAuth_Ind = true,
                Subm_Audit_File_Ind = true
            };

            var result = new List<SubmitterData>
            {
                submitter
            };

            return Task.FromResult(result);
        }

        public Task<List<string>> GetSubmitterCodesForOffice(string service, string office)
        {
            throw new NotImplementedException();
        }

        public Task<List<SubmitterData>> GetSubmitterIncludingClosed(string submCode = null, string submName = null, string enfOffCode = null, string enfServCode = null, string submFName = null, string submMName = null, string prov = null)
        {
            throw new NotImplementedException();
        }

        public Task<List<SubmitterMessageData>> GetSubmitterMessageForSubmitter(string submitterID, int languageCode)
        {
            throw new NotImplementedException();
        }

        public Task SubmitterMessageDelete(string submitterID)
        {
            throw new NotImplementedException();
        }

        public Task UpdateSubmitter(SubmitterData newSubmitter)
        {
            throw new NotImplementedException();
        }
        public Task<DateTime> UpdateSubmitterLastLogin(string submCd)
        {
            throw new NotImplementedException();
        }
    }
}
