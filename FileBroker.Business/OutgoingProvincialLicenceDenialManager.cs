using FileBroker.Common.Helpers;
using System.Text;

namespace FileBroker.Business;

public class OutgoingProvincialLicenceDenialManager : IOutgoingFileManager
{
    private APIBrokerList APIs { get; }
    private RepositoryList DB { get; }
    private FoaeaSystemAccess FoaeaAccess { get; }

    public OutgoingProvincialLicenceDenialManager(APIBrokerList apis, RepositoryList repositories, IFileBrokerConfigurationHelper config)
    {
        APIs = apis;
        DB = repositories;
        FoaeaAccess = new FoaeaSystemAccess(apis, config.FoaeaLogin);
    }

    public async Task<(string, List<string>)> CreateOutputFile(string fileBaseName)
    {
        var errors = new List<string>();

        bool fileCreated = false;

        var fileTableData = await DB.FileTable.GetFileTableDataForFileName(fileBaseName);

        string newCycle = (fileTableData.Cycle + 1).ToString("000000");

        try
        {
            var processCodes = await DB.ProcessParameterTable.GetProcessCodes(fileTableData.PrcId);

            string newFilePath = fileTableData.Path + fileBaseName + "." + newCycle + ".xml";
            if (File.Exists(newFilePath))
            {
                errors.Add("** Error: File Already Exists");
                return ("", errors);
            }

            await FoaeaAccess.SystemLogin();

            try
            {
                var data = await GetOutgoingDataFromFoaea(fileTableData, processCodes.ActvSt_Cd, processCodes.SubmRecptCd);

                if (data.Any())
                {
                    string fileContent = GenerateOutputFileContentFromData(data, newCycle);

                    await File.WriteAllTextAsync(newFilePath, fileContent);
                    fileCreated = true;

                    await DB.OutboundAuditTable.InsertIntoOutboundAudit(fileBaseName + "." + newCycle, DateTime.Now, fileCreated,
                                                                         "Outbound File created successfully.");

                    await DB.FileTable.SetNextCycleForFileType(fileTableData, newCycle.Length);

                    await APIs.LicenceDenialResponses.MarkTraceResultsAsViewed(processCodes.EnfSrv_Cd);
                }
                else
                {
                    errors.Add(DataHelper.NO_DATA_FOUND);
                }
            }
            finally
            {
                await FoaeaAccess.SystemLogout();
            }

            return (newFilePath, errors);

        }
        catch (Exception e)
        {
            string error = "Error Creating Outbound Data File: " + e.Message;
            errors.Add(error);

            await DB.OutboundAuditTable.InsertIntoOutboundAudit(fileBaseName + "." + newCycle, DateTime.Now, fileCreated, error);

            await DB.ErrorTrackingTable.MessageBrokerError($"File Error: {fileTableData.PrcId} {fileBaseName}",
                                                                       "Error creating outbound file", e, displayExceptionError: true);

            return (string.Empty, errors);
        }

    }

    private async Task<List<LicenceDenialOutgoingProvincialData>> GetOutgoingDataFromFoaea(FileTableData fileTableData, 
                                                                                           string actvSt_Cd, string recipientCode)
    {
        var recMax = await DB.ProcessParameterTable.GetValueForParameter(fileTableData.PrcId, "rec_max");
        int maxRecords = string.IsNullOrEmpty(recMax) ? 0 : int.Parse(recMax);

        var data = await APIs.LicenceDenialApplications.GetOutgoingProvincialLicenceDenialData(maxRecords, actvSt_Cd,
                                                                                               recipientCode);
        return data;
    }


    private static string GenerateOutputFileContentFromData(List<LicenceDenialOutgoingProvincialData> data,
                                                            string newCycle)
    {
        var result = new StringBuilder();

        result.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\"?>");
        result.AppendLine("<ProvincialOutboundXMLFileLicenceResponseCode85>");

        result.AppendLine(GenerateHeaderLine(newCycle));

        foreach (var item in data)
            result.AppendLine(GenerateDetailLine(item));

        result.AppendLine(GenerateFooterLine(data.Count));

        result.Append("</ProvincialOutboundXMLFileLicenceResponseCode85>");

        return result.ToString();
    }

    private static string GenerateHeaderLine(string newCycle)
    {
        string xmlCreationDateTime = DateTime.Now.Date.ToString("yyyy-MM-ddTHH:mm:sszzz");

        var output = new StringBuilder();
        output.AppendLine($"  <Header>");
        output.AppendLine($"    <Record_Type>01</Record_Type>");
        output.AppendLine($"    <Cycle_Number>{newCycle}</Cycle_Number>");
        output.AppendLine($"    <File_Creation_Date>{xmlCreationDateTime}</File_Creation_Date>");
        output.Append($"  </Header>");

        return output.ToString();
    }

    private static string GenerateDetailLine(LicenceDenialOutgoingProvincialData item)
    {
        string xmlReceiptDate = item.LicRsp_Rcpt_Dte.ToString("yyyy-MM-ddTHH:mm:sszzz");

        var output = new StringBuilder();
        output.AppendLine($"  <Licence_Response_Code>");
        output.AppendLine(" " + XmlHelper.GenerateXMLTagWithValue("Record_Type_Code", "80"));
        output.AppendLine(" " + XmlHelper.GenerateXMLTagWithValue("Enforcement_Service_Code", item.Appl_EnfSrv_Cd));
        output.AppendLine(" " + XmlHelper.GenerateXMLTagWithValue("Issuing_Submitter_Code", item.Subm_SubmCd));
        output.AppendLine(" " + XmlHelper.GenerateXMLTagWithValue("Appl_Control_Code", item.Appl_CtrlCd));
        output.AppendLine(" " + XmlHelper.GenerateXMLTagWithValue("Source_Reference_Number", item.Appl_Source_RfrNr));
        output.AppendLine(" " + XmlHelper.GenerateXMLTagWithValue("Recipient_Submitter_Code", item.Subm_Recpt_SubmCd));
        output.AppendLine(" " + XmlHelper.GenerateXMLTagWithValue("Receipt_Date", xmlReceiptDate));
        output.AppendLine(" " + XmlHelper.GenerateXMLTagWithValue("Sequence_Number", item.LicRsp_SeqNr));
        output.AppendLine(" " + XmlHelper.GenerateXMLTagWithValue("Licence_Status_Code", item.RqstStat_Cd.ToString()));
        output.AppendLine(" " + XmlHelper.GenerateXMLTagWithValue("Licence_Source_Service_Code", item.EnfSrv_Cd));
        output.Append($"  </Licence_Response_Code>");

        return output.ToString();
    }

    private static string GenerateFooterLine(int rowCount)
    {
        var output = new StringBuilder();
        output.AppendLine($"  <Trailer>");
        output.AppendLine($"    <Record_Type>99</Record_Type>");
        output.AppendLine($"    <Detail_Record_Count>{rowCount:000000}</Detail_Record_Count>");
        output.Append($"  </Trailer>");

        return output.ToString();
    }
}
