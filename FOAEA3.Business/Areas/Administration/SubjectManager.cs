namespace FOAEA3.Business.Areas.Administration;

public class SubjectManager
{
    private IRepositories DB { get; }
    public SubjectManager(IRepositories repositories)
    {
        DB = repositories;
    }

    public async Task<SubjectData> GetSubjectByConfirmationCode(string confirmationCode)
    {
        return await DB.SubjectTable.GetSubjectByConfirmationCode(confirmationCode);
    }

}
