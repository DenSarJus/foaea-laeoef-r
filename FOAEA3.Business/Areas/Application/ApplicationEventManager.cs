namespace FOAEA3.Business.Areas.Application;

internal class ApplicationEventManager
{
    public ApplicationEventsList Events { get; }

    ApplicationData Application { get; }
    IApplicationEventRepository EventDB { get; }

    public ApplicationEventManager(ApplicationData applicationData, IRepositories repositories)
    {
        Application = applicationData;
        Events = new ApplicationEventsList();
        EventDB = repositories.ApplicationEventTable;
    }

    public async Task<ApplicationEventsList> GetApplicationEventsForQueue(EventQueue queue)
    {
        return await EventDB.GetApplicationEvents(Application.Appl_EnfSrv_Cd, Application.Appl_CtrlCd, queue);
    }

    public void AddSubmEvent(EventCode eventCode, string eventReasonText = null,
                 ApplicationState appState = ApplicationState.UNDEFINED, DateTime? effectiveDateTime = null,
                 string recipientSubm = null, string submCd = null, string enfSrv = null, string controlCode = null,
                 string activeState = "A", string updateSubm = null)
    {
        AddEvent(eventCode, eventReasonText, EventQueue.EventSubm, appState, effectiveDateTime, recipientSubm, submCd,
                 enfSrv, controlCode, activeState, updateSubm);
    }

    public void AddTraceEvent(EventCode eventCode, string eventReasonText = null,
                 ApplicationState appState = ApplicationState.UNDEFINED, DateTime? effectiveDateTime = null,
                 string recipientSubm = null, string submCd = null, string enfSrv = null, string controlCode = null,
                 string activeState = "A", string updateSubm = null)
    {
        AddEvent(eventCode, eventReasonText, EventQueue.EventTrace, appState, effectiveDateTime, recipientSubm, submCd,
                 enfSrv, controlCode, activeState, updateSubm);
    }

    public void UpdateEvent(ApplicationEventData eventData)
    {
        Events.Add(eventData);
    }

    public void AddEvent(EventCode eventCode, string eventReasonText = null, EventQueue queue = EventQueue.EventSubm,
                         ApplicationState appState = ApplicationState.UNDEFINED, DateTime? effectiveDateTime = null,
                         string recipientSubm = null, string submCd = null, string enfSrv = null, string controlCode = null,
                         string activeState = "A", string updateSubm = null)
    {
        if (appState == ApplicationState.UNDEFINED)
            appState = Application.AppLiSt_Cd;

        DateTime eventEffectiveDateTime = DateTime.Now;
        if (effectiveDateTime.HasValue)
            eventEffectiveDateTime = effectiveDateTime.Value;
        else
        {
            if (queue == EventQueue.EventBF)
            {
                if (Application.AppCtgy_Cd == "I01")
                    eventEffectiveDateTime = Application.Appl_Create_Dte.AddDays(3);
                else
                    eventEffectiveDateTime = DateTime.Now.AddDays(10);
            }
        }

        Events.Add(new ApplicationEventData
        {
            Queue = queue,
            Event_Reas_Cd = eventCode,
            Event_Reas_Text = eventReasonText,
            Appl_CtrlCd = string.IsNullOrEmpty(controlCode) ? Application.Appl_CtrlCd : controlCode,
            Appl_EnfSrv_Cd = string.IsNullOrEmpty(enfSrv) ? Application.Appl_EnfSrv_Cd : enfSrv,
            AppLiSt_Cd = appState == ApplicationState.UNDEFINED ? ApplicationState.INITIAL_STATE_0 : appState,
            Subm_Recpt_SubmCd = recipientSubm ?? Application.Subm_Recpt_SubmCd,
            Event_TimeStamp = DateTime.Now,
            Event_RecptSubm_ActvStCd = "A",
            Event_Priority_Ind = "N",
            Event_Effctv_Dte = eventEffectiveDateTime,
            ActvSt_Cd = activeState,
            Subm_SubmCd = string.IsNullOrEmpty(submCd) ? Application.Subm_SubmCd : submCd,
            Subm_Update_SubmCd = updateSubm ?? Application.Appl_LastUpdate_Usr
        });
    }

    public async Task<bool> SaveEvents(ApplicationState applicationState = ApplicationState.UNDEFINED, string activeState = "")
    {
        if (!string.IsNullOrEmpty(Application.Appl_CtrlCd))
        {
            bool success = await EventDB.SaveEvents(Events, applicationState, activeState);

            Events.Clear();

            return success;
        }
        else
            return false;

    }

    public async Task<bool> SaveEvent(ApplicationEventData eventData, ApplicationState applicationState = ApplicationState.UNDEFINED,
                          string activeState = "")
    {
        return await EventDB.SaveEvent(eventData, applicationState, activeState);
    }

    public string GetLastError()
    {
        return EventDB.GetLastError();
    }

    #region EventBF

    public async Task<ApplicationEventsList> GetEventBF(string subm_SubmCd, string appl_CtrlCd, EventCode eventCode, string activeState)
    {
        return await EventDB.GetEventBF(subm_SubmCd, appl_CtrlCd, eventCode, activeState);
    }

    public void AddBFEvent(EventCode eventCode, string eventReasonText = null, ApplicationState appState = ApplicationState.UNDEFINED,
                           DateTime? effectiveDateTime = null)
    {
        AddEvent(eventCode, eventReasonText, queue: EventQueue.EventBF, appState: appState, effectiveDateTime: effectiveDateTime);
    }

    public async Task DeleteBFEvent(string subm_SubmCd, string appl_CtrlCd)
    {
        await EventDB.DeleteBFEvent(subm_SubmCd, appl_CtrlCd);
    }

    #endregion

    #region SIN

    public async Task<ApplicationEventsList> GetRequestedSINEventDataForFile(string enfSrv_Cd, string fileName)
    {
        return await EventDB.GetRequestedSINEventDataForFile(enfSrv_Cd, fileName);
    }

    public void AddSINEvent(EventCode eventCode, string eventReasonText = null, ApplicationState appState = ApplicationState.UNDEFINED)
    {
        if (appState == ApplicationState.UNDEFINED)
            appState = Application.AppLiSt_Cd;

        AddEvent(eventCode, eventReasonText, queue: EventQueue.EventSIN, appState: appState);
    }

    #endregion

    #region Tracing

    public async Task<int> GetTraceEventCount(string appl_EnfSrv_Cd, string appl_CtrlCd, DateTime receivedAffidavitDate,
                                  EventCode eventReasonCode, int eventId)
    {
        return await EventDB.GetTraceEventCount(appl_EnfSrv_Cd, appl_CtrlCd, receivedAffidavitDate, eventReasonCode, eventId);
    }

    public async Task<ApplicationEventsList> GetRequestedTRCINTracingEvents(string enfSrv_Cd, string cycle)
    {
        return await EventDB.GetRequestedTRCINTracingEvents(enfSrv_Cd, cycle);
    }

    public async Task<ApplicationEventsList> GetRequestedLICINLicenceDenialEvents(string enfSrv_Cd, string appl_EnfSrv_Cd,
                                                                           string appl_CtrlCd)
    {
        return await EventDB.GetRequestedLICINLicenceDenialEvents(enfSrv_Cd, appl_EnfSrv_Cd, appl_CtrlCd);
    }

    public async Task<List<SinInboundToApplData>> GetLatestSinEventDataSummary()
    {
        return await EventDB.GetLatestSinEventDataSummary();
    }
    #endregion

}
