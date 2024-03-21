namespace FOAEA3.Business.Security;

public class AccessAuditManager
{
    private readonly IRepositories DB;
    public readonly List<string> Errors;

    public AccessAuditManager(IRepositories repositories)
    {
        DB = repositories;
        Errors = new List<string>();
    }

    public async Task<int> AddAuditHeader(AccessAuditPage page)
    {
        string subject_submitter = $"{DB.CurrentUser} ({DB.CurrentSubmitter})";

        return await DB.AccessAuditTable.SaveDataPageInfo(page, subject_submitter);
    }

    public async Task AddAuditElement(int headerId, AccessAuditElement elementType, string elementValue)
    {
        var accessAuditElements = new Dictionary<AccessAuditElement, AccessAuditElementTypeData>();
        var allElementTypes = await DB.AccessAuditTable.GetAllElementAccessType();

        foreach (var thisElementType in allElementTypes)
        {
            if (Enum.IsDefined(typeof(AccessAuditElement), thisElementType.AccessAuditDataElementValueType_ID))
                accessAuditElements.Add((AccessAuditElement)thisElementType.AccessAuditDataElementValueType_ID, thisElementType);
            else
                Errors.Add($"Undefined access audit element type: {thisElementType.AccessAuditDataElementValueType_ID} [{thisElementType.ElementName}]");
        }

        string elementName = accessAuditElements[elementType].ElementName;
        await DB.AccessAuditTable.SaveDataValue(headerId, elementName, elementValue);
    }

    public async Task AddAuditElements(int headerId, Dictionary<AccessAuditElement, string> elements)
    {
        foreach (var (elementType, elementValue) in elements)
            await AddAuditElement(headerId, elementType, elementValue);
    }

}
