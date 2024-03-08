using DBHelper;

namespace FileBroker.Business;

public partial class IncomingFederalTracingManager
{
    public async Task<List<string>> ProcessFlatFileData(string flatFileContent, string flatFileName)
    {
        var fileTableData = await GetFileTableData(flatFileName);
        var tracingFileData = new FedTracingFileBase();
        (string enfSrvCd, FederalSource fedSource) = ConfigureTracingFileDataBasedOnSource(tracingFileData, fileTableData.Name);

        var errors = new List<string>();

        if (fedSource == FederalSource.Unknown)
            errors.Add($"No match for [{fileTableData.Name}] or not support federal tracing source.");

        else if (!fileTableData.Active.HasValue || !fileTableData.Active.Value)
            errors.Add($"[{fileTableData.Name}] is not active.");

        if (errors.Any())
            return errors;

        string fileCycle = Path.GetExtension(flatFileName)[1..];
        try
        {
            var fileLoader = new IncomingFederalTracingFileLoader(DB.FlatFileSpecs, fileTableData.PrcId);
            await fileLoader.FillTracingFileDataFromFlatFile(tracingFileData, flatFileContent, errors);

            if (errors.Any())
            {
                errors.Add($"File {flatFileName} was not loaded.");
                return errors;
            }

            ValidateHeader(tracingFileData.TRCIN01, flatFileName, ref errors);
            ValidateFooter(tracingFileData.TRCIN99, tracingFileData.TRCIN02, ref errors);

            if (errors.Any())
            {
                errors.Add($"File {flatFileName} was not loaded.");
                return errors;
            }

            if (!await FoaeaAccess.SystemLogin())
            {
                errors.Add("Failed to login to FOAEA!");
                errors.Add($"File {flatFileName} was not loaded.");
                return errors;
            }
            try
            {
                if ((tracingFileData.TRCIN02.Count == 0) && (fedSource == FederalSource.NETP_Tracing))
                    await CloseNETPTraceEvents();
                else
                {
                    var tracingResponses = await ExtractTracingResponsesFromFileData(tracingFileData, enfSrvCd, fileCycle, errors);

                    if (errors.Any())
                    {
                        errors.Add($"File {flatFileName} was not loaded.");
                        return errors;
                    }

                    if ((tracingResponses != null) && (tracingFileData.TRCIN02.Count > 0))
                        await SendTracingResponsesToFoaea(tracingFileData.TRCIN02, tracingResponses, fileTableData.PrcId,
                                                          enfSrvCd, fedSource, fileCycle, flatFileName, errors);
                }
            }
            finally
            {
                await FoaeaAccess.SystemLogout();
            }
        }
        catch (Exception e)
        {
            errors.Add("An error occurred: " + e.Message);
        }

        return errors;
    }

    private static void ValidateHeader(FedTracing_RecType01 dataFromFile, string flatFileName, ref List<string> errors)
    {
        int cycle = FileHelper.ExtractCycleFromFilename(flatFileName);
        if (dataFromFile.Cycle != cycle)
        {
            errors.Add($"Cycle in file [{dataFromFile.Cycle}] does not match cycle of file [{cycle}]");
        }
    }

    private static void ValidateFooter(FedTracing_RecType99 dataFromFile, List<FedTracing_RecType02> dataSummary, ref List<string> errors)
    {
        if (dataFromFile.ResponseCnt != dataSummary.Count)
        {
            errors.Add("Invalid ResponseCnt in section 99");
        }
    }

    public static (string enfSrvCd, FederalSource fedSource) ConfigureTracingFileDataBasedOnSource(
                                                                       FedTracingFileBase fedTracingData,
                                                                       string fileTableName)
    {
        string enfSrvCd = string.Empty;
        FederalSource fedSource = FederalSource.Unknown;

        switch (fileTableName)
        {
            case "RC3STSIT": // CRA Tracing
                enfSrvCd = "RC01";
                fedSource = FederalSource.CRA_TracingAddresses;
                fedTracingData.AddResidentialRecTypes("03", "05", "06", "07", "08", "09", "10", "11", "12", "13", "14", "15",
                                                      "16", "17", "18", "19", "20", "21");
                fedTracingData.AddEmployerRecTypes("04", "22", "23", "24", "25", "26", "27", "28", "29", "30", "31", "32",
                                                   "33", "34", "35", "36");
                break;

            case "HR3STSIT": // EI Tracing
                enfSrvCd = "HR02";
                fedSource = FederalSource.EI_Tracing;
                fedTracingData.AddResidentialRecTypes("03");
                fedTracingData.AddEmployerRecTypes("04");
                break;

            case "EI3STSIT":  // NETP Tracing
                enfSrvCd = "EI02";
                fedSource = FederalSource.NETP_Tracing;
                fedTracingData.AddEmployerRecTypes("80", "81");
                break;
        }

        return (enfSrvCd, fedSource);
    }

    public async Task SendTracingResponsesToFoaea(List<FedTracing_RecType02> fileTracingSummary, List<TraceResponseData> tracingResponses,
                                                  int processId, string enfSrvCd, FederalSource fedSource, string fileCycle,
                                                  string flatFileName, List<string> errors)
    {
        try
        {
            string cutOffDaysValue = await DB.ProcessParameterTable.GetValueForParameter(processId, "evnt_cutoff");
            int cutOffDays = int.Parse(cutOffDaysValue);

            var activeTraceEvents = await APIs.TracingEvents.GetRequestedTRCINEvents(enfSrvCd, fileCycle);
            var activeTraceEventDetails = await APIs.TracingEvents.GetActiveTracingEventDetails(enfSrvCd, fileCycle);

            if (fedSource == FederalSource.NETP_Tracing)
            {
                foreach (var item in fileTracingSummary)
                {
                    await MarkTraceEventsAsProcessed(item.dat_Appl_EnfSrvCd, item.dat_Appl_CtrlCd.Trim(), flatFileName, newState: 2,
                                                     activeTraceEvents, activeTraceEventDetails);
                }
                await CloseOrInactivateTraceEventDetails(cutOffDays, activeTraceEventDetails);
                await SendTRACEDataToTrcRsp(tracingResponses, errors);
                await CloseNETPTraceEvents();
            }
            else
            {
                foreach (var item in fileTracingSummary)
                    await MarkTraceEventsAsProcessed(item.dat_Appl_EnfSrvCd, item.dat_Appl_CtrlCd.Trim(), flatFileName, 0,
                                                     activeTraceEvents, activeTraceEventDetails);

                await CloseOrInactivateTraceEventDetails(cutOffDays, activeTraceEventDetails);
                await SendTRACEDataToTrcRsp(tracingResponses, errors);
                await UpdateTracingApplications(enfSrvCd, fileCycle, fedSource);
                await CloseOrInactivateTraceEventDetails(cutOffDays, activeTraceEventDetails);

                foreach(var item in fileTracingSummary)
                    await ResetOrCloseTraceEventDetails(item.dat_Appl_EnfSrvCd, item.dat_Appl_CtrlCd.Trim(), activeTraceEvents);
            }
        }
        catch (Exception e)
        {
            errors.Add("Error sending tracing responses to FOAEA: " + e.Message);
        }
    }
        
    private async Task<List<TraceResponseData>> ExtractTracingResponsesFromFileData(FedTracingFileBase tracingFileData, string enfSrvCd, string fileCycle,
                                                                                    List<string> errors)
    {
        var cycles = await APIs.TracingApplications.GetTraceCycleQuantityData(enfSrvCd, fileCycle);

        var responseData = IncomingFederalTracingResponse.GenerateFromFileData(tracingFileData, enfSrvCd, cycles, ref errors);

        ValidateResponseData(responseData, errors);

        return responseData;
    }

    private static void ValidateResponseData(List<TraceResponseData> responseData, List<string> errors)
    {
        foreach (var response in responseData)
        {
            if ((response.TrcRsp_Addr_LstUpdte is not null) &&
                (!response.TrcRsp_Addr_LstUpdte.Value.IsValidSqlDatetime()))
            {
                errors.Add($"(3) Invalid date ({response.TrcRsp_Addr_LstUpdte}) for {response.Appl_EnfSrv_Cd.Trim()}-{response.Appl_CtrlCd} [{response.Prcs_RecType} {response.TrcRsp_Addr_Ln}]");
            }
        }
    }

    private async Task CloseNETPTraceEvents()
    {
        await APIs.TracingEvents.CloseNETPTraceEvents();
    }

    private async Task SendTRACEDataToTrcRsp(List<TraceResponseData> responseData, List<string> errors)
    {
        await APIs.TracingResponses.InsertBulkData(responseData);

        if (APIs.TracingResponses.ApiHelper.ErrorData.Any())
        {
            foreach (var error in APIs.TracingResponses.ApiHelper.ErrorData)
                errors.Add(error.Description);
        }
    }

}
