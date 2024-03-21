using FOAEA3.Business.Security;

namespace FOAEA3.Business.Areas.Application;

internal class ApplicationSearchManager
{
    private readonly IRepositories DB;
    private readonly AccessAuditManager AuditManager;

    public string LastError { get; set; }

    public ApplicationSearchManager(IRepositories repositories)
    {
        DB = repositories;
        AuditManager = new AccessAuditManager(DB);
    }

    public async Task<(List<ApplicationSearchResultData>, int)> Search(QuickSearchData searchCriteria,
                                                                       int page, int perPage, string orderBy)
    {
        int accessAuditId = await AuditManager.AddAuditHeader(AccessAuditPage.ApplicationSearch);
        await AddSearchCriteriaToAccessAudit(accessAuditId, searchCriteria);

        List<ApplicationSearchResultData> searchResults;
        int totalCount;
        (searchResults, totalCount) = await DB.ApplicationSearchTable.QuickSearch(searchCriteria,
                                                                                     page, perPage, orderBy);

        LastError = DB.ApplicationSearchTable.LastError;

        await AddSearchResultsToAccessAudit(accessAuditId, searchResults);

        return (searchResults, totalCount);
    }

    private async Task AddSearchCriteriaToAccessAudit(int accessAuditId, QuickSearchData searchCriteria)
    {
        var items = new Dictionary<AccessAuditElement, string>();

        if (!string.IsNullOrEmpty(searchCriteria.Category)) items.Add(AccessAuditElement.Category, searchCriteria.Category);
        if (!string.IsNullOrEmpty(searchCriteria.ControlCode)) items.Add(AccessAuditElement.CtrlCode, searchCriteria.ControlCode);
        if (!string.IsNullOrEmpty(searchCriteria.EnforcementService)) items.Add(AccessAuditElement.EnfSrv_Cd, searchCriteria.EnforcementService);
        if (!string.IsNullOrEmpty(searchCriteria.ReferenceNumber)) items.Add(AccessAuditElement.EnfRefSourceNr, searchCriteria.ReferenceNumber);
        if (!string.IsNullOrEmpty(searchCriteria.DebtorFirstName)) items.Add(AccessAuditElement.FirstName, searchCriteria.DebtorFirstName);
        if (!string.IsNullOrEmpty(searchCriteria.DebtorMiddleName)) items.Add(AccessAuditElement.MiddleName, searchCriteria.DebtorMiddleName);
        if (!string.IsNullOrEmpty(searchCriteria.DebtorSurname)) items.Add(AccessAuditElement.LastName, searchCriteria.DebtorSurname);
        if (!string.IsNullOrEmpty(searchCriteria.JusticeNumber)) items.Add(AccessAuditElement.JusticeNr, searchCriteria.JusticeNumber);
        if (!string.IsNullOrEmpty(searchCriteria.SIN)) items.Add(AccessAuditElement.SIN, searchCriteria.SIN);
        if (!string.IsNullOrEmpty(searchCriteria.Status)) items.Add(AccessAuditElement.Status, searchCriteria.Status);
        if (!string.IsNullOrEmpty(searchCriteria.Submitter)) items.Add(AccessAuditElement.Subm_SubmCd, searchCriteria.Submitter);
        if (searchCriteria.State != ApplicationState.UNDEFINED) items.Add(AccessAuditElement.State, ((int)searchCriteria.State).ToString());
        if (searchCriteria.DebtorDateOfBirth_Start.HasValue) items.Add(AccessAuditElement.DOBFrom, searchCriteria.DebtorDateOfBirth_Start.Value.ToString(DateTimeExtensions.FOAEA_DATE_FORMAT));
        if (searchCriteria.DebtorDateOfBirth_End.HasValue) items.Add(AccessAuditElement.DOBTo, searchCriteria.DebtorDateOfBirth_End.Value.ToString(DateTimeExtensions.FOAEA_DATE_FORMAT));
        if (searchCriteria.CreatedDate_Start.HasValue) items.Add(AccessAuditElement.ApplicationReceiptDateFrom, searchCriteria.CreatedDate_Start.Value.ToString(DateTimeExtensions.FOAEA_DATE_FORMAT));
        if (searchCriteria.CreatedDate_End.HasValue) items.Add(AccessAuditElement.ApplicationReceiptDateTo, searchCriteria.CreatedDate_End.Value.ToString(DateTimeExtensions.FOAEA_DATE_FORMAT));
        if (searchCriteria.ActualEndDate_Start.HasValue) items.Add(AccessAuditElement.ApplicationExpiryDateFrom, searchCriteria.ActualEndDate_Start.Value.ToString(DateTimeExtensions.FOAEA_DATE_FORMAT));
        if (searchCriteria.ActualEndDate_End.HasValue) items.Add(AccessAuditElement.ApplicationExpiryDateTo, searchCriteria.ActualEndDate_End.Value.ToString(DateTimeExtensions.FOAEA_DATE_FORMAT));
        if (searchCriteria.ViewAllJurisdiction) items.Add(AccessAuditElement.ViewAllJurisdictions, searchCriteria.ViewAllJurisdiction.ToString());
        if (searchCriteria.SearchOnlySinConfirmed) items.Add(AccessAuditElement.SINConfirmed, searchCriteria.SearchOnlySinConfirmed.ToString());

        await AuditManager.AddAuditElements(accessAuditId, items);
    }

    private async Task AddSearchResultsToAccessAudit(int accessAuditId, List<ApplicationSearchResultData> searchResults)
    {
        foreach (var searchResult in searchResults)
        {
            string key = $"{searchResult.Appl_EnfSrv_Cd.Trim()}-{searchResult.Appl_CtrlCd.Trim()}";
            await AuditManager.AddAuditElement(accessAuditId, AccessAuditElement.ApplicationID, key);
        }
    }

}
