namespace FOAEA3.Business.Security;

internal class SubjectRoleManager
{
    private readonly IRepositories DB;


    public SubjectRoleManager(IRepositories repositories)
    {
        DB = repositories;
    }
    public async Task<List<SubjectRoleData>> GetSubjectRoles(string subjectName)
    {
        return await DB.SubjectRoleTable.GetSubjectRoles(subjectName);
    }
    public async Task<List<string>> GetAssumedRolesForSubject(string subjectName)
    {
        return await DB.SubjectRoleTable.GetAssumedRolesForSubject(subjectName);
    }
}
