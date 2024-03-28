using System;

namespace FileBroker.Model;

public class ApiTransferLogDetailData
{
    public int ApiTransferLogId { get; set; }
    public string Appl_EnfSrvCd { get; set; }
    public string Appl_CtrlCd { get; set; }
    public string OutgoingJsonData { get; set; }
    public DateTime? ApiRequestSent { get; set; }
    public bool IsWaiting { get; set; }
    public DateTime? ApiResponseReceived { get; set; }
    public string IncomingJsonData { get; set; }
    public bool IsStoredLocally { get; set; }
}
