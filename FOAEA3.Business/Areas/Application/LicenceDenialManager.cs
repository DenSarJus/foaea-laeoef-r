using FOAEA3.Business.Security;
using System.Security.Claims;

namespace FOAEA3.Business.Areas.Application;

internal partial class LicenceDenialManager : ApplicationManager
{
    public LicenceDenialApplicationData LicenceDenialApplication { get; private set; }

    public LicenceDenialValidation LicenceDenialValidation { get; private set; }

    private bool AffidavitExists() => !String.IsNullOrEmpty(LicenceDenialApplication.Appl_Crdtr_FrstNme);

    public LicenceDenialManager(IRepositories repositories, IFoaeaConfigurationHelper config, ClaimsPrincipal user) :
       this(new LicenceDenialApplicationData(), repositories, config, user)
    {

    }

    public LicenceDenialManager(LicenceDenialApplicationData licenceDenial, IRepositories repositories, IFoaeaConfigurationHelper config, ClaimsPrincipal user) :
        base(licenceDenial, repositories, config, user, new LicenceDenialValidation(licenceDenial, repositories, config, null))
    {
        SetupLicenceDenial(licenceDenial);
    }

    public LicenceDenialManager(IRepositories repositories, IFoaeaConfigurationHelper config, FoaeaUser user) :
       this(new LicenceDenialApplicationData(), repositories, config, user)
    {

    }

    public LicenceDenialManager(LicenceDenialApplicationData licenceDenial, IRepositories repositories, IFoaeaConfigurationHelper config, FoaeaUser user) :
        base(licenceDenial, repositories, config, user, new LicenceDenialValidation(licenceDenial, repositories, config, null))
    {
        SetupLicenceDenial(licenceDenial);
    }

    private void SetupLicenceDenial(LicenceDenialApplicationData licenceDenial)
    {
        LicenceDenialApplication = licenceDenial;

        // add Licence Denial specific state changes
        StateEngine.ValidStateChange.Add(ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7, new List<ApplicationState> {
                        ApplicationState.SIN_CONFIRMED_4,
                        ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7,
                        ApplicationState.APPLICATION_REJECTED_9,
                        ApplicationState.APPLICATION_ACCEPTED_10,
                        ApplicationState.MANUALLY_TERMINATED_14
                    });

        StateEngine.ValidStateChange[ApplicationState.SIN_CONFIRMED_4].Add(ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7);

        if (Validation is LicenceDenialValidation)
            LicenceDenialValidation = (Validation as LicenceDenialValidation);
    }

    public async Task<List<LicenceDenialOutgoingProvincialData>> GetProvincialOutgoingData(int maxRecords, string activeState, string recipientCode, bool isXML)
    {
        var licenceDenialDB = DB.LicenceDenialTable;
        var data = await licenceDenialDB.GetProvincialOutgoingData(maxRecords, activeState, recipientCode, isXML);
        return data;
    }

    public override async Task<bool> LoadApplication(string enfService, string controlCode)
    {
        // get data from Appl
        bool isSuccess = await base.LoadApplication(enfService, controlCode);

        if (isSuccess)
        {
            // get additional data from LicSusp table 
            var licenceDenialDB = DB.LicenceDenialTable;
            var data = await licenceDenialDB.GetLicenceDenialData(enfService, appl_L01_CtrlCd: controlCode);

            if (data != null)
                LicenceDenialApplication.Merge(data);
        }

        return isSuccess;
    }

    public override async Task<bool> CreateApplication()
    {
        if (!IsValidCategory("L01"))
            return false;

        if (LicenceDenialApplication.Medium_Cd == "FTP")
        {
            if (ValidateDeclaration())
                LicenceDenialApplication.LicSusp_Declaration_Ind = true;
            else
                return false;
        }
        else if (LicenceDenialApplication.LicSusp_Declaration_Ind is null ||
                 !LicenceDenialApplication.LicSusp_Declaration_Ind.Value)
            return false;

        if (string.IsNullOrEmpty(LicenceDenialApplication.Appl_Dbtr_LngCd))
            LicenceDenialApplication.Appl_Dbtr_LngCd = "E";

        bool success = await ValidateOrderOrProvisionInDefault();

        if (success)
            success = await base.CreateApplication();
        else
            success = false;

        if (success)
        {
            LicenceDenialApplication.LicSusp_LiStCd = 2;
            await DB.LicenceDenialTable.CreateLicenceDenialData(LicenceDenialApplication);
        }
        else
        {
            var failedSubmitterManager = new FailedSubmitAuditManager(DB, LicenceDenialApplication);
            await failedSubmitterManager.AddToFailedSubmitAudit(FailedSubmitActivityAreaType.L01);
        }

        return success;
    }

    protected override void TrimSpaces()
    {
        base.TrimSpaces();

        LicenceDenialApplication.LicSusp_CourtNme = LicenceDenialApplication.LicSusp_CourtNme?.Trim();
        LicenceDenialApplication.PymPr_Cd = LicenceDenialApplication.PymPr_Cd?.Trim();
        LicenceDenialApplication.LicSusp_Dbtr_EmplNme = LicenceDenialApplication.LicSusp_Dbtr_EmplNme?.Trim();
        LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_Ln = LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_Ln?.Trim();
        LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_Ln1 = LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_Ln1?.Trim();
        LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_CityNme = LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_CityNme?.Trim();
        LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_PrvCd = LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_PrvCd?.Trim();
        LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_CtryCd = LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_CtryCd?.Trim();
        LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_PCd = LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_PCd?.Trim();
        LicenceDenialApplication.LicSusp_Dbtr_EyesColorCd = LicenceDenialApplication.LicSusp_Dbtr_EyesColorCd?.Trim();
        LicenceDenialApplication.LicSusp_Dbtr_HeightUOMCd = LicenceDenialApplication.LicSusp_Dbtr_HeightUOMCd?.Trim();
        LicenceDenialApplication.LicSusp_Dbtr_Brth_CityNme = LicenceDenialApplication.LicSusp_Dbtr_Brth_CityNme?.Trim();
        LicenceDenialApplication.LicSusp_Dbtr_Brth_CtryCd = LicenceDenialApplication.LicSusp_Dbtr_Brth_CtryCd?.Trim();
    }

    public override void MakeUpperCase()
    {
        base.MakeUpperCase();

        LicenceDenialApplication.LicSusp_CourtNme = LicenceDenialApplication.LicSusp_CourtNme?.ToUpper();
        LicenceDenialApplication.PymPr_Cd = LicenceDenialApplication.PymPr_Cd?.ToUpper();
        LicenceDenialApplication.LicSusp_Dbtr_EmplNme = LicenceDenialApplication.LicSusp_Dbtr_EmplNme?.ToUpper();
        LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_Ln = LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_Ln?.ToUpper();
        LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_Ln1 = LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_Ln1?.ToUpper();
        LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_CityNme = LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_CityNme?.ToUpper();
        LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_PrvCd = LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_PrvCd?.ToUpper();
        LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_CtryCd = LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_CtryCd?.ToUpper();
        LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_PCd = LicenceDenialApplication.LicSusp_Dbtr_EmplAddr_PCd?.ToUpper();
        LicenceDenialApplication.LicSusp_Dbtr_EyesColorCd = LicenceDenialApplication.LicSusp_Dbtr_EyesColorCd?.ToUpper();
        LicenceDenialApplication.LicSusp_Dbtr_HeightUOMCd = LicenceDenialApplication.LicSusp_Dbtr_HeightUOMCd?.ToUpper();
        LicenceDenialApplication.LicSusp_Dbtr_Brth_CityNme = LicenceDenialApplication.LicSusp_Dbtr_Brth_CityNme?.ToUpper();
        LicenceDenialApplication.LicSusp_Dbtr_Brth_CtryCd = LicenceDenialApplication.LicSusp_Dbtr_Brth_CtryCd?.ToUpper();
    }

    private async Task<bool> ValidateOrderOrProvisionInDefault()
    {
        bool result = true;

        var app = LicenceDenialApplication;

        if (!await IsValidPaymentPeriod(app.PymPr_Cd))
        {
            app.Messages.AddError($"Invalid PymPr_Cd: {app.PymPr_Cd}");
            result = false;
        }

        if ((app.LicSusp_NrOfPymntsInDefault is null || app.LicSusp_NrOfPymntsInDefault < 3) &&
            (app.LicSusp_AmntOfArrears is null || app.LicSusp_AmntOfArrears < 3000M))
        {
            app.Messages.AddError("Number of payments less than 3 and amount of arrears < 3000.00");
            result = false;
        }

        return result;
    }

    private async Task<bool> IsValidPaymentPeriod(string paymentPeriodCode)
    {
        var paymentPeriods = await DB.InterceptionTable.GetPaymentPeriods();
        return paymentPeriods.Any(m => (m.PymPr_Cd == paymentPeriodCode.ToUpper()) && (m.ActvSt_Cd == "A"));
    }

    private bool ValidateDeclaration()
    {
        string declaration = LicenceDenialApplication.LicSusp_Declaration?.Trim();
        if (declaration is not null &&
            (declaration.Equals(Config.LicenceDenialDeclaration.English, StringComparison.InvariantCultureIgnoreCase) ||
             declaration.Equals(Config.LicenceDenialDeclaration.French, StringComparison.InvariantCultureIgnoreCase)))
            return true;
        else
        {
            LicenceDenialApplication.Messages.AddError("Invalid or missing declaration.");
            return false;
        }
    }

    public async Task<List<LicenceSuspensionHistoryData>> GetLicenceSuspensionHistory()
    {
        return await DB.LicenceDenialTable.GetLicenceSuspensionHistory(LicenceDenialApplication.Appl_EnfSrv_Cd, LicenceDenialApplication.Appl_CtrlCd);
    }

    public async Task CancelApplication()
    {
        LicenceDenialApplication.Appl_LastUpdate_Dte = DateTime.Now;
        LicenceDenialApplication.Appl_LastUpdate_Usr = DB.CurrentSubmitter;

        await SetNewStateTo(ApplicationState.MANUALLY_TERMINATED_14);

        MakeUpperCase();
        await UpdateApplicationNoValidation();
    }

    public override async Task UpdateApplication()
    {
        var current = new LicenceDenialManager(DB, Config, CurrentUser);
        await current.LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd);

        // keep these stored values
        LicenceDenialApplication.Appl_Create_Dte = current.LicenceDenialApplication.Appl_Create_Dte;
        LicenceDenialApplication.Appl_Create_Usr = current.LicenceDenialApplication.Appl_Create_Usr;

        bool success = await ValidateOrderOrProvisionInDefault();

        if (success)
        {
            if ((LicenceDenialApplication.ActvSt_Cd == "A") &&
                (LicenceDenialApplication.AppLiSt_Cd.In(ApplicationState.APPLICATION_ACCEPTED_10,
                                                        ApplicationState.PARTIALLY_SERVICED_12)))
            {
                if (AppChanged(current.LicenceDenialApplication))
                    await CreateLicenseEventForActiveL01DataChanged("Debtor Info Updated");
            }

            await base.UpdateApplication();
        }
    }

    private bool AppChanged(LicenceDenialApplicationData currentAppl)
    {
        bool wasChanged = false;
        var newAppl = LicenceDenialApplication;

        if (!currentAppl.Appl_Dbtr_Addr_Ln.Trim().Equals(newAppl.Appl_Dbtr_Addr_Ln.Trim(), StringComparison.InvariantCultureIgnoreCase))
            wasChanged = true;

        if (!currentAppl.Appl_Dbtr_Addr_Ln1.Trim().Equals(newAppl.Appl_Dbtr_Addr_Ln1.Trim(), StringComparison.InvariantCultureIgnoreCase))
            wasChanged = true;

        if (!currentAppl.Appl_Dbtr_Addr_CityNme.Trim().Equals(newAppl.Appl_Dbtr_Addr_CityNme.Trim(), StringComparison.InvariantCultureIgnoreCase))
            wasChanged = true;

        if (!currentAppl.Appl_Dbtr_Addr_PrvCd.Trim().Equals(newAppl.Appl_Dbtr_Addr_PrvCd.Trim(), StringComparison.InvariantCultureIgnoreCase))
            wasChanged = true;

        if (!currentAppl.Appl_Dbtr_Addr_CtryCd.Trim().Equals(newAppl.Appl_Dbtr_Addr_CtryCd.Trim(), StringComparison.InvariantCultureIgnoreCase))
            wasChanged = true;

        if (!currentAppl.Appl_Dbtr_Addr_PCd.Trim().Equals(newAppl.Appl_Dbtr_Addr_PCd.Trim(), StringComparison.InvariantCultureIgnoreCase))
            wasChanged = true;

        return wasChanged;
    }

    public async Task<bool> ProcessLicenceDenialResponse(string appl_EnfSrv_Cd, string appl_CtrlCd)
    {
        if (!await LoadApplication(appl_EnfSrv_Cd, appl_CtrlCd))
        {
            LicenceDenialApplication.Messages.AddError(SystemMessage.APPLICATION_NOT_FOUND);
            return false;
        }

        if (!IsValidCategory("L01"))
            return false;

        if (LicenceDenialApplication.AppLiSt_Cd.NotIn(ApplicationState.APPLICATION_ACCEPTED_10, ApplicationState.PARTIALLY_SERVICED_12))
        {
            LicenceDenialApplication.Messages.AddError("Invalid State for the current application.  Valid states allowed are 10 and 12.");
            return false;
        }

        LicenceDenialApplication.Appl_LastUpdate_Dte = DateTime.Now;
        LicenceDenialApplication.Appl_LastUpdate_Usr = DB.CurrentSubmitter;

        await SetNewStateTo(ApplicationState.PARTIALLY_SERVICED_12);

        await UpdateApplicationNoValidation();

        await DB.LicenceDenialTable.UpdateLicenceDenialData(LicenceDenialApplication);

        await EventManager.SaveEvents();

        return true;
    }

    public async Task<ApplicationEventsList> GetRequestedLICINLicenceDenialEvents(string enfSrv_Cd, string appl_EnfSrv_Cd,
                                                                           string appl_CtrlCd)
    {
        return await EventManager.GetRequestedLICINLicenceDenialEvents(enfSrv_Cd, appl_EnfSrv_Cd, appl_CtrlCd);
    }

    public async Task<ApplicationEventDetailsList> GetRequestedLICINLicenceDenialEventDetails(string enfSrv_Cd, string appl_EnfSrv_Cd,
                                                                           string appl_CtrlCd)
    {
        return await EventDetailManager.GetRequestedLICINLicenceDenialEventDetails(enfSrv_Cd, appl_EnfSrv_Cd, appl_CtrlCd);
    }

    public async Task<List<LicenceDenialOutgoingFederalData>> GetFederalOutgoingData(int maxRecords,
                                                                                     string activeState,
                                                                                     ApplicationState lifeState,
                                                                                     string enfServiceCode)
    {
        var licenceDenialDB = DB.LicenceDenialTable;
        return await licenceDenialDB.GetFederalOutgoingData(maxRecords, activeState, lifeState, enfServiceCode);
    }

    public async Task CreateResponseData(List<LicenceDenialResponseData> responseData)
    {
        var responsesDB = DB.LicenceDenialResponseTable;
        await responsesDB.InsertBulkData(responseData);
    }

    public async Task MarkResponsesAsViewed(string enfService)
    {
        var responsesDB = DB.LicenceDenialResponseTable;
        await responsesDB.MarkResponsesAsViewed(enfService);
    }

    public async Task CreateLicenseEventForActiveL01DataChanged(string sMessage)
    {
        if (!await ActiveDataMaintenanceLicenseEventForApplication())
        {
            EventManager.AddEvent(EventCode.C50529_APPLICATION_UPDATED_SUCCESSFULLY, sMessage, EventQueue.EventLicence);
            await EventManager.SaveEvents();
        }
    }

    public async Task<bool> ActiveDataMaintenanceLicenseEventForApplication()
    {
        var events = await EventManager.GetApplicationEventsForQueue(EventQueue.EventLicence);

        var activeEvents = events.Where(m => m.ActvSt_Cd == "A" &&
                                        m.Event_Reas_Cd == EventCode.C50529_APPLICATION_UPDATED_SUCCESSFULLY &&
                                        !m.Event_Compl_Dte.HasValue).ToList();

        return activeEvents.Count != 0;
    }

    public override async Task ProcessBringForwards(ApplicationEventData bfEvent)
    {
        bool closeEvent = false;

        TimeSpan diff;
        if (LicenceDenialApplication.Appl_LastUpdate_Dte.HasValue)
            diff = LicenceDenialApplication.Appl_LastUpdate_Dte.Value - DateTime.Now;
        else
            diff = TimeSpan.Zero;

        if ((LicenceDenialApplication.ActvSt_Cd != "A") &&
            ((!bfEvent.Event_Reas_Cd.HasValue) || (
             (bfEvent.Event_Reas_Cd.NotIn(EventCode.C50806_SCHEDULED_TO_BE_REINSTATED__QUARTERLY_TRACING,
                                          EventCode.C50680_CHANGE_OR_SUPPLY_ADDITIONAL_DEBTOR_INFORMATION_SEE_SIN_VERIFICATION_RESULTS_PAGE_IN_FOAEA_FOR_SPECIFIC_DETAILS,
                                          EventCode.C50600_INVALID_APPLICATION)))) &&
            ((diff.Equals(TimeSpan.Zero)) || (Math.Abs(diff.TotalHours) > 24)))
        {
            bfEvent.AppLiSt_Cd = ApplicationState.MANUALLY_TERMINATED_14;
            bfEvent.ActvSt_Cd = "I";

            await EventManager.SaveEvent(bfEvent);

            closeEvent = false;
        }
        else
        {
            if (bfEvent.Event_Reas_Cd.HasValue)
                switch (bfEvent.Event_Reas_Cd)
                {
                    case EventCode.C50528_BF_10_DAYS_FROM_RECEIPT_OF_APPLICATION:
                        if (LicenceDenialApplication.AppLiSt_Cd >= ApplicationState.APPLICATION_REJECTED_9)
                        {
                            // no action required
                        }
                        else
                        {
                            var DaysElapsed = (DateTime.Now - LicenceDenialApplication.Appl_Lgl_Dte).TotalDays;
                            if (LicenceDenialApplication.AppLiSt_Cd.In(ApplicationState.INVALID_APPLICATION_1, ApplicationState.SIN_NOT_CONFIRMED_5) &&
                               (Math.Abs(DaysElapsed) > 40))
                            {
                                // Reject the application according to the Application category code
                                // .RejectT01(app.Appl.Item(0).Appl_EnfSrv_Cd, app.Appl.Item(0).Appl_CtrlCd, Appl_LastUpdate_Subm)
                                EventManager.AddEvent(EventCode.C50760_APPLICATION_REJECTED_AS_CONDITIONS_NOT_MET_IN_TIMEFRAME);
                            }
                            else if ((LicenceDenialApplication.AppLiSt_Cd < ApplicationState.APPLICATION_REJECTED_9)
                                && (DaysElapsed > 90))
                            {
                                // Reject the application according to the Application category code
                                EventManager.AddEvent(EventCode.C50760_APPLICATION_REJECTED_AS_CONDITIONS_NOT_MET_IN_TIMEFRAME);
                            }
                            else
                            {
                                if (LicenceDenialApplication.AppLiSt_Cd.In(ApplicationState.INVALID_APPLICATION_1, ApplicationState.SIN_NOT_CONFIRMED_5))
                                {
                                    //Event_Reas_Cd = 50902 'Awaiting an action on this application
                                    //dteDateForNextBF = DateAdd(DateInterval.Day, 10, dteDateForNextBF)
                                }
                                else if (LicenceDenialApplication.AppLiSt_Cd.In(ApplicationState.PENDING_ACCEPTANCE_SWEARING_6, ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7))
                                {
                                    //Select Case Current_LifeState
                                    //    Case 6
                                    //        Event_Reas_Cd = 51042 'Requires legal authorization
                                    //    Case 7
                                    //        Event_Reas_Cd = 54004 'Awaiting affidavit to be entered
                                    //End Select
                                    //dteDateForNextBF = DateAdd(DateInterval.Day, 20, dteDateForNextBF)
                                }
                                else
                                {
                                    //Event_Reas_Cd = 50902 'Awaiting an action on this application
                                    //dteDateForNextBF = DateAdd(DateInterval.Day, 10, dteDateForNextBF)
                                    //.CreateEvent("EvntAm", Subm_SubmCd, Appl_EnfSrv_Cd, Appl_CtrlCd, Now, Subm_Recpt_SubmCd, #12:00:00 AM#, 51100, "", "N", #12:00:00 AM#, "A", AppList_Cd, "")
                                }
                                //.CreateEvent("EvntSubm", Subm_SubmCd, Appl_EnfSrv_Cd, Appl_CtrlCd, Now, Subm_Recpt_SubmCd, #12:00:00 AM#, Event_Reas_Cd, "", "N", #12:00:00 AM#, "A", AppList_Cd, Appl_LastUpdate_Subm)
                                //.CreateEvent("EvntBF", Subm_SubmCd, Appl_EnfSrv_Cd, Appl_CtrlCd, Now, Subm_Recpt_SubmCd, #12:00:00 AM#, 50528, "", "N", dteDateForNextBF, "A", AppList_Cd, "")

                            }
                        }
                        break;

                    case EventCode.C54001_BF_EVENT:
                    case EventCode.C54002_TELEPHONE_EVENT:
                        EventManager.AddEvent(bfEvent.Event_Reas_Cd.Value);
                        break;

                    default:
                        EventManager.AddEvent(EventCode.C54003_UNKNOWN_EVNTBF, queue: EventQueue.EventSYS);
                        break;
                }
        }

        if (closeEvent)
        {
            bfEvent.AppLiSt_Cd = LicenceDenialApplication.AppLiSt_Cd;
            bfEvent.ActvSt_Cd = "C";

            await EventManager.SaveEvent(bfEvent);
        }

        await EventManager.SaveEvents();
    }

    public async Task<List<LicenceDenialToApplData>> GetLicenceDenialToApplData(string federalSource)
    {
        var licenceDenialDB = DB.LicenceDenialTable;
        return await licenceDenialDB.GetLicenceDenialToApplData(federalSource);
    }
}
