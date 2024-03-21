namespace FOAEA3.Business.Security;

internal class SubmitterProfileManager
{
    private readonly IRepositories DB;

    public SubmitterProfileManager(IRepositories repositories)
    {
        DB = repositories;
    }

    public async Task<SubmitterProfileData> GetSubmitterProfile(string submitterCode)
    {
        return await DB.SubmitterProfileTable.GetSubmitterProfile(submitterCode);
    }

}
