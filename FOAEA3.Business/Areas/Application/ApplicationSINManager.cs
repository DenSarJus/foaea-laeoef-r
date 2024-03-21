using FOAEA3.Model.Base;

namespace FOAEA3.Business.Areas.Application;

internal class ApplicationSINManager
{
    private IRepositories DB { get; }

    private ApplicationData Application { get; }
    private ApplicationManager ApplicationManager { get; }
    private ApplicationEventManager EventManager { get => ApplicationManager.EventManager; }

    public ApplicationSINManager(ApplicationData application, ApplicationManager manager)
    {
        DB = manager.DB;
        Application = application;
        ApplicationManager = manager;
    }

    public async Task<List<SINOutgoingFederalData>> GetFederalOutgoingData(int maxRecords, string activeState, ApplicationState lifeState,
                                                               string enfServiceCode)
    {
        var sinDB = DB.SINResultTable;
        return await sinDB.GetFederalSINOutgoingData(maxRecords, activeState, lifeState, enfServiceCode);
    }

    public async Task SINconfirmationBypass(string applDbtrEntrdSIN, string lastUpdateUser, bool swapNames = false,
                                      string HRDCcomments = "")
    {
        Application.Appl_LastUpdate_Usr = lastUpdateUser;
        Application.Appl_LastUpdate_Dte = DateTime.Now;

        if (Application.AppLiSt_Cd == ApplicationState.SIN_NOT_CONFIRMED_5)
        {
            EventManager.AddEvent(EventCode.C51116_SIN_HAS_BEEN_MANUALLY_CONFIRMED);
            await EventManager.SaveEvents();

            await SINconfirmation(true, applDbtrEntrdSIN, lastUpdateUser, swapNames, HRDCcomments);
        }
        else
        {
            EventManager.AddEvent(EventCode.C50933_INVALID_OPERATION_FROM_THE_CURRENT_LIFE_STATE, $"Inv. action {(int)Application.AppLiSt_Cd} <> 5");
            await EventManager.SaveEvents();

            return;
        }

        await ApplicationManager.LoadApplication(Application.Appl_EnfSrv_Cd, Application.Appl_CtrlCd);

        if (Application.Medium_Cd != "FTP") Application.Messages.AddInformation(EventCode.C50620_VALID_APPLICATION);

    }

    public async Task SINconfirmation(bool isSinConfirmed, string confirmedSin, string lastUpdateUser, bool swapNames = false,
                                      string HRDCcomments = "")
    {

        if (swapNames)
        {
            string surname = Application.Appl_Dbtr_SurNme;
            Application.Appl_Dbtr_SurNme = Application.Appl_Dbtr_FrstNme;
            Application.Appl_Dbtr_FrstNme = surname;
        }

        Application.Appl_LastUpdate_Usr = lastUpdateUser;
        Application.Appl_LastUpdate_Dte = DateTime.Now;

        if (Application.AppLiSt_Cd.NotIn(ApplicationState.SIN_CONFIRMATION_PENDING_3, ApplicationState.SIN_NOT_CONFIRMED_5))
        {
            EventManager.AddEvent(EventCode.C50933_INVALID_OPERATION_FROM_THE_CURRENT_LIFE_STATE, $"Inv. action {(int)Application.AppLiSt_Cd} <>3, <>5");
            await EventManager.SaveEvents();

            return;
        }

        if (isSinConfirmed)
        {
            if (ValidationHelper.IsValidSinNumberMod10(confirmedSin))
            {
                Application.Appl_Dbtr_Cnfrmd_SIN = confirmedSin;
                Application.Appl_SIN_Cnfrmd_Ind = 1;

                await ApplicationManager.SetNewStateTo(ApplicationState.SIN_CONFIRMED_4);
            }
            else
            {
                EventManager.AddEvent(EventCode.C50523_INVALID_SIN, appState: ApplicationState.INVALID_APPLICATION_1);

                await ApplicationManager.SetNewStateTo(ApplicationState.INVALID_APPLICATION_1);
            }
        }
        else
        {
            Application.Appl_Dbtr_Cnfrmd_SIN = null;
            Application.Appl_SIN_Cnfrmd_Ind = 0;

            await ApplicationManager.SetNewStateTo(ApplicationState.SIN_NOT_CONFIRMED_5);
        }

        ApplicationManager.MakeUpperCase();

        await UpdateSINChangeHistory(HRDCcomments);

        await ApplicationManager.UpdateApplicationNoValidation();

        await EventManager.SaveEvents();
    }

    public async Task UpdateSINChangeHistory(string HRDCcomments = "")
    {
        if (Application.Appl_SIN_Cnfrmd_Ind == 1)
        {
            var sinChangeHistoryData = new SINChangeHistoryData
            {
                Appl_CtrlCd = Application.Appl_CtrlCd,
                Appl_EnfSrv_Cd = Application.Appl_EnfSrv_Cd,
                Appl_Dbtr_SurNme = Application.Appl_Dbtr_SurNme,
                Appl_Dbtr_FrstNme = Application.Appl_Dbtr_FrstNme,
                Appl_Dbtr_MddleNme = Application.Appl_Dbtr_MddleNme,
                AppLiSt_Cd = (int)Application.AppLiSt_Cd,
                Appl_Dbtr_Cnfrmd_SIN = Application.Appl_Dbtr_Cnfrmd_SIN
            };

            if (!string.IsNullOrEmpty(HRDCcomments) && (HRDCcomments.Contains("ESDC")))
                sinChangeHistoryData.SINChangeHistoryUser = "ESDC_XLSFile";
            else
                sinChangeHistoryData.SINChangeHistoryUser = Application.Appl_LastUpdate_Usr;

            if (!string.IsNullOrEmpty(HRDCcomments))
                sinChangeHistoryData.SINChangeHistoryComment = HRDCcomments;
            else
            {
                sinChangeHistoryData.SINChangeHistoryComment = "SIN Confirmation";
                EventManager.AddEvent(EventCode.C50650_SIN_CONFIRMED, appState: ApplicationState.PENDING_ACCEPTANCE_SWEARING_6,
                                      eventReasonText: await ApplicationManager.GetSINResultsEventText());
            }

            await DB.SINChangeHistoryTable.CreateSINChangeHistory(sinChangeHistoryData);
        }

    }

    public async Task<string> ProcessCRAPendingUpdateSIN(string action, string oldSIN = "", string newSIN = "")
    {
        string result = string.Empty;

        switch (action.ToLower())
        {
            case "insert":
                await DB.CraSinPendingTable.Insert(oldSIN, newSIN);
                result = $"SIN ({oldSIN}) for changing is added to the queue.";
                break;

            case "delete":
                await DB.CraSinPendingTable.Delete(newSIN);
                result = $"Pending SIN ({newSIN}) for changing is completed and deleted from the queue.";
                break;
        }

        return result;
    }

    public async Task<DataList<SINResultData>> GetSINResults()
    {
        return await DB.SINResultTable.GetSINResults(Application.Appl_EnfSrv_Cd, Application.Appl_CtrlCd);
    }

    public async Task<DataList<SINResultWithHistoryData>> GetSINResultsWithHistory()
    {
        return await DB.SINResultTable.GetSINResultsWithHistory(Application.Appl_EnfSrv_Cd, Application.Appl_CtrlCd);
    }

    public async Task CreateResultData(List<SINResultData> resultData)
    {
        var responsesDB = DB.SINResultTable;
        await responsesDB.InsertBulkData(resultData);
    }

    public string GetSinForApplication()
    {
        string sin = string.Empty;

        if (Application.Appl_SIN_Cnfrmd_Ind == 1)
        {
            if (!string.IsNullOrEmpty(Application.Appl_Dbtr_Cnfrmd_SIN))
                sin = Application.Appl_Dbtr_Cnfrmd_SIN;
        }
        else if (!string.IsNullOrEmpty(Application.Appl_Dbtr_Entrd_SIN))
            sin = Application.Appl_Dbtr_Entrd_SIN;

        return sin;
    }

}
