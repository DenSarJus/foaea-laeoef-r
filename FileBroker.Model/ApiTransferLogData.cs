using System;

namespace FileBroker.Model;

public class ApiTransferLogData
{
    public int ApiTransferLogId { get; set; }
    public int FileCycle { get; set; }
    public DateTime FileCreated { get; set; }
    public string FileName { get; set; }
    public string ApiUrl { get; set; }
}
