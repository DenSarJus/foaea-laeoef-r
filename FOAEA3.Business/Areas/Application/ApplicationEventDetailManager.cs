namespace FOAEA3.Business.Areas.Application;

internal class ApplicationEventDetailManager
{
    ApplicationData Application { get; }
    public ApplicationEventDetailsList EventDetails { get; }
    IApplicationEventDetailRepository EventDetailDB { get; }

    public ApplicationEventDetailManager(ApplicationData applicationData, IRepositories repositories)
    {
        Application = applicationData;
        EventDetails = new ApplicationEventDetailsList();
        EventDetailDB = repositories.ApplicationEventDetailTable;
    }

    public async Task<ApplicationEventDetailsList> GetApplicationEventDetailsForQueue(EventQueue queue)
    {
        return await EventDetailDB.GetApplicationEventDetails(Application.Appl_EnfSrv_Cd, Application.Appl_CtrlCd, queue);
    }

    public async Task<bool> SaveEventDetails()
    {
        return await EventDetailDB.SaveEventDetails(EventDetails);
    }

    public async Task<bool> SaveEventDetail(ApplicationEventDetailData eventDetailData)
    {
        return await EventDetailDB.SaveEventDetail(eventDetailData);
    }

    public async Task UpdateOutboundEventDetail(string activeState, string applicationState, string enfSrvCode,
                                          string writtenFile, List<int> eventIds)
    {
        await EventDetailDB.UpdateOutboundEventDetail(activeState, applicationState, enfSrvCode, writtenFile, eventIds);
    }

    public async Task<ApplicationEventDetailsList> GetRequestedSINEventDetailDataForFile(string enfSrv_Cd, string fileName)
    {
        return await EventDetailDB.GetRequestedSINEventDetailDataForFile(enfSrv_Cd, fileName);
    }

    public async Task<ApplicationEventDetailsList> GetActiveTracingEventDetails(string enfSrv_Cd, string cycle)
    {
        return await EventDetailDB.GetActiveTracingEventDetails(enfSrv_Cd, cycle);
    }

    public async Task<ApplicationEventDetailsList> GetRequestedLICINLicenceDenialEventDetails(string enfSrv_Cd, string appl_EnfSrv_Cd,
                                                                   string appl_CtrlCd)
    {
        return await EventDetailDB.GetRequestedLICINLicenceDenialEventDetails(enfSrv_Cd, appl_EnfSrv_Cd, appl_CtrlCd);
    }
}
