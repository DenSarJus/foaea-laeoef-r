using DBHelper;
using FileBroker.Model.Interfaces;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace FileBroker.Data.DB;

public class DBMailService : IMailServiceRepository
{
    private IDBToolsAsync MainDB { get; }

    public DBMailService(IDBToolsAsync mainDB)
    {
        MainDB = mainDB;
    }

    public async Task<string> SendEmail(string subject, string recipients, string body, string attachmentPath = null)
    {
     
        var parameters = new Dictionary<string, object>
        {
            {"subject", subject },
            {"body", body },
            {"recipients", recipients }
        };

        if (!string.IsNullOrEmpty(attachmentPath))
        {
            int attachmentId = await UploadAttachmentToDatabase(attachmentPath);

            if (!string.IsNullOrEmpty(MainDB.LastError))
                return MainDB.LastError;

            parameters.Add("attachmentId", attachmentId);
        }

        await MainDB.ExecProcAsync("SendHtmlMailMessageWithAttachment", parameters);

        if (!string.IsNullOrEmpty(MainDB.LastError))
            return MainDB.LastError;

        return "";
    }

    private async Task<int> UploadAttachmentToDatabase(string attachmentPath)
    {
        string attachmentFileName = Path.GetFileName(attachmentPath);
        string attachmentContent = File.ReadAllText(attachmentPath);

        var parameters = new Dictionary<string, object>
        {
            {"AttachmentFileName", attachmentFileName},
            {"AttachmentContent", attachmentContent }
        };

        decimal returnValue = await MainDB.GetDataFromProcSingleValueAsync<decimal>("EmailAttachment_Insert", parameters);

        return (int)returnValue;

    }

}
