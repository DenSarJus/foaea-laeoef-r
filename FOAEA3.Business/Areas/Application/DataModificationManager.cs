using System.Security.Claims;

namespace FOAEA3.Business.Areas.Application;

internal class DataModificationManager
{
    public IRepositories DB { get; private set; }
    public IRepositories_Finance DBfinance { get; private set; }
    protected IFoaeaConfigurationHelper Config { get; private set; }

    private FoaeaUser _currentUser;
    public FoaeaUser CurrentUser
    {
        get
        {
            return _currentUser;
        }
        private set
        {
            _currentUser = value;

            if (DB is not null)
                DB.CurrentSubmitter = this.CurrentUser.Submitter.Subm_SubmCd;
        }
    }

    protected async Task SetCurrentUser(ClaimsPrincipal user)
    {
        CurrentUser = await UserHelper.ExtractDataFromUser(user, DB);
    }
    public DataModificationManager(IRepositories repositories, IRepositories_Finance financialRepository,
                                   IFoaeaConfigurationHelper config, ClaimsPrincipal user)
    {
        DB = repositories;
        DBfinance = financialRepository;
        Config = config;
        SetCurrentUser(user).Wait();
    }

    public async Task<string> Update(DataModificationData dataModicationsData)
    {
        string sMessage = string.Empty;
        bool updateL01SINChange = false;
        ApplicationData application = null;

        if (dataModicationsData.Applications.Any())
            application = dataModicationsData.Applications.First();

        switch (dataModicationsData.UpdateAction)
        {
            case DataModAction.ExGratiaBypass:
                if (application is not null)
                    await DB.ApplicationTable.UpdateApplication(application);
                break;

            case DataModAction.RemoveSIN:
                if (dataModicationsData.Applications.Any())
                {
                    updateL01SINChange = true;

                    foreach (var appl in dataModicationsData.Applications)
                    {
                        if (appl.ActvSt_Cd == "A")
                            sMessage = "redCancelbeforeremovingSIN";
                        else if (appl.ActvSt_Cd == "X")
                        {
                            switch (appl.AppCtgy_Cd)
                            {
                                case "I01":
                                    int CRA_EISO_amount = await GetCRA_EISO_amount(dataModicationsData.PreviousConfirmedSIN);
                                    if (CRA_EISO_amount > 0)
                                        sMessage = "redSINCRAamount";
                                    else if (appl.AppLiSt_Cd == ApplicationState.MANUALLY_TERMINATED_14)
                                        if (UpdatedLessThan24HoursAgo(appl))
                                            sMessage = "redSIN24hournotice";
                                    break;
                                default:
                                    if (UpdatedLessThan24HoursAgo(appl))
                                        sMessage = "redSIN24hournotice";
                                    break;
                            }
                        }
                    }

                    if (!string.IsNullOrEmpty(sMessage))
                    {
                        return sMessage;
                    }
                    else
                    {
                        foreach (var appl in dataModicationsData.Applications)
                        {
                            appl.Appl_Dbtr_Cnfrmd_SIN = null;
                            appl.Appl_SIN_Cnfrmd_Ind = 0;

                            await DB.ApplicationTable.UpdateApplication(appl);

                            var sinChangeHistoryData = new SINChangeHistoryData
                            {
                                Appl_CtrlCd = appl.Appl_CtrlCd,
                                Appl_EnfSrv_Cd = appl.Appl_EnfSrv_Cd,
                                Appl_Dbtr_SurNme = appl.Appl_Dbtr_SurNme,
                                Appl_Dbtr_FrstNme = appl.Appl_Dbtr_FrstNme,
                                Appl_Dbtr_MddleNme = appl.Appl_Dbtr_MddleNme,
                                AppLiSt_Cd = (int)appl.AppLiSt_Cd,
                                Appl_Dbtr_Cnfrmd_SIN = appl.Appl_Dbtr_Cnfrmd_SIN,
                                SINChangeHistoryUser = appl.Appl_LastUpdate_Usr
                            };

                            if (!string.IsNullOrEmpty(dataModicationsData.SinUpdateComment))
                                sinChangeHistoryData.SINChangeHistoryComment = dataModicationsData.SinUpdateComment;
                            else
                                sinChangeHistoryData.SINChangeHistoryComment = "SIN removed";

                            await DB.SINChangeHistoryTable.CreateSINChangeHistory(sinChangeHistoryData);

                            sMessage = $"SINremoved";
                        }
                    }
                }
                break;

            case DataModAction.UpdateConfirmedSIN:
                if (application is not null)
                {
                    updateL01SINChange = true;

                    bool activeDFexistsForSIN = await DBfinance.SummDFRepository.ActiveDFExistsForSin(dataModicationsData.PreviousConfirmedSIN);
                    if (!activeDFexistsForSIN)
                    {
                        await DB.ApplicationTable.UpdateApplication(application);

                        var sinChangeHistoryData = new SINChangeHistoryData
                        {
                            Appl_CtrlCd = application.Appl_CtrlCd,
                            Appl_EnfSrv_Cd = application.Appl_EnfSrv_Cd,
                            Appl_Dbtr_SurNme = application.Appl_Dbtr_SurNme,
                            Appl_Dbtr_FrstNme = application.Appl_Dbtr_FrstNme,
                            Appl_Dbtr_MddleNme = application.Appl_Dbtr_MddleNme,
                            AppLiSt_Cd = (int)application.AppLiSt_Cd,
                            Appl_Dbtr_Cnfrmd_SIN = application.Appl_Dbtr_Cnfrmd_SIN
                        };

                        if (!string.IsNullOrEmpty(dataModicationsData.SinUpdateComment) && (dataModicationsData.SinUpdateComment.Contains("SIN updated by CRA")))
                            sinChangeHistoryData.SINChangeHistoryUser = "CRAFile";
                        else
                            sinChangeHistoryData.SINChangeHistoryUser = application.Appl_LastUpdate_Usr;

                        if (!string.IsNullOrEmpty(dataModicationsData.SinUpdateComment))
                            sinChangeHistoryData.SINChangeHistoryComment = dataModicationsData.SinUpdateComment;
                        else
                            sinChangeHistoryData.SINChangeHistoryComment = "SIN changed without a reason!";

                        bool success = await DB.SINChangeHistoryTable.CreateSINChangeHistory(sinChangeHistoryData);
                        if (!success)
                            sMessage = $"Confirmed SIN {dataModicationsData.PreviousConfirmedSIN} was not changed. Insert SINChangeHistory failed.";
                    }
                    else
                        sMessage = $"Confirmed SIN {dataModicationsData.PreviousConfirmedSIN} was not changed. DF transaction exists without corresponding FT transaction.";
                }
                break;

            case DataModAction.UpdateDebtorInfo:
                if (application is not null)
                {
                    await DB.ApplicationTable.UpdateApplication(application);
                    await CreateDataModificationEvent(application);
                    sMessage = "Debtor Info Updated";
                }
                break;

            default:
                break;
        }

        if (application is not null)
        {
            if ((application.AppCtgy_Cd == "L01") && (application.ActvSt_Cd == "A"))
            {
                if (!updateL01SINChange)
                {
                    var eventManager = new ApplicationEventManager(application, DB);
                    eventManager.AddEvent(EventCode.C50529_APPLICATION_UPDATED_SUCCESSFULLY, sMessage, EventQueue.EventLicence);
                    await eventManager.SaveEvents();
                }
            }
        }


        return sMessage;
    }

    private static bool UpdatedLessThan24HoursAgo(ApplicationData appl)
    {
        var dateDiff = DateTime.Now - appl.Appl_LastUpdate_Dte;
        if ((dateDiff.HasValue) && (dateDiff.Value.TotalHours < 24))
            return true;
        else
            return false;
    }

    private async Task<int> GetCRA_EISO_amount(string previousConfirmedSIN)
    {
        int CRA_EISO_amount = 0;
        var data = await DB.InterceptionTable.GetEISOHistoryBySIN(previousConfirmedSIN);
        if ((data != null) && (data.Any()))
            if (int.TryParse(data.First().TRANS_AMT, out int transAmount))
                CRA_EISO_amount = transAmount;
        return CRA_EISO_amount;
    }

    private async Task CreateDataModificationEvent(ApplicationData application)
    {
        var events = await DB.ApplicationEventTable.GetApplicationEvents(application.Appl_EnfSrv_Cd, application.Appl_CtrlCd, EventQueue.EventSubm, activeState: "A");
        if (!events.ContainsEventCode(EventCode.C50782_APPLICATION_UPDATED_VIA_DATA_MAINTENANCE))
        {
            var eventManager = new ApplicationEventManager(application, DB);
            eventManager.AddSubmEvent(EventCode.C50782_APPLICATION_UPDATED_VIA_DATA_MAINTENANCE);
            await eventManager.SaveEvents();
        }
    }
}
