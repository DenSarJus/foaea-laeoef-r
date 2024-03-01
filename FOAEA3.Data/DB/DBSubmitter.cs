using DBHelper;
using FOAEA3.Data.Base;
using FOAEA3.Model;
using FOAEA3.Model.Interfaces.Repository;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FOAEA3.Data.DB
{
    internal class DBSubmitter(IDBToolsAsync mainDB) : DBbase(mainDB), ISubmitterRepository
    {
        public async Task<List<SubmitterData>> GetSubmitter(string submCode = null,
                                                string submName = null,
                                                string enfOffCode = null,
                                                string enfServCode = null,
                                                string submFName = null,
                                                string submMName = null,
                                                string prov = null)
        {

            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(submCode)) { parameters["Subm_SubmCd"] = submCode; }
            if (!string.IsNullOrEmpty(submName)) { parameters["Subm_SurNme"] = submName; }
            if (!string.IsNullOrEmpty(enfOffCode)) { parameters["EnfOff_City_LocCd"] = enfOffCode; }
            if (!string.IsNullOrEmpty(enfServCode)) { parameters["EnfSrv_Cd"] = enfServCode; }
            if (!string.IsNullOrEmpty(submFName)) { parameters["Subm_FrstNme"] = submMName; }
            if (!string.IsNullOrEmpty(submFName)) { parameters["Subm_MddleNme"] = submMName; }
            if (!string.IsNullOrEmpty(prov)) { parameters["ProvCode"] = prov; }

            return await MainDB.GetDataFromStoredProcAsync<SubmitterData>("SubmGetSubm", parameters, FillSubmitterData);

        }

        public async Task<List<SubmitterData>> GetSubmitterIncludingClosed(string submCode = null,
                                                string submName = null,
                                                string enfOffCode = null,
                                                string enfServCode = null,
                                                string submFName = null,
                                                string submMName = null,
                                                string prov = null)
        {

            var parameters = new Dictionary<string, object>();

            if (!string.IsNullOrEmpty(submCode)) { parameters["Subm_SubmCd"] = submCode; }
            if (!string.IsNullOrEmpty(submName)) { parameters["Subm_SurNme"] = submName; }
            if (!string.IsNullOrEmpty(enfOffCode)) { parameters["EnfOff_City_LocCd"] = enfOffCode; }
            if (!string.IsNullOrEmpty(enfServCode)) { parameters["EnfSrv_Cd"] = enfServCode; }
            if (!string.IsNullOrEmpty(submFName)) { parameters["Subm_FrstNme"] = submMName; }
            if (!string.IsNullOrEmpty(submFName)) { parameters["Subm_MddleNme"] = submMName; }
            if (!string.IsNullOrEmpty(prov)) { parameters["ProvCode"] = prov; }

            return await MainDB.GetDataFromStoredProcAsync<SubmitterData>("SubmGetSubmAll", parameters, FillSubmitterData);

        }


        public async Task<List<string>> GetSubmitterCodesForOffice(string service, string office)
        {
            var parameters = new Dictionary<string, object> {
                    { "EnforcementOfficeCode",  office},
                    { "EnforcementServiceCode",  service}
                };

            var data = await MainDB.GetRecordsFromStoredProcAsync<string>("SubmGetEnforcementOfficeSubmitters", parameters, FillCodesFromReader);
            return data;
        }

        public async Task<string> GetFOAEAOfficersEmail()
        {
            string officers = await MainDB.GetDataFromProcSingleValueAsync<string>("GetFOAEAOfficersEmails", null);
            return officers;
        }

        private void FillCodesFromReader(IDBHelperReader rdr, out string code)
        {
            code = rdr["SubmitterCode"] as string;
        }

        public async Task<List<CommissionerData>> GetCommissioners(string locationCode, string currentSubmitter)
        {

            var submittersForLocation = await GetSubmitter(enfOffCode: locationCode);
            var commissioners = (from s in submittersForLocation
                                 where (s.Subm_LglSgnAuth_Ind && (s.Subm_SubmCd != currentSubmitter) && (s.ActvSt_Cd == "A"))
                                 select new CommissionerData
                                 {
                                     Subm_SubmCd = s.Subm_SubmCd,
                                     Subm_Name = (s.Subm_SurNme + ", " + s.Subm_FrstNme + ' ' + ((string.IsNullOrEmpty(s.Subm_MddleNme)) ? "" : s.Subm_MddleNme)).Trim()
                                 }).OrderBy(m => m.Subm_Name).ToList<CommissionerData>();

            return commissioners;

        }

        public async Task<string> GetSignAuthorityForSubmitter(string submCd)
        {
            var parameters = new Dictionary<string, object>
            {
                { "Subm_SubmCd", submCd }
            };

            return await MainDB.GetDataFromStoredProcAsync<string>("GetSignAuthorityForSubmCd", parameters);
        }

        public async Task<string> GetMaxSubmitterCode(string submCodePart)
        {
            var parameters = new Dictionary<string, object>
            {
                { "submCodePart", submCodePart }
            };

            return await MainDB.GetDataFromStoredProcAsync<string>("SubmGetMaxSubmitterCode", parameters);
        }

        public async Task CreateSubmitter(SubmitterData newSubmitter)
        {
            var parameters = GetParametersForSubmitterData2(newSubmitter);

            await MainDB.ExecProcAsync("SubmInsert", parameters);
        }



        public async Task UpdateSubmitter(SubmitterData newSubmitter)
        {
            // if update, then call new CRUD proc "Subm_Update"
            await MainDB.UpdateDataAsync<SubmitterData, string>("Subm", newSubmitter, "Subm_SubmCd", newSubmitter.Subm_SubmCd, SetParametersForUpdateSubmitterData);

            if (!String.IsNullOrEmpty(MainDB.LastError))
                newSubmitter.Messages.AddSystemError("Database Error: " + MainDB.LastError);

        }
        public async Task<DateTime> UpdateSubmitterLastLogin(string submCd)
        {
            DateTime loginDate = DateTime.Now;

            var parameters = new Dictionary<string, object>
            {
                { "NewLoginDateTime", loginDate.ToString() },
                { "SubmitterID", submCd }

            };

            await MainDB.ExecProcAsync("SubmUpdateLastLogin", parameters);

            return loginDate;
        }

        private static void SetParametersForUpdateSubmitterData(SubmitterData data, Dictionary<string, object> parameters)
        {
            var baseParameters = GetParametersForSubmitterData(data);
            foreach (var parameter in baseParameters)
                parameters.Add(parameter.Key, parameter.Value);
        }


        private static bool ByteToBool(byte value)
        {
            return value != 0;
        }

        private static string ConvertToString(int value)
        {
            string result = value.ToString();

            return value == 0 ? string.Empty : result;
        }
        private static string ConvertToString(short value)
        {
            string result = value.ToString();

            return value == 0 ? string.Empty : result;
        }

        private static Dictionary<string, object> GetParametersForSubmitterData(SubmitterData data)
        {
            var parameters = new Dictionary<string, object>
            {
                { "Subm_FrstNme", data.Subm_FrstNme },
                { "Subm_MddleNme", data.Subm_MddleNme },
                { "Subm_SurNme", data.Subm_SurNme },
                { "Lng_Cd", data.Lng_Cd },
                { "Subm_LstLgn_Dte", data.Subm_LstLgn_Dte },
                { "Subm_Assg_Email", data.Subm_Assg_Email },
                { "Subm_IP_Addr", data.Subm_IP_Addr },
                { "Subm_Lic_AccsPrvCd", data.Subm_Lic_AccsPrvCd },
                { "Subm_Trcn_AccsPrvCd", data.Subm_Trcn_AccsPrvCd },
                { "Subm_Intrc_AccsPrvCd", data.Subm_Intrc_AccsPrvCd },
                { "Subm_Tel_AreaC", data.Subm_Tel_AreaC },
                { "Subm_TelNr", data.Subm_TelNr },
                { "Subm_TelEx", data.Subm_TelEx },
                { "Subm_Fax_AreaC", data.Subm_Fax_AreaC },
                { "Subm_FaxNr", data.Subm_FaxNr },
                { "Subm_Last_SeqNr", data.Subm_Last_SeqNr },
                { "Subm_Altrn_SubmCd", data.Subm_Altrn_SubmCd },
                { "EnfSrv_Cd", data.EnfSrv_Cd },
                { "EnfOff_City_LocCd", data.EnfOff_City_LocCd },
                { "Subm_Title", data.Subm_Title },
                { "Subm_TrcNtf_Ind", data.Subm_TrcNtf_Ind ? 1 : 0 },
                { "Subm_LglSgnAuth_Ind", data.Subm_LglSgnAuth_Ind ? 1 : 0 },
                { "Subm_SgnAuth_SubmCd", data.Subm_SgnAuth_SubmCd },
                { "Subm_Comments", data.Subm_Comments },
                { "Subm_EnfSrvAuth_Ind", data.Subm_EnfSrvAuth_Ind ? 1 : 0 },
                { "Subm_EnfOffAuth_Ind", data.Subm_EnfOffAuth_Ind ? 1 : 0 },
                { "Subm_SysMgr_Ind", data.Subm_SysMgr_Ind ? 1 : 0 },
                { "Subm_AppMgr_Ind", data.Subm_AppMgr_Ind ? 1 : 0 },
                { "Subm_Fin_Ind", data.Subm_Fin_Ind ? 1 : 0 },
                { "Subm_CourtUsr_Ind", data.Subm_CourtUsr_Ind ? 1 : 0 },
                { "ActvSt_Cd", data.ActvSt_Cd },
                { "Subm_LiSt_Cd", data.Subm_LiSt_Cd },
                { "Subm_Class", data.Subm_Class },
                { "Subm_LastUpdate_Usr", data.Subm_LastUpdate_Usr }
            };

            return parameters;
        }

        private static Dictionary<string, object> GetParametersForSubmitterData2(SubmitterData data)
        {
            var parameters = new Dictionary<string, object>
            {
                { "SubmitterCode", data.Subm_SubmCd },
                { "FirstName", data.Subm_FrstNme },
                { "MiddleName", data.Subm_MddleNme },
                { "SurName", data.Subm_SurNme },
                { "Language", data.Lng_Cd },
                { "Email", data.Subm_Assg_Email },
                { "IPAddress", data.Subm_IP_Addr ?? "" },
                { "LicencingAccess", data.Subm_Lic_AccsPrvCd ? 1 : 0 },
                { "TracingAccess", data.Subm_Trcn_AccsPrvCd ? 1 : 0 },
                { "InterceptionAccess", data.Subm_Intrc_AccsPrvCd ? 1 : 0 },
                { "TelephoneAreaCode", int.Parse(data.Subm_Tel_AreaC) },
                { "TelephoneNumber", int.Parse(data.Subm_TelNr.Remove(3,1)) },
                { "TelephoneExtension", int.Parse(data.Subm_TelEx) },
                { "FaxAreaCode", int.Parse(data.Subm_Fax_AreaC) },
                { "FaxNumber", int.Parse(data.Subm_FaxNr) },
                { "AlternateSubmitter", data.Subm_Create_Usr },
                { "EnforcementServiceCode", data.EnfSrv_Cd },
                { "EnforcementOfficeCity", data.EnfOff_City_LocCd },
                { "SubmitterTitle", data.Subm_Title },
                { "TracingNotification", data.Subm_TrcNtf_Ind ? 1 : 0 },
                { "LegalSigningAuthority", data.Subm_LglSgnAuth_Ind ? 1 : 0 },
                { "SigningAuthoritySubmitterCode", data.Subm_SgnAuth_SubmCd ?? ""},
                { "Comments", data.Subm_Comments },
                { "EnforcementServiceAuthority", data.Subm_EnfSrvAuth_Ind ? 1 : 0 },
                { "EnforcementOfficeAuthority", data.Subm_EnfOffAuth_Ind ? 1 : 0 },
                { "SystemManagerIndicator", data.Subm_SysMgr_Ind ? 1 : 0 },
                { "ApplicationManagerIndicator", data.Subm_AppMgr_Ind ? 1 : 0 },
                { "FinancialIndicator", data.Subm_Fin_Ind ? 1 : 0 },
                { "CourtUserIndicator", data.Subm_CourtUsr_Ind ? 1 : 0 },
                { "Status", data.ActvSt_Cd },
                { "SubmitterClass", data.Subm_Class },
                { "SubmittedBy", data.Subm_Create_Usr },
                { "ReceiveAuditIndicatior", data.Subm_Audit_File_Ind }
            };

            return parameters;
        }

        private void FillSubmitterData(IDBHelperReader rdr, SubmitterData data)
        {
            data.Subm_SubmCd = rdr["Subm_SubmCd"] as string;
            data.Subm_FrstNme = rdr["Subm_FrstNme"] as string;
            data.Subm_MddleNme = rdr["Subm_MddleNme"] as string; // can be null
            data.Subm_SurNme = rdr["Subm_SurNme"] as string;
            data.Lng_Cd = rdr["Lng_Cd"] as string;
            data.Subm_LstLgn_Dte = rdr["Subm_LstLgn_Dte"] as DateTime?; // can be null
            data.Subm_Assg_Email = rdr["Subm_Assg_Email"] as string; // can be null
            data.Subm_IP_Addr = rdr["Subm_IP_Addr"] as string; // can be null
            data.Subm_Lic_AccsPrvCd = rdr["Subm_Lic_AccsPrvCd"] != null && ByteToBool((byte)rdr["Subm_Lic_AccsPrvCd"]); // can be null
            data.Subm_Trcn_AccsPrvCd = rdr["Subm_Trcn_AccsPrvCd"] != null && ByteToBool((byte)rdr["Subm_Trcn_AccsPrvCd"]); // can be null
            data.Subm_Intrc_AccsPrvCd = rdr["Subm_Intrc_AccsPrvCd"] != null && ByteToBool((byte)rdr["Subm_Intrc_AccsPrvCd"]); // can be null
            data.Subm_Tel_AreaC = ConvertToString((short)rdr["Subm_Tel_AreaC"]);
            data.Subm_TelNr = ConvertToString((int)rdr["Subm_TelNr"]);
            data.Subm_TelEx = rdr["Subm_TelEx"] != null ? ConvertToString((int)rdr["Subm_TelEx"]) : string.Empty; // can be null
            data.Subm_Fax_AreaC = rdr["Subm_Fax_AreaC"] != null ? ConvertToString((short)rdr["Subm_Fax_AreaC"]) : string.Empty; // can be null
            data.Subm_FaxNr = rdr["Subm_FaxNr"] != null ? ConvertToString((int)rdr["Subm_FaxNr"]) : string.Empty; // can be null
            data.Subm_Last_SeqNr = rdr["Subm_Last_SeqNr"] as string; // can be null
            data.Subm_Altrn_SubmCd = rdr["Subm_Altrn_SubmCd"] as string; // can be null
            data.EnfSrv_Cd = rdr["EnfSrv_Cd"] as string;
            data.EnfOff_City_LocCd = rdr["EnfOff_City_LocCd"] as string;
            data.Subm_Title = rdr["Subm_Title"] as string; // can be null
            data.Subm_SgnAuth_SubmCd = rdr["Subm_SgnAuth_SubmCd"] as string; // can be null
            data.Subm_Comments = rdr["Subm_Comments"] as string; // can be null
            data.Subm_TrcNtf_Ind = rdr["Subm_TrcNtf_Ind"] != null && ByteToBool((byte)rdr["Subm_TrcNtf_Ind"]);
            data.Subm_LglSgnAuth_Ind = rdr["Subm_LglSgnAuth_Ind"] != null && ByteToBool((byte)rdr["Subm_LglSgnAuth_Ind"]);
            data.Subm_EnfSrvAuth_Ind = rdr["Subm_EnfSrvAuth_Ind"] != null && ByteToBool((byte)rdr["Subm_EnfSrvAuth_Ind"]);
            data.Subm_EnfOffAuth_Ind = rdr["Subm_EnfOffAuth_Ind"] != null && ByteToBool((byte)rdr["Subm_EnfOffAuth_Ind"]);
            data.Subm_Audit_File_Ind = (bool)rdr["Subm_Audit_File_Ind"];
            data.Subm_SysMgr_Ind = rdr["Subm_SysMgr_Ind"] != null && ByteToBool((byte)rdr["Subm_SysMgr_Ind"]);
            data.Subm_AppMgr_Ind = rdr["Subm_AppMgr_Ind"] != null && ByteToBool((byte)rdr["Subm_AppMgr_Ind"]);
            data.Subm_Fin_Ind = rdr["Subm_Fin_Ind"] != null && ByteToBool((byte)rdr["Subm_Fin_Ind"]);
            data.Subm_CourtUsr_Ind = rdr["Subm_CourtUsr_Ind"] != null && ByteToBool((byte)rdr["Subm_CourtUsr_Ind"]);
            data.ActvSt_Cd = rdr["ActvSt_Cd"] as string;
            data.Subm_LiSt_Cd = rdr["Subm_LiSt_Cd"] as short?; // can be null
            data.Subm_Class = rdr["Subm_Class"] as string; // can be null
            data.Subm_LastUpdate_Usr = rdr["Subm_LastUpdate_Usr"] as string;

            // trim strings
            data.Subm_SubmCd = data.Subm_SubmCd?.Trim();
            data.Subm_FrstNme = data.Subm_FrstNme?.Trim();
            data.Subm_MddleNme = data.Subm_MddleNme?.Trim(); // can be null
            data.Subm_SurNme = data.Subm_SurNme?.Trim();
            data.Subm_Assg_Email = data.Subm_Assg_Email?.Trim(); // can be null
            data.Subm_IP_Addr = data.Subm_IP_Addr?.Trim(); // can be null
            data.Subm_Last_SeqNr = data.Subm_Last_SeqNr?.Trim(); // can be null
            data.Subm_Altrn_SubmCd = data.Subm_Altrn_SubmCd?.Trim(); // can be null
            data.EnfSrv_Cd = data.EnfSrv_Cd?.Trim();
            data.EnfOff_City_LocCd = data.EnfOff_City_LocCd?.Trim();
            data.Subm_Title = data.Subm_Title?.Trim(); // can be null
            data.Subm_SgnAuth_SubmCd = data.Subm_SgnAuth_SubmCd?.Trim(); // can be null
            data.Subm_Comments = data.Subm_Comments?.Trim(); // can be null
            data.ActvSt_Cd = data.ActvSt_Cd?.Trim();
            data.Subm_Class = data.Subm_Class?.Trim(); // can be null
            data.Subm_LastUpdate_Usr = data.Subm_LastUpdate_Usr?.Trim();


        }

        #region SubmitterMessage

        public async Task SubmitterMessageDelete(string submitterID)
        {
            var parameters = new Dictionary<string, object>
            {
                { "SubmitterID", submitterID }
            };

            await MainDB.ExecProcAsync("SubmMsgDeleteSubmMsg", parameters);
        }

        public async Task CreateSubmitterMessage(SubmitterMessageData submitterMessage)
        {
            var parameters = new Dictionary<string, object>
            {
                { "Subm_SubmCd", submitterMessage.Subm_SubmCd },
                { "Appl_EnfSrv_Cd", submitterMessage.Appl_EnfSrv_Cd },
                { "Appl_CtrlCd", submitterMessage.Appl_CtrlCd },
                { "AppLiSt_Cd", submitterMessage.AppLiSt_Cd },
                { "Msg_Nr", submitterMessage.Msg_Nr },
                { "Owner_EnfSrv_Cd", submitterMessage.Owner_EnfSrv_Cd },
                { "Owner_SubmCd", submitterMessage.Owner_SubmCd }
            };

            await MainDB.ExecProcAsync("SubmMsgInsert", parameters);
        }

        // exec SubmMsgGetSubmMsg @UserID=N'ON2D68',@LanguageCode=1033

        public async Task<List<SubmitterMessageData>> GetSubmitterMessageForSubmitter(string submitterID, int languageCode)
        {
            var parameters = new Dictionary<string, object>
            {
                { "UserID", submitterID },
                { "LanguageCode", languageCode }
            };

            return await MainDB.GetDataFromStoredProcAsync<SubmitterMessageData>("SubmMsgGetSubmMsg", parameters, FillSubmitterMessageData);

        }

        private void FillSubmitterMessageData(IDBHelperReader rdr, SubmitterMessageData data)
        {
            if (rdr.ColumnExists("Subm_SubmCd"))
                data.Subm_SubmCd = rdr["Subm_SubmCd"] as string;
            if (rdr.ColumnExists("Appl_EnfSrv_Cd"))
                data.Appl_EnfSrv_Cd = rdr["Appl_EnfSrv_Cd"] as string; // can be null 

            data.Appl_CtrlCd = rdr["Appl_CtrlCd"] as string; // can be null 
            data.AppLiSt_Cd = rdr["AppLiSt_Cd"] as short?; // can be null 
            data.Msg_Nr = rdr["Msg_Nr"] as int?; // can be null 
            data.Owner_EnfSrv_Cd = rdr["Owner_EnfSrv_Cd"] as string; // can be null 
            data.Owner_SubmCd = rdr["Owner_SubmCd"] as string; // can be null 

            if (rdr.ColumnExists("customMessage_en"))
                data.CustomMessage_en = rdr["customMessage_en"] as string; // can be null 
            if (rdr.ColumnExists("customMessage_fr"))
                data.CustomMessage_fr = rdr["customMessage_fr"] as string; // can be null 
            if (rdr.ColumnExists("displayContext"))
                data.DisplayContext = rdr["displayContext"] as Guid?; // can be null 

            if (rdr.ColumnExists("AppLiSt_Txt_E"))
                data.AppLiSt_Txt_E = rdr["AppLiSt_Txt_E"] as string; // can be null 
            if (rdr.ColumnExists("AppLiSt_Txt_F"))
                data.AppLiSt_Txt_F = rdr["AppLiSt_Txt_F"] as string; // can be null 
            if (rdr.ColumnExists("description"))
                data.Description = rdr["description"] as string; // can be null 
            if (rdr.ColumnExists("Appl_JusticeNr"))
                data.Appl_JusticeNr = rdr["Appl_JusticeNr"] as string; // can be null 
            if (rdr.ColumnExists("AppCtgy_Cd"))
                data.AppCtgy_Cd = rdr["AppCtgy_Cd"] as string; // can be null 

        }

        #endregion

    }
}
