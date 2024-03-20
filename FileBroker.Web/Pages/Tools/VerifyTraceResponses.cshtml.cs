using FileBroker.Business;
using FileBroker.Business.Helpers;
using FileBroker.Common;
using FileBroker.Model;
using FileBroker.Model.Interfaces;
using FOAEA3.Common.Brokers;
using FOAEA3.Model;
using FOAEA3.Model.Enums;
using FOAEA3.Resources;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;

namespace FileBroker.Web.Pages.Tools
{
    public class VerifyTraceResponsesModel : PageModel
    {
        [BindProperty]
        [Display(Name = "TRACE_RESPONSE_CYCLE", ResourceType = typeof(LanguageResource))]
        public string TraceResponseCycle { get; set; }

        [BindProperty]
        public string FileName { get; set; }

        private IFileTableRepository FileTable { get; }
        private IFlatFileSpecificationRepository FlatFileSpecs { get; }
        private IFileBrokerConfigurationHelper Config { get; }

        public List<FileTableData> IncomingTraceFiles { get; }
        private FoaeaSystemAccess FoaeaAccess { get; }

        private APIBrokerList Apis { get; }

        public VerifyTraceResponsesModel(IFileTableRepository fileTable,
            IFlatFileSpecificationRepository flatFileSpecs,
                                            IFileBrokerConfigurationHelper config)
        {
            FileTable = fileTable;
            FlatFileSpecs = flatFileSpecs;
            Config = config;

            Apis = FoaeaApiHelper.SetupFoaeaAPIs(config.ApiRootData);
            FoaeaAccess = new FoaeaSystemAccess(Apis, config.FoaeaLogin);

            IncomingTraceFiles = FileTable.GetFileTableDataForCategory("TRCIN").Result
                                    .Where(m => m.IsXML == false)
                                    .ToList();
        }

        public async Task OnPost()
        {
            if (ModelState.IsValid)
            {
                try
                {
                    if (!FileExists(out string fullName))
                        ViewData["Error"] = $"Error: {fullName} file not found!";
                    else
                    {
                        // get file content
                        string flatFileContent = System.IO.File.ReadAllText(fullName);

                        var incomingFileData = IncomingTraceFiles.Find(m => m.Name == FileName);
                        var tracingFileData = new FedTracingFileBase();
                        (string enfSrvCd, FederalSource fedSource) = IncomingFederalTracingManager.ConfigureTracingFileDataBasedOnSource(tracingFileData, incomingFileData.Name);

                        var errors = new List<string>();
                        var fileLoader = new IncomingFederalTracingFileLoader(FlatFileSpecs, incomingFileData.PrcId);
                        await fileLoader.FillTracingFileDataFromFlatFile(tracingFileData, flatFileContent, errors);

                        if (errors.Count == 0)
                        {
                            // get data from FOAEA
                            if (!await FoaeaAccess.SystemLogin())
                            {
                                ViewData["Error"] = $"Error: Failed to login to FOAEA!";
                            }
                            else
                            {
                                try
                                {
                                    string baseNameAndExt = Path.GetFileName(fullName);
                                    string errorMessage = string.Empty;

                                    foreach (var applResults in tracingFileData.TRCIN02)
                                    {
                                        string enfSrv = applResults.dat_Appl_EnfSrvCd;
                                        string ctrlCd = applResults.dat_Appl_CtrlCd;

                                        var responses = await Apis.TracingResponses.GetTraceResponseForApplication(enfSrv, ctrlCd);

                                        var eventDetails = (await Apis.TracingEvents.GetTraceEventDetails(enfSrv, ctrlCd, activeOnly: false))
                                                            .Where(m => m.Event_Reas_Text.Contains(baseNameAndExt, StringComparison.InvariantCultureIgnoreCase)).ToList();
                                        DateTime? fileProcessedDate = eventDetails?.FirstOrDefault().Event_Compl_Dte;

                                        bool isSame = CompareData(enfSrvCd, fileProcessedDate, applResults,
                                                                  tracingFileData, responses,
                                                                  out List<FedTracing_RecTypeResidential> missingResidentials,
                                                                  out List<FedTracing_RecTypeEmployer> missingEmployers);

                                        if (!isSame)
                                        {
                                            if (missingResidentials.Count > 0)
                                                foreach (var missingResidential in missingResidentials)
                                                    errorMessage += $"{enfSrv}-{ctrlCd}: [R][{missingResidential.RecType}] lastUpdate: {missingResidential.dat_TrcRsp_Addr_LstUpdte} {missingResidential.dat_TrcRsp_Addr_Ln} missing<br />";
                                            if (missingEmployers.Count > 0)
                                                foreach (var missingEmployer in missingEmployers)
                                                    errorMessage += $"{enfSrv}-{ctrlCd}: [E][{missingEmployer.RecType}] lastUpdate: {missingEmployer.dat_TrcRsp_Addr_LstUpdte} {missingEmployer.dat_TrcRsp_Addr_Ln} missing<br />";
                                        }

                                    }

                                    if (!string.IsNullOrEmpty(errorMessage))
                                        ViewData["Error"] = $"Error in {baseNameAndExt}: <br />{errorMessage}";
                                    else
                                        ViewData["Message"] = $"{fullName}: All good.";

                                }
                                catch (Exception e)
                                {
                                    ViewData["Error"] = $"Error: {e.Message}";
                                }
                                finally
                                {
                                    await FoaeaAccess.SystemLogout();
                                }

                            }
                        }
                        else
                            ViewData["Error"] = $"Error: " + errors[0];

                    }
                }
                catch (Exception e)
                {
                    ViewData["Error"] = $"Error: " + e.Message;
                }
            }
        }

        private static bool CompareData(string fedCode, DateTime? receiptDate, FedTracing_RecType02 applResults,
                                        FedTracingFileBase tracingFileData,
                                        List<TraceResponseData> responses,
                                        out List<FedTracing_RecTypeResidential> missingResidentials,
                                        out List<FedTracing_RecTypeEmployer> missingEmployers)
        {
            bool foundAll = true;

            missingResidentials = new List<FedTracing_RecTypeResidential>();
            missingEmployers = new List<FedTracing_RecTypeEmployer>();

            foreach (var residentialKey in tracingFileData.TRCINResidentials.Keys)
            {
                var residentials = tracingFileData.TRCINResidentials[residentialKey].Where(n =>
                                                        n.dat_Appl_EnfSrvCd == applResults.dat_Appl_EnfSrvCd &&
                                                        n.dat_Appl_CtrlCd == applResults.dat_Appl_CtrlCd).ToList();
                foreach (var addressInfo in residentials)
                {
                    var matches = responses.Where(m =>
                            m.Appl_EnfSrv_Cd == addressInfo.dat_Appl_EnfSrvCd &&
                            m.Appl_CtrlCd == addressInfo.dat_Appl_CtrlCd &&
                            m.EnfSrv_Cd.Trim() == fedCode &&
                            m.TrcRsp_Rcpt_Dte.Date == receiptDate?.Date &&
                            SpecialStringCompare(m.TrcRsp_Addr_Ln, addressInfo.dat_TrcRsp_Addr_Ln) &&
                            SpecialStringCompare(m.TrcRsp_Addr_Ln1, addressInfo.dat_TrcRsp_Addr_Ln1) &&
                            SpecialStringCompare(m.TrcRsp_Addr_CityNme, addressInfo.dat_TrcRsp_Addr_CityNme) &&
                            m.TrcRsp_Addr_PrvCd == addressInfo.dat_TrcRsp_Addr_PrvCd &&
                            SpecialStringCompare(m.TrcRsp_Addr_CtryCd?.Trim(), addressInfo.dat_TrcRsp_Addr_CtryCd.Trim() == "CA" ? string.Empty : addressInfo.dat_TrcRsp_Addr_CtryCd.Trim()) &&
                            m.TrcRsp_Addr_PCd == addressInfo.dat_TrcRsp_Addr_PCd &&
                            //m.TrcRsp_Addr_LstUpdte?.Date == addressInfo.dat_TrcRsp_Addr_LstUpdte.Date &&
                            m.AddrTyp_Cd.Trim() == "R" &&
                            m.Prcs_RecType == addressInfo.RecType
                        );
                    if (!matches.Any())
                    {
                        foundAll = false;
                        missingResidentials.Add(addressInfo);
                    }

                }
            }

            foreach (var employerKey in tracingFileData.TRCINEmployers.Keys)
            {
                var employers = tracingFileData.TRCINEmployers[employerKey].Where(n =>
                                                        n.dat_Appl_EnfSrvCd == applResults.dat_Appl_EnfSrvCd &&
                                                        n.dat_Appl_CtrlCd == applResults.dat_Appl_CtrlCd).ToList();
                foreach (var addressInfo in employers)
                {

                    var matches = responses.Where(m =>
                            m.Appl_EnfSrv_Cd == addressInfo.dat_Appl_EnfSrvCd &&
                            m.Appl_CtrlCd == addressInfo.dat_Appl_CtrlCd &&
                            m.EnfSrv_Cd.Trim() == fedCode &&
                            m.TrcRsp_Rcpt_Dte.Date == receiptDate?.Date &&
                            SpecialStringCompare(m.TrcRsp_EmplNme, addressInfo.dat_TrcRcp_EmplNme) &&
                            SpecialStringCompare(m.TrcRsp_EmplNme1, addressInfo.dat_TrcRcp_EmplNme1) &&
                            SpecialStringCompare(m.TrcRsp_Addr_Ln, addressInfo.dat_TrcRsp_Addr_Ln) &&
                            SpecialStringCompare(m.TrcRsp_Addr_Ln1, addressInfo.dat_TrcRsp_Addr_Ln1) &&
                            SpecialStringCompare(m.TrcRsp_Addr_CityNme, addressInfo.dat_TrcRsp_Addr_CityNme) &&
                            m.TrcRsp_Addr_PrvCd == addressInfo.dat_TrcRsp_Addr_PrvCd &&
                            SpecialStringCompare(m.TrcRsp_Addr_CtryCd?.Trim(), addressInfo.dat_TrcRsp_Addr_CtryCd.Trim() == "CA" ? string.Empty : addressInfo.dat_TrcRsp_Addr_CtryCd.Trim()) &&
                            m.TrcRsp_Addr_PCd == addressInfo.dat_TrcRsp_Addr_PCd &&
                            //m.TrcRsp_Addr_LstUpdte?.Date == addressInfo.dat_TrcRsp_Addr_LstUpdte.Date &&
                            m.AddrTyp_Cd.Trim() == "E" &&
                            m.Prcs_RecType == addressInfo.RecType
                        );
                    if (!matches.Any())
                    {
                        foundAll = false;
                        missingEmployers.Add(addressInfo);
                    }
                }
            }

            return foundAll;
        }

        private static bool SpecialStringCompare(string s1, string s2)
        {
            if (string.IsNullOrWhiteSpace(s1) && string.IsNullOrWhiteSpace(s2))
                return true;

            if (s1 is null)
                return false;

            if (s2 is null)
                return false;

            if (s1.Replace("?", " ").Equals(s2.Replace((char)65533, ' '), StringComparison.InvariantCultureIgnoreCase))
                return true;

            if (s2.Contains((char)65533))
            {
                if (s1[..2] == s2[..2])
                    return true;
                else
                    return false;
            }

            return false;
        }

        private bool FileExists(out string fullName)
        {
            var incomingFileData = IncomingTraceFiles.Find(m => m.Name == FileName);
            string basePath = incomingFileData.Path;

            int cycleLength = 6;
            if (FileName.Equals("RC3STSIT", StringComparison.InvariantCultureIgnoreCase))
                cycleLength = 3;

            string cycle = TraceResponseCycle.PadLeft(cycleLength, '0');
            fullName = basePath + FileName + "." + cycle;
            if (incomingFileData.IsXML)
                fullName += ".XML";

            if (!System.IO.File.Exists(fullName))
                return false;
            else
                return true;
        }
    }
}
