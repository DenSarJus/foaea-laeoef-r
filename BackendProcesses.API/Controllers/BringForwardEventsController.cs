using FOAEA3.Business.Areas.BackendProcesses;
using FOAEA3.Common.Helpers;
using FOAEA3.Model.Interfaces;
using FOAEA3.Model.Interfaces.Repository;
using Microsoft.AspNetCore.Mvc;
using Serilog;
using System;
using System.Threading.Tasks;

namespace BackendProcesses.API.Controllers
{
    [Route("api/v1/bringforwardevents")]
    [ApiController]
    public class BringForwardEventsController : Controller
    {
        private readonly IFoaeaConfigurationHelper config;

        public BringForwardEventsController()
        {
            var configHelper = new FoaeaConfigurationHelper();
            config = configHelper;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpGet("Version")]
        public ActionResult<string> Version() => Ok("BringForwardEvents API Version 1.4");

        [HttpPut("")]
        public async Task<ActionResult<string>> RunBringForward([FromServices] IRepositories repositories,
                                                                [FromServices] IRepositories_Finance repositoriesFinance)
        {
            repositories.CurrentSubmitter = "";

            ILogger log = Log.ForContext("APIpath", HttpContext.Request.Path.Value);
            log.Information("(PUT) method RunBringForward(): referer = {referer}", Request.Headers["Referer"]);

            var startTime = DateTime.Now;

            var bringForwardProcess = new BringForwardEventProcess(repositories, repositoriesFinance, config, User);
            await bringForwardProcess.Run();

            var endTime = DateTime.Now;

            var duration = endTime - startTime;

            return Ok($"{duration.Hours} hour(s) and {duration.Minutes} minute(s)");
        }
    }
}
