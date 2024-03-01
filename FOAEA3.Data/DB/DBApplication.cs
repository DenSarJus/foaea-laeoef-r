using DBHelper;
using FOAEA3.Data.Base;
using FOAEA3.Model;
using FOAEA3.Model.Base;
using FOAEA3.Model.Enums;
using FOAEA3.Model.Interfaces.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FOAEA3.Data.DB
{
    internal class DBApplication(IDBToolsAsync mainDB) : DBbase(mainDB), IApplicationRepository
    {
        public async Task<ApplicationData> GetApplication(string appl_EnfSrv_Cd, string appl_CtrlCd)
        {
            var parameters = new Dictionary<string, object>
                {
                    {"Appl_EnfSrv_Cd", appl_EnfSrv_Cd},
                    {"Appl_CtrlCd", appl_CtrlCd }
                };

            List<ApplicationData> data = await MainDB.GetDataFromStoredProcAsync<ApplicationData>("ApplGetAppl2", parameters, FillApplicationDataFromReader);

            return data.FirstOrDefault(); // returns null if no data found

        }

        public async Task<List<ApplicationData>> GetApplicationsForAutomation(string appl_EnfSrv_Cd, string medium_Cd,
                                                                  ApplicationState appLiSt_Cd, string appCtgy_Cd,
                                                                  string actvSt_Cd)
        {
            var parameters = new Dictionary<string, object>
                {
                    {"Appl_EnfSrv_Cd", appl_EnfSrv_Cd},
                    {"Medium_Cd", DBNull.Value},
                    {"AppLiSt_Cd", (int) appLiSt_Cd},
                    {"AppCtgy_Cd", appCtgy_Cd},
                    {"ActvSt_Cd", actvSt_Cd}
                };

            var data = await MainDB.GetDataFromStoredProcAsync<ApplicationData>("GetApplProvinceAutomation", parameters, FillApplicationDataFromReader);

            return data;

        }

        public async Task<List<ApplicationData>> GetApplicationsForCategoryAndLifeState(string category, ApplicationState lifeState)
        {

            var parameters = new Dictionary<string, object> {
                    { "AppCtgy_Cd",  category},
                    { "AppLiSt_Cd",  (short) lifeState}
                };

            var data = await MainDB.GetDataFromStoredProcAsync<ApplicationData>("Appl_SelectForCategoryAndLifestate", parameters, FillApplicationDataFromReader);

            return data;
        }

        public async Task<bool> CreateApplication(ApplicationData application)
        {
            var parameters = SetParameters(application);

            await MainDB.ExecProcAsync("ApplInsert", parameters);

            if (!string.IsNullOrEmpty(MainDB.LastError))
            {
                application.Messages.AddSystemError(MainDB.LastError);
                return false;
            }
            else
                return true;
        }

        public async Task UpdateApplication(ApplicationData application)
        {
            var parameters = SetParameters(application);

            await MainDB.ExecProcAsync("ApplUpdate", parameters);
        }

        private static Dictionary<string, object> SetParameters(ApplicationData application)
        {
            var parameters = new Dictionary<string, object>
            {
                { "Appl_EnfSrv_Cd", application.Appl_EnfSrv_Cd },
                { "Appl_CtrlCd", application.Appl_CtrlCd },
                { "Subm_SubmCd", application.Subm_SubmCd },
                { "Subm_Recpt_SubmCd", application.Subm_Recpt_SubmCd },
                { "Appl_CommSubm_Text", application.Appl_CommSubm_Text?.TrimEnd() },
                { "Appl_Lgl_Dte", application.Appl_Lgl_Dte },
                { "Appl_Rcptfrm_Dte", application.Appl_Rcptfrm_Dte },
                { "Appl_Group_Batch_Cd", application.Appl_Group_Batch_Cd },
                { "Appl_Source_RfrNr", application.Appl_Source_RfrNr?.TrimEnd() },
                { "Subm_Affdvt_SubmCd", application.Subm_Affdvt_SubmCd },
                { "Appl_Affdvt_DocTypCd", application.Appl_Affdvt_DocTypCd },
                { "Appl_JusticeNr", application.Appl_JusticeNr },
                { "Appl_Crdtr_FrstNme", application.Appl_Crdtr_FrstNme?.TrimEnd() },
                { "Appl_Crdtr_MddleNme", application.Appl_Crdtr_MddleNme?.TrimEnd() },
                { "Appl_Crdtr_SurNme", application.Appl_Crdtr_SurNme?.TrimEnd() },
                { "Appl_Dbtr_FrstNme", application.Appl_Dbtr_FrstNme?.TrimEnd() },
                { "Appl_Dbtr_MddleNme", application.Appl_Dbtr_MddleNme?.TrimEnd() },
                { "Appl_Dbtr_SurNme", application.Appl_Dbtr_SurNme?.TrimEnd() },
                { "Appl_Dbtr_Parent_SurNme", application.Appl_Dbtr_Parent_SurNme_Birth },
                { "Appl_Dbtr_LngCd", application.Appl_Dbtr_LngCd },
                { "Appl_Dbtr_Gendr_Cd", application.Appl_Dbtr_Gendr_Cd },
                { "Appl_Dbtr_Entrd_SIN", application.Appl_Dbtr_Entrd_SIN },
                { "Appl_Dbtr_Cnfrmd_SIN", application.Appl_Dbtr_Cnfrmd_SIN },
                { "Appl_Dbtr_RtrndBySrc_SIN", application.Appl_Dbtr_RtrndBySrc_SIN },
                { "Appl_Dbtr_Addr_Ln", application.Appl_Dbtr_Addr_Ln?.TrimEnd() },
                { "Appl_Dbtr_Addr_Ln1", application.Appl_Dbtr_Addr_Ln1?.TrimEnd() },
                { "Appl_Dbtr_Addr_CityNme", application.Appl_Dbtr_Addr_CityNme?.TrimEnd() },
                { "Appl_Dbtr_Addr_PrvCd", application.Appl_Dbtr_Addr_PrvCd },
                { "Appl_Dbtr_Addr_CtryCd", application.Appl_Dbtr_Addr_CtryCd },
                { "Appl_Dbtr_Addr_PCd", application.Appl_Dbtr_Addr_PCd },
                { "Medium_Cd", application.Medium_Cd },
                { "Appl_SIN_Cnfrmd_Ind", application.Appl_SIN_Cnfrmd_Ind },
                { "AppCtgy_Cd", application.AppCtgy_Cd },
                { "AppReas_Cd", application.AppReas_Cd },
                { "Appl_Create_Dte", application.Appl_Create_Dte },
                { "Appl_Create_Usr", application.Appl_Create_Usr },
                { "Appl_LastUpdate_Usr", application.Appl_LastUpdate_Usr },
                { "ActvSt_Cd", application.ActvSt_Cd },
                { "AppLiSt_Cd", (int) application.AppLiSt_Cd }
            };

            // set optional dates: empty dates are set to DBNull
            if (application.Appl_Dbtr_Brth_Dte is null) parameters.Add("Appl_Dbtr_Brth_Dte", DBNull.Value); else parameters.Add("Appl_Dbtr_Brth_Dte", application.Appl_Dbtr_Brth_Dte.Value);
            if ((application.Appl_Crdtr_Brth_Dte is null) || (application.Appl_Crdtr_Brth_Dte.Value == DateTime.MinValue)) parameters.Add("Appl_Crdtr_Brth_Dte", DBNull.Value); else parameters.Add("Appl_Crdtr_Brth_Dte", application.Appl_Crdtr_Brth_Dte.Value);
            if (application.Appl_RecvAffdvt_Dte is null) parameters.Add("Appl_RecvAffdvt_Dte", DBNull.Value); else parameters.Add("Appl_RecvAffdvt_Dte", application.Appl_RecvAffdvt_Dte.Value);
            if (application.Appl_Reactv_Dte is null) parameters.Add("Appl_Reactv_Dte", DBNull.Value); else parameters.Add("Appl_Reactv_Dte", application.Appl_Reactv_Dte.Value);

            // replace empty strings with DBNull
            if (String.IsNullOrWhiteSpace(application.Appl_CommSubm_Text)) parameters["Appl_CommSubm_Text"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Group_Batch_Cd)) parameters["Appl_Group_Batch_Cd"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Source_RfrNr)) parameters["Appl_Source_RfrNr"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Subm_Affdvt_SubmCd)) parameters["Subm_Affdvt_SubmCd"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Affdvt_DocTypCd)) parameters["Appl_Affdvt_DocTypCd"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_JusticeNr)) parameters["Appl_JusticeNr"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Crdtr_FrstNme)) parameters["Appl_Crdtr_FrstNme"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Crdtr_MddleNme)) parameters["Appl_Crdtr_MddleNme"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Crdtr_SurNme)) parameters["Appl_Crdtr_SurNme"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Dbtr_MddleNme)) parameters["Appl_Dbtr_MddleNme"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Dbtr_Parent_SurNme_Birth)) parameters["Appl_Dbtr_Parent_SurNme"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Dbtr_LngCd)) parameters["Appl_Dbtr_LngCd"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Dbtr_Entrd_SIN)) parameters["Appl_Dbtr_Entrd_SIN"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Dbtr_Cnfrmd_SIN)) parameters["Appl_Dbtr_Cnfrmd_SIN"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Dbtr_RtrndBySrc_SIN)) parameters["Appl_Dbtr_RtrndBySrc_SIN"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Dbtr_Addr_Ln)) parameters["Appl_Dbtr_Addr_Ln"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Dbtr_Addr_Ln1)) parameters["Appl_Dbtr_Addr_Ln1"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Dbtr_Addr_CityNme)) parameters["Appl_Dbtr_Addr_CityNme"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Dbtr_Addr_PrvCd)) parameters["Appl_Dbtr_Addr_PrvCd"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Dbtr_Addr_CtryCd)) parameters["Appl_Dbtr_Addr_CtryCd"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Appl_Dbtr_Addr_PCd)) parameters["Appl_Dbtr_Addr_PCd"] = DBNull.Value;
            if (String.IsNullOrWhiteSpace(application.Medium_Cd)) parameters["Medium_Cd"] = DBNull.Value;

            if (String.IsNullOrWhiteSpace(application.Appl_Create_Usr)) parameters["Appl_Create_Usr"] = application.Subm_SubmCd;
            if (String.IsNullOrWhiteSpace(application.Appl_LastUpdate_Usr)) parameters["Appl_LastUpdate_Usr"] = application.Subm_SubmCd;
            if (application.Appl_LastUpdate_Dte is null) parameters.Add("Appl_LastUpdate_Dte", DateTime.Now); else parameters.Add("Appl_LastUpdate_Dte", application.Appl_LastUpdate_Dte.Value);

            return parameters;
        }

        public async Task<DataList<ApplicationData>> GetApplicationsWaitingForAffidavit(string appCategory)
        {
            var result = new DataList<ApplicationData>();

            List<ApplicationData> data = null;

            if (appCategory == "T01")
                data = await MainDB.GetDataFromStoredProcAsync<ApplicationData>("Appl_TracingsWaitingForAffidavit", FillApplicationDataFromReader);
            else if (appCategory == "L01")
                data = await MainDB.GetDataFromStoredProcAsync<ApplicationData>("Appl_LicenceDenialsWaitingForAffidavit", FillApplicationDataFromReader);
            else
            {
                MainDB.LastError = $"Invalid category code: {appCategory}";
                data = [];
            }

            if (!string.IsNullOrEmpty(MainDB.LastError))
                result.Messages.AddSystemError(MainDB.LastError);

            result.Items = data;

            return result;
        }

        public async Task<bool> ApplicationExists(string appl_EnfSrv_Cd, string appl_CtrlCd)
        {
            var parameters = new Dictionary<string, object>
                {
                    {"Appl_EnfSrv_Cd", appl_EnfSrv_Cd},
                    {"Appl_CtrlCd", appl_CtrlCd }
                };

            int count = await MainDB.GetDataFromStoredProcAsync<int>("ApplRecordExists", parameters);

            return count > 0;

        }

        public async Task<string> GenerateApplicationControlCode(string appl_EnfSrv_Cd)
        {
            var parameters = new Dictionary<string, object>
                {
                    {"Appl_EnfSrv_Cd", appl_EnfSrv_Cd}
                };

            return await MainDB.GetDataFromStoredProcAsync<string>("GetApplSequenceNo", parameters);
        }

        public async Task<List<ApplicationModificationActivitySummaryData>> GetApplicationRecentActivityForSubmitter(string submCd, int days = 0)
        {
            var parameters = new Dictionary<string, object> {
                    { "Subm_SubmCd",  submCd},
                    { "Days", days}
                };

            return await MainDB.GetDataFromStoredProcAsync<ApplicationModificationActivitySummaryData>("Appl_GetRecentActivityForSubmitter", parameters, FillModificationHistory);
        }

        public async Task<List<ApplicationModificationActivitySummaryData>> GetApplicationAtStateForSubmitter(string submCd, ApplicationState state)
        {
            var parameters = new Dictionary<string, object> {
                    { "Subm_SubmCd",  submCd},
                    { "AppLiSt_Cd", (int) state}
                };

            return await MainDB.GetDataFromStoredProcAsync<ApplicationModificationActivitySummaryData>("Appl_GetAtStateForSubmitter", parameters, FillModificationHistory);
        }

        public async Task<List<ApplicationModificationActivitySummaryData>> GetApplicationWithEventForSubmitter(string submCd, int eventReasonCode)
        {
            var parameters = new Dictionary<string, object> {
                    { "Subm_SubmCd",  submCd},
                    { "Event_Reas_Cd", eventReasonCode}
                };

            return await MainDB.GetDataFromStoredProcAsync<ApplicationModificationActivitySummaryData>("Appl_GetWithEventForSubmitter", parameters, FillModificationHistory);
        }

        private void FillModificationHistory(IDBHelperReader rdr, ApplicationModificationActivitySummaryData data)
        {
            data.Appl_EnfSrv_Cd = rdr["Appl_EnfSrv_Cd"] as string;
            data.Appl_CtrlCd = rdr["Appl_CtrlCd"] as string;
            data.AppCtgy_Cd = rdr["AppCtgy_Cd"] as string;
            data.Appl_Create_Dte = (DateTime)rdr["Appl_Create_Dte"]; // can be null 
            data.Appl_Create_Usr = rdr["Appl_Create_Usr"] as string; // can be null 
            data.Appl_LastUpdate_Dte = rdr["Appl_LastUpdate_Dte"] as DateTime?; // can be null 
            data.Appl_LastUpdate_Usr = rdr["Appl_LastUpdate_Usr"] as string; // can be null 
            data.AppLiSt_Cd = (ApplicationState)rdr["AppLiSt_Cd"];
        }

        public async Task UpdateSubmitterDefaultControlCode(string subm_SubmCd, string appl_CtrlCd)
        {
            var parameters = new Dictionary<string, object>
                {
                    {"Subm_SubmCd", subm_SubmCd},
                    {"Appl_CtrlCd", appl_CtrlCd }
                };

            _ = await MainDB.ExecProcAsync("SubmUpdateLastSequenceNr", parameters);

        }

        public async Task<bool> GetApplLocalConfirmedSINExists(string enteredSIN, string debtorSurname, DateTime? debtorBirthDate,
                                                   string submCd, string ctrlCd, string debtorFirstName = "")
        {
            var parameters = new Dictionary<string, object>
                {
                    {"enteredSIN", enteredSIN},
                    {"debtorSurname", debtorSurname },
                    {"Subm_SubmCd",  submCd},
                    {"Appl_CtrlCd",  ctrlCd}
                };

            if (!String.IsNullOrEmpty(debtorFirstName))
                parameters.Add("debtorFrstname", debtorFirstName);

            if (debtorBirthDate.HasValue)
                parameters.Add("debtorBirthdate", debtorBirthDate.Value);

            return await MainDB.GetDataFromStoredProcViaReturnParameterAsync<bool>("ApplLocalConfirmedSINExists", parameters, "Appl_Cnfrmd_SIN_Exists");

        }

        public async Task<bool> GetConfirmedSINSameEnforcementOfficeExists(string appl_EnfSrv_Cd, string subm_SubmCd,
                                                                 string appl_CtrlCd, string appl_Dbtr_Cnfrmd_SIN, string categoryCode)
        {
            var parameters = new Dictionary<string, object>
                {
                    {"enteredSIN", appl_Dbtr_Cnfrmd_SIN},
                    {"Subm_SubmCd",  subm_SubmCd},
                    {"Appl_CtrlCd",  appl_CtrlCd},
                    {"Appl_EnfSrv_Cd",  appl_EnfSrv_Cd},
                    {"AppCtgy_Cd",  categoryCode}
                };

            return await MainDB.GetDataFromStoredProcViaReturnParameterAsync<bool>("ApplGetConfirmedSINSameEnforcementOffice", parameters, "sameEnforcementOffice");

        }

        public async Task<List<ConfirmedSinData>> GetConfirmedSinByDebtorId(string debtorId, bool isActiveOnly)
        {
            var parameters = new Dictionary<string, object> {
                    { "chrDebtor_Id", debtorId},
                    { "bitActvst_cd", isActiveOnly}
                };

            return await MainDB.GetDataFromStoredProcAsync<ConfirmedSinData>("GetCnfrmdSINByDebtorID", parameters, FillConfirmedSinForDebtor);
        }

        private void FillConfirmedSinForDebtor(IDBHelperReader rdr, ConfirmedSinData data)
        {
            data.Appl_EnfSrv_Cd = rdr["Appl_EnfSrv_Cd"] as string;
            data.Appl_CtrlCd = rdr["Appl_CtrlCd"] as string;
            data.SVR_SIN = rdr["SVR_SIN"] as string;
        }

        public async Task<List<ApplicationConfirmedSINData>> GetConfirmedSINOtherEnforcementOfficeExists(string appl_EnfSrv_Cd, string subm_SubmCd, string appl_CtrlCd, string appl_Dbtr_Cnfrmd_SIN)
        {
            var parameters = new Dictionary<string, object>
                {
                    {"enteredSIN", appl_Dbtr_Cnfrmd_SIN},
                    {"Subm_SubmCd", subm_SubmCd },
                    {"Appl_CtrlCd", appl_CtrlCd },
                    {"Appl_EnfSrv_Cd", appl_EnfSrv_Cd }
                };

            var data = await MainDB.GetDataFromStoredProcAsync<ApplicationConfirmedSINData>("ApplGetConfirmedSINOtherEnforcementOffice", parameters, FillConfirmedSINDataFromReader);
            return data;

        }
        public async Task<DataList<ApplicationData>> GetRequestedSINApplDataForFile(string enfSrv_Cd, string fileName)
        {
            var parameters = new Dictionary<string, object>
            {
                { "EnfSrv_Cd", enfSrv_Cd },
                { "fileName", fileName }
            };

            var resultData = await MainDB.GetDataFromStoredProcAsync<ApplicationData>("MessageBrokerRequestedSININApplData", parameters, FillApplicationDataFromReader);

            var result = new DataList<ApplicationData>
            {
                Items = resultData
            };

            if (!string.IsNullOrEmpty(MainDB.LastError))
                result.Messages.AddSystemError(MainDB.LastError);

            return result;
        }

        public async Task<List<ApplicationData>> GetSameCreditorForAppCtgy(string appl_CtrlCd, string subm_SubmCd, string appl_Dbtr_Entrd_SIN, byte appl_SIN_Cnfrmd_Ind, string actvSt_Cd, string appCtgy_Cd)
        {
            var parameters = new Dictionary<string, object>
                {
                    {"Appl_CtrlCd", appl_CtrlCd },
                    {"Subm_SubmCd", subm_SubmCd },
                    {"Appl_Dbtr_Entrd_SIN", appl_Dbtr_Entrd_SIN },
                    {"Appl_SIN_Cnfrmd_Ind", appl_SIN_Cnfrmd_Ind },
                    {"ActvSt_Cd", actvSt_Cd },
                    {"AppCtgy ", appCtgy_Cd }
                };

            return await MainDB.GetDataFromStoredProcAsync<ApplicationData>("GetSameCreditorForAppCtgy", parameters, FillApplicationDataFromReader);
        }


        public async Task<(string errorSameEnfOFf, string errorDiffEnfOff)> GetConfirmedSINRecords(string subm_SubmCd, string appl_CtrlCd, string appl_Dbtr_Cnfrmd_SIN)
        {
            var parameters = new Dictionary<string, object>
                {
                    {"enteredSIN", appl_Dbtr_Cnfrmd_SIN},
                    {"Subm_SubmCd", subm_SubmCd },
                    {"Appl_CtrlCd", appl_CtrlCd }
                };

            List<ApplicationConfirmedSINData> data = await MainDB.GetDataFromStoredProcAsync<ApplicationConfirmedSINData>("ApplGetConfirmedSinRecords", parameters, FillConfirmedSINDataFromReader);

            var errorSameEnfOff = new StringBuilder();
            var errorDiffEnfOff = new StringBuilder();

            if (data.Count != 0)
            {
                foreach (ApplicationConfirmedSINData item in data)
                {
                    string tempString = item.Appl_EnfSrv_Cd.Trim() + "-" + item.Subm_SubmCd.Trim() + "-" + item.Appl_CtrlCd.Trim();

                    if (item.Subm_SubmCd[..2].Equals(subm_SubmCd[..2], StringComparison.CurrentCultureIgnoreCase))
                        errorSameEnfOff.Append(tempString + "  ");
                    else
                        errorDiffEnfOff.Append(tempString + "  ");
                }
            }

            if (errorSameEnfOff.Length > 80)
                errorSameEnfOff.Length = 80;

            if (errorDiffEnfOff.Length > 80)
                errorDiffEnfOff.Length = 80;

            return (errorSameEnfOff.ToString(), errorDiffEnfOff.ToString());
        }

        public async Task<List<ApplicationData>> GetDailyApplCountBySIN(string appl_Dbtr_Entrd_SIN,
                                                              string appl_EnfSrv_Cd,
                                                              string appl_CtrlCd,
                                                              string appCtgy_Cd,
                                                              string appl_Source_RfrNr)
        {
            var parameters = new Dictionary<string, object>
                {
                    {"Appl_Dbtr_Entrd_SIN", appl_Dbtr_Entrd_SIN},
                    {"Appl_EnfSrv_Cd", appl_EnfSrv_Cd },
                    {"Appl_CtrlCd", appl_CtrlCd },
                    {"AppCtgy_Cd", appCtgy_Cd}
                };

            if (!String.IsNullOrEmpty(appl_Source_RfrNr))
                parameters.Add("Appl_Source_RfrNr", appl_Source_RfrNr);

            return await MainDB.GetDataFromStoredProcAsync<ApplicationData>("ApplGetDailyApplCountBySIN", parameters, FillApplicationDataFromReader);
        }

        public async Task<List<StatsOutgoingProvincialData>> GetStatsProvincialOutgoingData(int maxRecords,
                                                                                string activeState,
                                                                                string recipientCode,
                                                                                bool isXML = true)
        {

            var parameters = new Dictionary<string, object>
            {
                { "intRecMax", maxRecords },
                { "dchrActvSt_Cd", activeState },
                { "chrRecptCd", recipientCode },
                { "isXML", isXML ? 1 : 0 }
            };

            var data = await MainDB.GetDataFromStoredProcAsync<StatsOutgoingProvincialData>("MessageBrokerGetSTATAPPOUTOutboundData",
                                                                               parameters, FillStatsOutgoingProvincialData);
            return data;

        }

        public async Task<List<ApplicationData>> GetApplicationsForSin(string confirmedSIN)
        {
            var parameters = new Dictionary<string, object> {
                    { "confirmedSIN",  confirmedSIN}
                };

            return await MainDB.GetDataFromStoredProcAsync<ApplicationData>("GetAppsForSIN", parameters, FillApplicationDataFromReader);
        }

        private void FillStatsOutgoingProvincialData(IDBHelperReader rdr, StatsOutgoingProvincialData data)
        {
            data.Event_dtl_Id = (int)rdr["Event_dtl_Id"];
            data.Event_Reas_Cd_Zero = (int)rdr["Event_Reas_Cd"];
            data.Event_Reas_Text_Zero = (int)rdr["Event_Reas_Text"];
            data.Source_ActvSt_Cd = rdr["ActvSt_Cd"] as string;
            data.Recordtype = rdr["Recordtype"] as string;
            data.EnfSrv_Cd = rdr["Val_1"] as string;
            data.Subm_SubmCd = rdr["Val_2"] as string;
            data.Appl_CtrlCd = rdr["Val_3"] as string;
            data.Appl_Source_RfrNr = rdr["Val_4"] as string;
            data.AppCtgy_Cd = rdr["Val_5"] as string;
            data.Subm_Recpt_SubmCd = rdr["Val_6"] as string;
            data.Event_TimeStamp = (DateTime)rdr["Val_7"];
            data.Event_TimeStamp2 = (DateTime)rdr["Val_8"];
            data.ActvSt_Cd = rdr["Val_9"] as string;
            data.AppLiSt_Cd = rdr["Val_10"] as string;
            data.Event_Priority_Ind = rdr["Val_11"] as string;
            data.Event_Reas_Cd = rdr["Val_12"] as string;
            data.Event_Effctv_Dte = (DateTime)rdr["Val_13"];
            data.Event_Compl_Dte = (DateTime)rdr["Val_14"];
            data.Event_Reas_Text = rdr["Val_15"] as string;
        }

        private void FillConfirmedSINDataFromReader(IDBHelperReader rdr, ApplicationConfirmedSINData data)
        {
            data.Appl_EnfSrv_Cd = rdr["Appl_EnfSrv_Cd"] as string;
            data.Appl_CtrlCd = rdr["Appl_CtrlCd"] as string;
            data.Subm_SubmCd = rdr["Subm_SubmCd"] as string;
            data.Subm_Recpt_SubmCd = rdr["Subm_Recpt_SubmCd"] as string;
            if (rdr.ColumnExists("Appl_Crdtr_SurNme"))
            {
                data.Appl_Crdtr_SurNme = rdr["Appl_Crdtr_SurNme"] as string; // can be null 
            }
            data.AppLiSt_Cd = (ApplicationState)rdr["AppLiSt_Cd"];
        }

        public static void FillApplicationDataFromReader(IDBHelperReader rdr, ApplicationData data)
        {
            data.Appl_EnfSrv_Cd = rdr["Appl_EnfSrv_Cd"] as string;
            data.Appl_CtrlCd = rdr["Appl_CtrlCd"] as string;
            data.Subm_SubmCd = rdr["Subm_SubmCd"] as string;
            data.Subm_Recpt_SubmCd = rdr["Subm_Recpt_SubmCd"] as string;
            data.Appl_Lgl_Dte = (DateTime)rdr["Appl_Lgl_Dte"]; // should never be null 
            data.Appl_CommSubm_Text = rdr["Appl_CommSubm_Text"] as string; // can be null 
            data.Appl_Rcptfrm_Dte = (DateTime)rdr["Appl_Rcptfrm_Dte"];
            data.Appl_Group_Batch_Cd = rdr["Appl_Group_Batch_Cd"] as string; // can be null 
            data.Appl_Source_RfrNr = rdr["Appl_Source_RfrNr"] as string; // can be null 
            data.Subm_Affdvt_SubmCd = rdr["Subm_Affdvt_SubmCd"] as string; // can be null 
            data.Appl_RecvAffdvt_Dte = rdr["Appl_RecvAffdvt_Dte"] as DateTime?; // can be null 
            data.Appl_Affdvt_DocTypCd = rdr["Appl_Affdvt_DocTypCd"] as string; // can be null 
            data.Appl_JusticeNr = rdr["Appl_JusticeNr"] as string; // can be null 
            data.Appl_Crdtr_FrstNme = rdr["Appl_Crdtr_FrstNme"] as string; // can be null 
            data.Appl_Crdtr_MddleNme = rdr["Appl_Crdtr_MddleNme"] as string; // can be null 
            data.Appl_Crdtr_SurNme = rdr["Appl_Crdtr_SurNme"] as string; // can be null 
            data.Appl_Dbtr_FrstNme = rdr["Appl_Dbtr_FrstNme"] as string; // can be null 
            data.Appl_Dbtr_MddleNme = rdr["Appl_Dbtr_MddleNme"] as string; // can be null 
            data.Appl_Dbtr_SurNme = rdr["Appl_Dbtr_SurNme"] as string;

            if (rdr.ColumnExists("Appl_Dbtr_Parent_SurNme"))
                data.Appl_Dbtr_Parent_SurNme_Birth = rdr["Appl_Dbtr_Parent_SurNme"] as string; // can be null 

            data.Appl_Dbtr_Brth_Dte = rdr["Appl_Dbtr_Brth_Dte"] as DateTime?; // can be null 
            data.Appl_Dbtr_LngCd = rdr["Appl_Dbtr_LngCd"] as string; // can be null 
            data.Appl_Dbtr_Gendr_Cd = rdr["Appl_Dbtr_Gendr_Cd"] as string;
            data.Appl_Dbtr_Entrd_SIN = rdr["Appl_Dbtr_Entrd_SIN"] as string; // can be null 
            data.Appl_Dbtr_Cnfrmd_SIN = rdr["Appl_Dbtr_Cnfrmd_SIN"] as string; // can be null 
            data.Appl_Dbtr_RtrndBySrc_SIN = rdr["Appl_Dbtr_RtrndBySrc_SIN"] as string; // can be null 
            data.Appl_Dbtr_Addr_Ln = rdr["Appl_Dbtr_Addr_Ln"] as string; // can be null 
            data.Appl_Dbtr_Addr_Ln1 = rdr["Appl_Dbtr_Addr_Ln1"] as string; // can be null 
            data.Appl_Dbtr_Addr_CityNme = rdr["Appl_Dbtr_Addr_CityNme"] as string; // can be null 
            data.Appl_Dbtr_Addr_PrvCd = rdr["Appl_Dbtr_Addr_PrvCd"] as string; // can be null 
            data.Appl_Dbtr_Addr_CtryCd = rdr["Appl_Dbtr_Addr_CtryCd"] as string; // can be null 
            data.Appl_Dbtr_Addr_PCd = rdr["Appl_Dbtr_Addr_PCd"] as string; // can be null 
            data.Medium_Cd = rdr["Medium_Cd"] as string;
            data.Appl_Reactv_Dte = rdr["Appl_Reactv_Dte"] as DateTime?; // can be null 
            data.Appl_SIN_Cnfrmd_Ind = (byte)rdr["Appl_SIN_Cnfrmd_Ind"];
            data.AppCtgy_Cd = rdr["AppCtgy_Cd"] as string;
            data.AppReas_Cd = (rdr["AppReas_Cd"] as string)?.TrimEnd();
            data.Appl_Create_Dte = (DateTime)rdr["Appl_Create_Dte"]; // can be null 
            data.Appl_Create_Usr = rdr["Appl_Create_Usr"] as string; // can be null 
            data.Appl_LastUpdate_Dte = rdr["Appl_LastUpdate_Dte"] as DateTime?; // can be null 
            data.Appl_LastUpdate_Usr = rdr["Appl_LastUpdate_Usr"] as string; // can be null 
            data.ActvSt_Cd = rdr["ActvSt_Cd"] as string;
            data.AppLiSt_Cd = (ApplicationState)rdr["AppLiSt_Cd"];
            data.Appl_WFID = rdr["Appl_WFID"] as Guid?; // can be null 
            data.Appl_Crdtr_Brth_Dte = rdr["Appl_Crdtr_Brth_Dte"] as DateTime?; // can be null 
        }

    }
}
