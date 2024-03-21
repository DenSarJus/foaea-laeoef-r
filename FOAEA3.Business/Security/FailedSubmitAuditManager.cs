namespace FOAEA3.Business.Security;

public class FailedSubmitAuditManager
{
    private readonly IRepositories DB;
    private readonly ApplicationData Application;

    public FailedSubmitAuditManager(IRepositories repositories, ApplicationData application)
    {
        DB = repositories;
        Application = application;
    }

    public async Task AddToFailedSubmitAudit(FailedSubmitActivityAreaType activityType)
    {
        string subject_submitter = $"{DB.CurrentUser} ({DB.CurrentSubmitter})";

        foreach (var errorInfo in Application.Messages.GetMessagesForType(MessageType.Error))
            await DB.FailedSubmitAuditTable.AppendFiledSubmitAudit(subject_submitter, activityType, errorInfo.Description);

    }

}
