using FOAEA3.Data.DB;

namespace FOAEA3.Business.Security;

internal class LoginManager
{
    private readonly IRepositories DB;
    private const int PASSWORD_EXPIRY_DAYS = 180;
    private const int PASSWORD_FORMAT = 1;

    public LoginManager(IRepositories repositories)
    {
        DB = repositories;
    }
    public static bool ValidateLogins = true;

    public async Task<bool> LoginIsAccountExpired(string subjectName)
    {
        return await DB.LoginTable.IsLoginExpired(subjectName);
    }

    public async Task<bool> CheckPreviousPasswords(string subjectName, string newPassword)
    {
        SubjectData subject = await GetSubject(subjectName);
        return await DB.LoginTable.CheckPreviousPasswords(subject.SubjectId, newPassword);

    }
    public async Task<bool> GetAllowedAccess(string username)
    {
        return await DB.LoginTable.GetAllowedAccess(username);
    }

    public async Task AcceptNewTermsOfReference(string username)
    {
        await DB.LoginTable.AcceptNewTermsOfReferernce(username);
    }

    public async Task<bool> SetPassword(string username, string password, string passwordSalt)
    {
        SubjectData subject = await GetSubject(username);
        if (subject.SubjectName == null)
        {
            return false;
        }
        else
        {
            await DB.LoginTable.SetPassword(username, password, PASSWORD_FORMAT, passwordSalt, PASSWORD_EXPIRY_DAYS);
            return true;
        }

    }

    public async Task SendEmail(string subject, string recipient, string body, int isHTML = 1)
    {
        var dbNotification = new DBNotification(DB.MainDB);
        await dbNotification.SendEmail(subject, recipient, body, isHTML);
    }

    public async Task<SubjectData> GetSubject(string subjectName)
    {
        return await DB.SubjectTable.GetSubject(subjectName);
    }

    public async Task<SubjectData> GetSubjectByConfirmationCode(string confirmationCode)
    {
        return await DB.SubjectTable.GetSubjectByConfirmationCode(confirmationCode);
    }

    public async Task PostConfirmationCode(int subjectId, string confirmationCode)
    {
        await DB.LoginTable.PostConfirmationCode(subjectId, confirmationCode);
    }

    public async Task<string> GetEmailByConfirmationCode(string confirmationCode)
    {
        return await DB.LoginTable.GetEmailByConfirmationCode(confirmationCode);
    }

    public async Task PostPassword(string confirmationCode, string password, string salt, string initial)
    {
        await DB.LoginTable.PostPassword(confirmationCode, password, salt, initial);
    }
}
