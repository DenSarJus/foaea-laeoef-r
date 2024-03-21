using FOAEA3.Business.Security;
using FOAEA3.Model.Base;
using System.Security.Claims;

namespace FOAEA3.Business.Areas.Application;

internal partial class TracingManager : ApplicationManager
{
    public TracingApplicationData TracingApplication { get; private set; }
    public TracingValidation TracingValidation { get; private set; }
    private DateTime ReinstateEffectiveDate { get; set; }
    private EventCode BFEventReasonCode { get; set; }
    private int BFEvent_Id { get; set; }
    private FederalSource FedSource { get; set; } = FederalSource.Unknown;

    public TracingManager(TracingApplicationData tracing, IRepositories repositories, IFoaeaConfigurationHelper config, ClaimsPrincipal user) :
        base(tracing, repositories, config, user, new TracingValidation(tracing, repositories, config, null))
    {
        SetupTracingManager(tracing);
    }

    public TracingManager(TracingApplicationData tracing, IRepositories repositories, IFoaeaConfigurationHelper config, FoaeaUser user) :
        base(tracing, repositories, config, user, new TracingValidation(tracing, repositories, config, null))
    {
        SetupTracingManager(tracing);
    }

    public TracingManager(IRepositories repositories, IFoaeaConfigurationHelper config, ClaimsPrincipal user) :
        this(new TracingApplicationData(), repositories, config, user)
    {

    }

    public TracingManager(IRepositories repositories, IFoaeaConfigurationHelper config, FoaeaUser user) :
        this(new TracingApplicationData(), repositories, config, user)
    {

    }

    private void SetupTracingManager(TracingApplicationData tracing)
    {
        TracingApplication = tracing;

        // add Tracing specific valid state changes
        StateEngine.ValidStateChange.Add(ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7,
                                            new List<ApplicationState> {
                                                ApplicationState.SIN_CONFIRMED_4,
                                                ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7,
                                                ApplicationState.APPLICATION_REJECTED_9,
                                                ApplicationState.APPLICATION_ACCEPTED_10,
                                                ApplicationState.MANUALLY_TERMINATED_14
                                            });
        StateEngine.ValidStateChange.Add(ApplicationState.APPLICATION_REINSTATED_11,
                                            new List<ApplicationState> {
                                                ApplicationState.APPLICATION_ACCEPTED_10,
                                                ApplicationState.MANUALLY_TERMINATED_14,
                                                ApplicationState.EXPIRED_15
                                            });
        StateEngine.ValidStateChange[ApplicationState.APPLICATION_ACCEPTED_10].Add(ApplicationState.APPLICATION_REINSTATED_11);
        StateEngine.ValidStateChange[ApplicationState.PARTIALLY_SERVICED_12].Add(ApplicationState.APPLICATION_REINSTATED_11);
        StateEngine.ValidStateChange[ApplicationState.PENDING_ACCEPTANCE_SWEARING_6].Add(ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7);
        StateEngine.ValidStateChange[ApplicationState.SIN_CONFIRMED_4].Add(ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7);

        if (Validation is TracingValidation)
            TracingValidation = (Validation as TracingValidation);
    }

    public override async Task<bool> LoadApplication(string enfService, string controlCode)
    {
        // get data from Appl
        bool isSuccess = await base.LoadApplication(enfService, controlCode);

        await TracingValidation.GetC78infoFromEnfSrv();

        if (isSuccess)
        {
            // get additional data from Trace table, if available
            TracingApplicationData data = await DB.TracingTable.GetTracingData(enfService, controlCode);

            if (data != null)
                TracingApplication.Merge(data);
        }

        if (!isSuccess)
        {
            // add to audit
        }

        return isSuccess;
    }

    public override async Task<bool> CreateApplication()
    {
        if (!IsValidCategory("T01"))
            return false;

        await TracingValidation.GetC78infoFromEnfSrv();

        IsAddressMandatory = false;
        bool success = await base.CreateApplication();

        if (!success)
        {
            var failedSubmitterManager = new FailedSubmitAuditManager(DB, TracingApplication);
            await failedSubmitterManager.AddToFailedSubmitAudit(FailedSubmitActivityAreaType.T01);
        }
        else
        {
            if (TracingValidation.IsC78() || TracingApplication.Medium_Cd == "FTP")
                await DB.TracingTable.CreateTracingData(TracingApplication);
        }

        return success;

    }

    public override async Task UpdateApplication()
    {
        TracingApplication.Appl_LastUpdate_Usr = CurrentUser.Submitter.Subm_SubmCd;
        TracingApplication.Appl_LastUpdate_Dte = DateTime.Now;

        IsAddressMandatory = false;

        await TracingValidation.GetC78infoFromEnfSrv();

        if (TracingValidation.IsC78())
        {
            await DB.TracingTable.UpdateTracingData(TracingApplication);

            MakeUpperCase();

            var affidavitDate = TracingApplication.Appl_RecvAffdvt_Dte;
            var affidavitSubm = TracingApplication.Subm_Affdvt_SubmCd;

            if (TracingApplication.Subm_SubmCd.IsPeaceOfficerSubmitter())
            {
                if ((TracingApplication.AppLiSt_Cd == ApplicationState.PENDING_ACCEPTANCE_SWEARING_6) &&
                    !string.IsNullOrEmpty(TracingApplication.Subm_Affdvt_SubmCd))
                {
                    await SetNewStateTo(ApplicationState.APPLICATION_ACCEPTED_10);
                }
            }

            await base.UpdateApplication();

            if ((!TracingApplication.Messages.ContainsMessagesOfType(MessageType.Error)) &&
                TracingApplication.Subm_SubmCd.IsPeaceOfficerSubmitter())
            {
                if ((TracingApplication.AppLiSt_Cd == ApplicationState.SIN_CONFIRMATION_PENDING_3) &&
                    !string.IsNullOrEmpty(affidavitSubm))
                {
                    TracingApplication.Appl_RecvAffdvt_Dte = affidavitDate;
                    TracingApplication.Subm_Affdvt_SubmCd = affidavitSubm;

                    await UpdateApplicationNoValidation();
                }
            }
        }
        else
        {
            var currentDataManager = new TracingManager(DB, Config, CurrentUser);
            await currentDataManager.LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd);
            var currentApplication = currentDataManager.TracingApplication;

            bool isReset = currentApplication.AppLiSt_Cd.In(ApplicationState.INVALID_APPLICATION_1,
                                                            ApplicationState.SIN_NOT_CONFIRMED_5);
            bool isCancelled = TracingApplication.AppLiSt_Cd == ApplicationState.MANUALLY_TERMINATED_14;
            bool hasStateChanged = currentDataManager.TracingApplication.AppLiSt_Cd != TracingApplication.AppLiSt_Cd;

            if ((hasStateChanged && isCancelled) || isReset)
            {
                MakeUpperCase();
                await base.UpdateApplication();
            }
            else if (!string.IsNullOrEmpty(TracingApplication.FamPro_Cd))
            {
                // affidavit data has been passed -- need to either create it or update it
                if (!await TraceDataExists())
                {
                    await ValidateAndCreateTraceData();

                    string newAppl_Crdtr_FrstNme = TracingApplication.Appl_Crdtr_FrstNme;
                    string newAppl_Crdtr_MddleNme = TracingApplication.Appl_Crdtr_MddleNme;
                    string newAppl_Crdtr_SurNme = TracingApplication.Appl_Crdtr_SurNme;
                    string newLastUpdateUser = TracingApplication.Appl_LastUpdate_Usr;
                    DateTime? newLastUpdateDate = TracingApplication.Appl_LastUpdate_Dte;

                    DateTime? newAffdvtDate = TracingApplication.Appl_RecvAffdvt_Dte;
                    string newAffdvtSubmitter = TracingApplication.Subm_Affdvt_SubmCd;

                    await LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd);

                    if (CurrentUser.Submitter.Subm_SubmCd.IsInternalAgentSubmitter())
                    {
                        TracingApplication.Appl_RecvAffdvt_Dte = newAffdvtDate;
                        TracingApplication.Subm_Affdvt_SubmCd = newAffdvtSubmitter;

                        if (TracingApplication.AppLiSt_Cd.In(ApplicationState.PENDING_ACCEPTANCE_SWEARING_6,
                                                             ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7))
                            await SetNewStateTo(ApplicationState.APPLICATION_ACCEPTED_10);
                    }

                    TracingApplication.Appl_Crdtr_FrstNme = newAppl_Crdtr_FrstNme;
                    TracingApplication.Appl_Crdtr_MddleNme = newAppl_Crdtr_MddleNme;
                    TracingApplication.Appl_Crdtr_SurNme = newAppl_Crdtr_SurNme;

                    TracingApplication.Appl_LastUpdate_Usr = newLastUpdateUser;
                    TracingApplication.Appl_LastUpdate_Dte = newLastUpdateDate;

                    if (TracingApplication.AppLiSt_Cd == ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7)
                        TracingApplication.AppLiSt_Cd = ApplicationState.PENDING_ACCEPTANCE_SWEARING_6;

                    MakeUpperCase();

                    if (TracingApplication.Medium_Cd != "FTP") TracingApplication.Messages.AddInformation($"{LanguageResource.APPLICATION_REFERENCE_NUMBER}: {Application.Appl_EnfSrv_Cd}-{Application.Subm_SubmCd}-{Application.Appl_CtrlCd}");
                    if (TracingApplication.Medium_Cd != "FTP") TracingApplication.Messages.AddInformation(ReferenceData.Instance().ApplicationLifeStates[Application.AppLiSt_Cd].Description);

                    await base.UpdateApplicationNoValidation();
                }
                else
                {
                    await DB.TracingTable.UpdateTracingData(TracingApplication);

                    MakeUpperCase();

                    if (Application.Medium_Cd != "FTP") TracingApplication.Messages.AddInformation($"{LanguageResource.APPLICATION_REFERENCE_NUMBER}: {Application.Appl_EnfSrv_Cd}-{Application.Subm_SubmCd}-{Application.Appl_CtrlCd}");
                    if (TracingApplication.Medium_Cd != "FTP") TracingApplication.Messages.AddInformation(ReferenceData.Instance().ApplicationLifeStates[Application.AppLiSt_Cd].Description);

                    await base.UpdateApplicationNoValidation();
                }

            }
            else
            {
                await DB.TracingTable.CreateTracingData(TracingApplication);

                MakeUpperCase();

                await base.UpdateApplicationNoValidation();
            }
        }
    }

    public async Task<bool> ReinstateApplication(string enfService, string controlCode, string lastUpdateUser,
                                     DateTime eventEffectiveDate, EventCode bfEventCode, int bfEventId)
    {
        ReinstateEffectiveDate = eventEffectiveDate;
        BFEventReasonCode = bfEventCode;
        BFEvent_Id = bfEventId;

        await LoadApplication(enfService, controlCode);

        TracingApplication.Appl_LastUpdate_Usr = lastUpdateUser;
        TracingApplication.Appl_LastUpdate_Dte = DateTime.Now;

        if (!IsValidCategory("T01"))
            return false;

        TracingApplication.Appl_Reactv_Dte = ReinstateEffectiveDate;

        int traceCycle = TracingApplication.Trace_Cycl_Qty;

        await SetNewStateTo(ApplicationState.APPLICATION_REINSTATED_11);

        TracingApplication.Trace_Cycl_Qty = traceCycle + 1;
        TracingApplication.Trace_LiSt_Cd = 80;
        TracingApplication.Trace_LstCyclStr_Dte = DateTime.Now;

        MakeUpperCase();

        await UpdateApplication();

        return true;
    }

    public async Task<List<TracingApplicationData>> GetTracingApplicationsWaitingAtState6()
    {
        var applications = await DB.ApplicationTable.GetApplicationsForCategoryAndLifeState("T01",
                                                                    ApplicationState.PENDING_ACCEPTANCE_SWEARING_6);
        var result = new List<TracingApplicationData>();
        foreach (var appl in applications)
        {
            var thisTracingAppl = new TracingApplicationData();
            thisTracingAppl.Merge(appl);

            var data = await DB.TracingTable.GetTracingData(appl.Appl_EnfSrv_Cd, appl.Appl_CtrlCd);

            if (data != null)
                thisTracingAppl.Merge(data);

            result.Add(thisTracingAppl);
        }

        return result;
    }

    public async Task AcceptApplication()
    {
        await SetNewStateTo(ApplicationState.APPLICATION_ACCEPTED_10);

        await UpdateApplicationNoValidation();

        await EventManager.SaveEvents();
    }

    public async Task<bool> RejectApplication(string enfService, string controlCode, string lastUpdateUser)
    {
        bool result = true;

        await LoadApplication(enfService, controlCode);

        TracingApplication.Appl_LastUpdate_Usr = lastUpdateUser;
        TracingApplication.Appl_LastUpdate_Dte = DateTime.Now;

        if (!IsValidCategory("T01"))
            return false;

        await SetNewStateTo(ApplicationState.APPLICATION_REJECTED_9);

        await UpdateApplication();

        return result;
    }

    public async Task<bool> CertifyAffidavit(string lastUpdateUser)
    {
        TracingApplication.Appl_LastUpdate_Dte = DateTime.Now;
        TracingApplication.Appl_LastUpdate_Usr = lastUpdateUser;

        if (TracingApplication.AppLiSt_Cd != ApplicationState.PENDING_ACCEPTANCE_SWEARING_6)
        {
            TracingApplication.Messages.AddError($"CertifyL02 is not available for state {TracingApplication.AppLiSt_Cd}.  It should be from state 6 (legal authorization pending) only.");
            return false;
        }

        if (!lastUpdateUser.IsInternalAgentSubmitter())
        {
            if (string.IsNullOrEmpty(TracingApplication.Subm_Affdvt_SubmCd))
            {
                TracingApplication.Subm_Affdvt_SubmCd = lastUpdateUser;
                TracingApplication.Appl_RecvAffdvt_Dte = DateTime.Now;
            }

            await SetNewStateTo(ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7);

            MakeUpperCase();
            await UpdateApplicationNoValidation();

            await EventManager.SaveEvents();

            if (TracingApplication.Medium_Cd != "FTP")
                TracingApplication.Messages.AddInformation(EventCode.C50620_VALID_APPLICATION);
        }
        else
        {
            // TODO: send message back that this is not allowed?
        }

        return true;

    }

    public async Task RejectAffidavit(string lastUpdateUser)
    {
        TracingApplication.Appl_LastUpdate_Dte = DateTime.Now;
        TracingApplication.Appl_LastUpdate_Usr = lastUpdateUser;

        await SetNewStateTo(ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7);

        MakeUpperCase();
        await UpdateApplicationNoValidation();

        EventManager.AddEvent(EventCode.C51019_REJECTED_AFFIDAVIT);

        await EventManager.SaveEvents();

        TracingApplication.Messages.AddInformation(EventCode.C50763_AFFIDAVIT_REJECTED_BY_FOAEA);
    }

    public async Task PartiallyServiceApplication(FederalSource fedSource)
    {
        FedSource = fedSource;

        await SetNewStateTo(ApplicationState.PARTIALLY_SERVICED_12);

        MakeUpperCase();
        await UpdateApplication();
    }

    public async Task FullyServiceApplication(FederalSource fedSource)
    {
        FedSource = fedSource;

        await SetNewStateTo(ApplicationState.FULLY_SERVICED_13);

        MakeUpperCase();
        await UpdateApplication();
    }

    public async Task<List<TraceCycleQuantityData>> GetTraceCycleQuantityData(string enfSrv_Cd, string cycle)
    {
        var tracingDB = DB.TracingTable;
        return await tracingDB.GetTraceCycleQuantityData(enfSrv_Cd, cycle);
    }

    public async Task<List<TraceToApplData>> GetTraceToApplData()
    {
        var tracingDB = DB.TracingTable;
        return await tracingDB.GetTraceToApplData();
    }

    private async Task SetTracingForReinstate(DateTime traceDate, DateTime bfDate, EventCode eventReasonCode)
    {
        TracingApplication.AppLiSt_Cd = ApplicationState.APPLICATION_ACCEPTED_10;
        TracingApplication.Messages.AddInformation(EventCode.C50780_APPLICATION_ACCEPTED);

        EventManager.AddEvent(EventCode.C50780_APPLICATION_ACCEPTED, queue: EventQueue.EventTrace);
        EventManager.AddEvent(EventCode.C50780_APPLICATION_ACCEPTED);

        EventManager.AddEvent(eventReasonCode, appState: ApplicationState.APPLICATION_REINSTATED_11, effectiveDateTime: traceDate);

        //var eventBF = await EventManager.GetEventBF(TracingApplication.Subm_SubmCd, Appl_CtrlCd,
        //                                            EventCode.C50806_SCHEDULED_TO_BE_REINSTATED__QUARTERLY_TRACING, "A");
        //
        //if (eventBF is not null)
        //{
        //    var firstEventBF = eventBF.First();
        //    firstEventBF.Event_Effctv_Dte = bfDate;
        //    EventManager.UpdateEvent(firstEventBF);
        //}
        //else
        EventManager.AddBFEvent(eventReasonCode, appState: ApplicationState.APPLICATION_REINSTATED_11, effectiveDateTime: bfDate);

        await Validation.AddDuplicateCreditorWarningEvents();
    }

    private async Task ValidateAndCreateTraceData()
    {
        if (!TracingApplication.Appl_LastUpdate_Usr.IsInternalAgentSubmitter())
        {
            TracingApplication.Subm_Affdvt_SubmCd = null;
        }

        if (TracingApplication.AppLiSt_Cd.In(ApplicationState.INVALID_APPLICATION_1, ApplicationState.SIN_CONFIRMATION_PENDING_3,
                                             ApplicationState.SIN_NOT_CONFIRMED_5, ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7))
        {
            TracingApplication.Trace_LiSt_Cd = 80;
            await DB.TracingTable.CreateTracingData(TracingApplication);

            if (TracingApplication.AppLiSt_Cd == ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7)
            {
                await Process_04_SinConfirmed();
            }
        }
    }

    public override async Task ProcessBringForwards(ApplicationEventData bfEvent)
    {
        bool closeEvent = false;

        TimeSpan diff = TimeSpan.Zero;
        if (TracingApplication.Appl_LastUpdate_Dte.HasValue)
            diff = TracingApplication.Appl_LastUpdate_Dte.Value - DateTime.Now;

        if ((TracingApplication.ActvSt_Cd != "A") &&
            ((!bfEvent.Event_Reas_Cd.HasValue) || (
             (bfEvent.Event_Reas_Cd.NotIn(EventCode.C50806_SCHEDULED_TO_BE_REINSTATED__QUARTERLY_TRACING,
                                          EventCode.C50680_CHANGE_OR_SUPPLY_ADDITIONAL_DEBTOR_INFORMATION_SEE_SIN_VERIFICATION_RESULTS_PAGE_IN_FOAEA_FOR_SPECIFIC_DETAILS,
                                          EventCode.C50600_INVALID_APPLICATION)))) &&
            ((diff.Equals(TimeSpan.Zero)) || (Math.Abs(diff.TotalHours) > 24)))
        {
            await EventManager.SaveEvent(bfEvent, ApplicationState.MANUALLY_TERMINATED_14, "I");
            closeEvent = false;
        }
        else
        {
            if (bfEvent.Event_Reas_Cd.HasValue)
            {
                var currentLifeState = TracingApplication.AppLiSt_Cd;

                switch (bfEvent.Event_Reas_Cd)
                {
                    case EventCode.C50528_BF_10_DAYS_FROM_RECEIPT_OF_APPLICATION:
                        if (currentLifeState < ApplicationState.APPLICATION_REJECTED_9)
                        {
                            var DaysElapsed = Math.Abs((DateTime.Now - TracingApplication.Appl_Lgl_Dte).TotalDays);

                            if (currentLifeState.In(ApplicationState.INVALID_APPLICATION_1, ApplicationState.SIN_NOT_CONFIRMED_5) &&
                               (DaysElapsed > 40))
                            {
                                await RejectApplication(Appl_EnfSrv_Cd, Appl_CtrlCd, DB.CurrentSubmitter);
                                EventManager.AddEvent(EventCode.C50760_APPLICATION_REJECTED_AS_CONDITIONS_NOT_MET_IN_TIMEFRAME);
                            }
                            else if (DaysElapsed > 90)
                            {
                                await RejectApplication(Appl_EnfSrv_Cd, Appl_CtrlCd, DB.CurrentSubmitter);
                                EventManager.AddEvent(EventCode.C50760_APPLICATION_REJECTED_AS_CONDITIONS_NOT_MET_IN_TIMEFRAME);
                            }
                            else
                            {
                                AddNewBFevent();
                            }
                        }
                        closeEvent = true;
                        break;

                    case EventCode.C50804_SCHEDULED_TO_BE_REINSTATED:
                        if (currentLifeState == ApplicationState.APPLICATION_REINSTATED_11)
                        {
                            await ReinstateApplication(Appl_EnfSrv_Cd, Appl_CtrlCd, DB.CurrentSubmitter,
                                                 bfEvent.Event_Effctv_Dte, bfEvent.Event_Reas_Cd.Value, bfEvent.Event_Id);
                            closeEvent = true;
                        }
                        break;

                    case EventCode.C50806_SCHEDULED_TO_BE_REINSTATED__QUARTERLY_TRACING:
                        if (currentLifeState == ApplicationState.PARTIALLY_SERVICED_12)
                        {
                            await ReinstateApplication(Appl_EnfSrv_Cd, Appl_CtrlCd, DB.CurrentSubmitter,
                                                       bfEvent.Event_Effctv_Dte, bfEvent.Event_Reas_Cd.Value, bfEvent.Event_Id);
                            TracingApplication.AppLiSt_Cd = ApplicationState.APPLICATION_REINSTATED_11; // to store correct value in event
                            closeEvent = true;
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
        }

        if (closeEvent)
        {
            await EventManager.SaveEvent(bfEvent, TracingApplication.AppLiSt_Cd, "C");
        }

        await EventManager.SaveEvents();
    }

    private void AddNewBFevent()
    {
        DateTime nextBFdate = DateTime.Now;
        EventCode eventReasonCode = EventCode.UNDEFINED;

        if (TracingApplication.AppLiSt_Cd.In(ApplicationState.INVALID_APPLICATION_1, ApplicationState.SIN_NOT_CONFIRMED_5))
        {
            eventReasonCode = EventCode.C50902_AWAITING_AN_ACTION_ON_THIS_APPLICATION;
            nextBFdate = nextBFdate.AddDays(10);
        }
        else if (TracingApplication.AppLiSt_Cd.In(ApplicationState.PENDING_ACCEPTANCE_SWEARING_6, ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7))
        {
            switch (TracingApplication.AppLiSt_Cd)
            {
                case ApplicationState.PENDING_ACCEPTANCE_SWEARING_6:
                    eventReasonCode = EventCode.C51042_REQUIRES_LEGAL_AUTHORIZATION;
                    break;
                case ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7:
                    eventReasonCode = EventCode.C54004_AWAITING_AFFIDAVIT_TO_BE_ENTERED;
                    break;
            }
            nextBFdate = nextBFdate.AddDays(20);
        }
        else
        {
            eventReasonCode = EventCode.C50902_AWAITING_AN_ACTION_ON_THIS_APPLICATION;
            nextBFdate = nextBFdate.AddDays(10);

            EventManager.AddEvent(EventCode.C51100_STATE_OUT_OF_SYNC, queue: EventQueue.EventAM);
        }

        EventManager.AddEvent(eventReasonCode);
        EventManager.AddBFEvent(EventCode.C50520_INVALID_STATUS_CODE, effectiveDateTime: nextBFdate);
    }

    public async Task<DataList<TracingApplicationData>> GetApplicationsWaitingForAffidavit()
    {
        return await DB.TracingTable.GetApplicationsWaitingForAffidavit();
    }

    private async Task<bool> TraceDataExists()
    {
        return await DB.TracingTable.TracingDataExists(Appl_EnfSrv_Cd, Appl_CtrlCd);
    }

    public async Task<DataList<TraceResponseData>> GetTraceResults(bool checkCycle = false)
    {
        return await DB.TraceResponseTable.GetTraceResponseForApplication(Appl_EnfSrv_Cd, Appl_CtrlCd, checkCycle);
    }

    public async Task<List<CraFieldData>> GetCraFields()
    {
        return await DB.TraceResponseTable.GetCraFields();
    }

    public async Task<List<CraFormData>> GetCraForms()
    {
        return await DB.TraceResponseTable.GetCraForms();
    }

    public async Task<DataList<TraceFinancialResponseData>> GetTraceFinancialResults()
    {
        var data = await DB.TraceResponseTable.GetTraceResponseFinancialsForApplication(Appl_EnfSrv_Cd, Appl_CtrlCd);

        await GetFinancialDetails(data);

        return data;
    }

    private async Task GetFinancialDetails(DataList<TraceFinancialResponseData> data)
    {
        foreach (var finData in data.Items)
        {
            var details = await DB.TraceResponseTable.GetTraceResponseFinancialDetails(finData.TrcRspFin_Id);
            data.Messages.AddRange(details.Messages);

            await GetFinancialDetailValues(details, data);

            finData.TraceFinancialDetails = details.Items;
        }
    }

    private async Task GetFinancialDetailValues(DataList<TraceFinancialResponseDetailData> details, DataList<TraceFinancialResponseData> data)
    {
        foreach (var detail in details.Items)
        {
            var values = await DB.TraceResponseTable.GetTraceResponseFinancialDetailValues(detail.TrcRspFin_Dtl_Id);
            data.Messages.AddRange(values.Messages);

            detail.TraceDetailValues = values.Items;
        }
    }

    public async Task<ApplicationEventsList> GetRequestedTRCINTracingEvents(string enfSrv_Cd, string cycle)
    {
        return await EventManager.GetRequestedTRCINTracingEvents(enfSrv_Cd, cycle);
    }

    public async Task<ApplicationEventDetailsList> GetActiveTracingEventDetails(string enfSrv_Cd, string cycle)
    {
        return await EventDetailManager.GetActiveTracingEventDetails(enfSrv_Cd, cycle);
    }

    public async Task ApplySINfailed()
    {
        await SetNewStateTo(ApplicationState.SIN_NOT_CONFIRMED_5);

        foreach (var eventItem in EventManager.Events)
            eventItem.Subm_Update_SubmCd = "SYSTEM";

        await UpdateApplicationNoValidation();
    }

    public async Task<bool> CreateResponseData(List<TraceResponseData> responseData)
    {
        var responsesDB = DB.TraceResponseTable;
        await responsesDB.InsertBulkData(responseData);

        if (!string.IsNullOrEmpty(DB.MainDB.LastError))
            return false;

        return true;
    }

    public async Task CreateFinancialResponseData(TraceFinancialResponseData responseData)
    {
        var id = await DB.TraceResponseTable.CreateTraceFinancialResponse(responseData);
        if (responseData.TraceFinancialDetails is not null)
            foreach (var detail in responseData.TraceFinancialDetails)
            {
                detail.TrcRspFin_Id = id;
                var detailId = await DB.TraceResponseTable.CreateTraceFinancialResponseDetail(detail);
                if (detail.TraceDetailValues is not null)
                {
                    foreach (var value in detail.TraceDetailValues)
                    {
                        value.TrcRspFin_Dtl_Id = detailId;
                        _ = await DB.TraceResponseTable.CreateTraceFinancialResponseDetailValue(value);
                    }
                }
            }
    }

    public async Task MarkResponsesAsViewed(string recipientSubmCd)
    {
        var responsesDB = DB.TraceResponseTable;
        await responsesDB.MarkResponsesAsViewed(recipientSubmCd);
    }

    public async Task CreateNETPevents()
    {
        await DB.TracingTable.CreateESDCEventTraceData();
    }

    public async Task<List<TracingOutgoingFederalData>> GetFederalOutgoingData(int maxRecords,
                                                                               string activeState,
                                                                               ApplicationState lifeState,
                                                                               string enfServiceCode)
    {
        var tracingDB = DB.TracingTable;
        return await tracingDB.GetFederalOutgoingData(maxRecords, activeState, lifeState, enfServiceCode);
    }

    public async Task<TracingOutgoingProvincialData> GetProvincialOutgoingData(int maxRecords,
                                                                               string activeState,
                                                                               string recipientCode,
                                                                               bool isXML = true)
    {
        var tracingDB = DB.TracingTable;
        var data = await tracingDB.GetProvincialOutgoingData(maxRecords, activeState, recipientCode, isXML);
        return data;
    }

    public async Task InsertAffidavitData(AffidavitData data)
    {
        await DB.AffidavitTable.InsertAffidavitData(data);

        var currentDataManager = new TracingManager(DB, Config, CurrentUser);
        await currentDataManager.LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd);

        var currentState = currentDataManager.GetState();
        if (currentState.In(ApplicationState.INVALID_APPLICATION_1,
                            ApplicationState.SIN_CONFIRMATION_PENDING_3,
                            ApplicationState.SIN_NOT_CONFIRMED_5,
                            ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7))
        {
            currentDataManager.EventManager.AddSubmEvent(EventCode.C50773_APPLICATION_IS_NOT_AT_STATE_6_AND_CANNOT_BE_SWORN__SWEARING_HELD_FOR_90_DAYS);
            await currentDataManager.EventManager.SaveEvents();
        }
    }

}
