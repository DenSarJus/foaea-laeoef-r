using FOAEA3.Resources.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace FOAEA3.Admin.Web.Pages.Tools
{
    public class VerifyAPIsModel : PageModel
    {
        private const string LOCAL_HOST = "localhost";
        private const string FOAEA_DEV = "FOAEA DEV";
        private const string FOAEA_UAT = "FOAEA UAT";
        private const string PRODUCTION = "Production";

        [BindProperty]
        public string Environment { get; set; } = LOCAL_HOST;
        public string[] Environments = [LOCAL_HOST, FOAEA_DEV, FOAEA_UAT, PRODUCTION];

        public async Task OnPost()
        {
            if (ModelState.IsValid)
            {
                try
                {
                    string errorMessage = string.Empty;
                    string infoMessage = string.Empty;
                    string server = Environment switch
                    {
                        LOCAL_HOST => "localhost",
                        FOAEA_DEV => "%TEST_FOAEA_DEV_API_SERVER%".ReplaceVariablesWithEnvironmentValues(),
                        FOAEA_UAT => "%TEST_FOAEA_UAT_API_SERVER%".ReplaceVariablesWithEnvironmentValues(),
                        PRODUCTION => "%TEST_FOAEA_Production_API_SERVER%".ReplaceVariablesWithEnvironmentValues(),
                        _ => string.Empty,
                    };
                    ;

                    if (!string.IsNullOrEmpty(server))
                    {
                        string amountOwedPort = Environment != LOCAL_HOST ? "51054" : "51053";

                        string amountOwedApiVersionPath = $"https://{server}:{amountOwedPort}/api/v1/applicationsAmountOwed/Version";
                        string commonApiVersionPath = $"https://{server}:12011/api/v1/Applications/Version";
                        string interceptionApiVersionPath = $"https://{server}:12012/api/v1/Interceptions/Version";
                        string licenceDenialApiVersionPath = $"https://{server}:12013/api/v1/licenceDenials/version";
                        string tracingApiVersionPath = $"https://{server}:12014/api/v1/tracings/version";
                        string tracingApiPutPath = $"https://{server}:12014/api/v1/tracings/AA00-12345";

                        (infoMessage, errorMessage) = await CheckApiForGetVersion(amountOwedApiVersionPath, "Amount Owed", infoMessage, errorMessage);
                        (infoMessage, errorMessage) = await CheckApiForGetVersion(commonApiVersionPath, "Common", infoMessage, errorMessage);
                        (infoMessage, errorMessage) = await CheckApiForGetVersion(interceptionApiVersionPath, "Interception", infoMessage, errorMessage);
                        (infoMessage, errorMessage) = await CheckApiForGetVersion(tracingApiVersionPath, "Tracing", infoMessage, errorMessage);
                        (infoMessage, errorMessage) = await CheckApiForGetVersion(licenceDenialApiVersionPath, "Licence Denial", infoMessage, errorMessage);

                        string result = await ApiWorksForPUTmethod(tracingApiPutPath);
                        if (result == "401")
                            infoMessage += "Tracing PUT method API: GOOD<br />";
                        else
                            errorMessage += $"Tracing PUT method API: FAIL (status code: {result})<br />";
                    }
                    else
                    {
                        errorMessage = "Error: Missing/Unknown environment selected. <br \\>" +
                                       "You need the following environment variables set up for this to work: <br \\>" +
                                       "  TEST_FOAEA_DEV_API_SERVER, TEST_FOAEA_UAT_API_SERVER and TEST_FOAEA_Production_API_SERVER.";
                    }

                    if (!string.IsNullOrEmpty(infoMessage))
                        ViewData["Message"] = infoMessage;

                    if (!string.IsNullOrEmpty(errorMessage))
                        ViewData["Error"] = errorMessage;

                }
                catch (Exception e)
                {
                    ViewData["Error"] = "Error: " + e.Message;
                }
            }
        }

        private static async Task<(string infoMessage, string errorMessage)> CheckApiForGetVersion(
                                                                                string apiVersionPath, string description,
                                                                                string infoMessage, string errorMessage)
        {
            string version = await ApiWorksForGetVersion(apiVersionPath);

            if (!string.IsNullOrEmpty(version))
                infoMessage += $"{description} API: GOOD ({version})<br \\>";
            else
                errorMessage += $"{description} API: FAIL<br \\>";

            return (infoMessage, errorMessage);
        }

        private static async Task<string> ApiWorksForPUTmethod(string apiPutPath)
        {
            string result = string.Empty;

            using var httpClient = CreateHttpClient();

            string keyData = "{}";

            try
            {
                HttpResponseMessage callResult;
                using (var content = new StringContent(keyData, Encoding.UTF8, "application/json"))
                    callResult = await httpClient.PutAsync(apiPutPath, content);

                if (callResult != null)
                    result = ((int)callResult.StatusCode).ToString();
            }
            catch
            {
                // do nothing
            }

            return result;
        }

        private static async Task<string> ApiWorksForGetVersion(string amountOwedApiVersionPath)
        {
            return await GetString(amountOwedApiVersionPath);
        }

        private static async Task<string> GetString(string apiPath)
        {
            string result = string.Empty;

            using var httpClient = CreateHttpClient();

            try
            {
                var callResult = await httpClient.GetAsync(apiPath);

                if (callResult.IsSuccessStatusCode)
                {
                    result = await callResult.Content.ReadAsStringAsync();
                }
                else if (callResult.StatusCode == HttpStatusCode.Unauthorized)
                {
                    result = "401";
                }
            }
            catch
            {
                // do nothing
            }

            return result;

        }

        private static HttpClient CreateHttpClient()
        {
            var httpClient = new HttpClient
            {
                Timeout = new(0, 20, 30)
            };

            httpClient.DefaultRequestHeaders.Accept.Clear();
            httpClient.DefaultRequestHeaders.Add("CurrentSubmitter", "TEST");
            httpClient.DefaultRequestHeaders.Add("CurrentSubject", "TEST");

            return httpClient;
        }
    }
}
