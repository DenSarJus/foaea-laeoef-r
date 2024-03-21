using FOAEA3.Business.Areas.BackendProcesses;
using FOAEA3.Model;
using FOAEA3.Model.Interfaces.Repository;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace BackendProcess.API.Controllers
{
    [Route("api/v1/applicationsAmountOwed")]
    [ApiController]
    public class ApplicationsAmountOwedController : ControllerBase
    {
        [HttpGet("Version")]
        public ActionResult<string> GetVersion() => Ok($"Applications Amount Owed API Version 1.5");

        [HttpGet("DB")]
        public ActionResult<string> GetDatabase([FromServices] IRepositories repositories) => Ok(repositories.MainDB.ConnectionString);

        [HttpPut("")]
        public async Task<ActionResult<string>> RunAmountOwed([FromServices] IRepositories repositories, [FromServices] IRepositories_Finance repositoriesFinance)
        {
            repositories.CurrentSubmitter = "";

            var startTime = DateTime.Now;

            var amountOwedProcess = new AmountOwedProcess(repositories, repositoriesFinance);
            await amountOwedProcess.Run();

            var endTime = DateTime.Now;

            var duration = endTime - startTime;

            return Ok($"{duration.Hours} hour(s) and {duration.Minutes} minute(s)");
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<SummonsSummaryData>> CalculateAmountOwed(string id, [FromServices] IRepositories repositories, [FromServices] IRepositories_Finance repositoriesFinance)
        {
            repositories.CurrentSubmitter = "";

            string[] values = id.Split("-");
            if (values.Length == 2)
            {
                string enfSrv = values[0];
                string ctrlCd = values[1];

                var amountOwedProcess = new AmountOwedProcess(repositories, repositoriesFinance);
                var (summSmryNewData, _) = await amountOwedProcess.CalculateAndUpdateAmountOwedForVariation(enfSrv, ctrlCd);

                if (summSmryNewData != null)
                {
                    if (!Response.Headers.ContainsKey("get-amount-owed"))
                        Response.Headers.Append("get-amount-owed", "GET " + HttpContext.Request.Path.Value);

                    return Ok(summSmryNewData);
                }
                else
                    return NotFound();
            }
            else
            {
                return BadRequest();
            }
        }
    }
}
