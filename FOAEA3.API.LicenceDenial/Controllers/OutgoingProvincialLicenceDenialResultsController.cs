using FOAEA3.Business.Areas.Application;
using FOAEA3.Common;
using FOAEA3.Model;
using FOAEA3.Model.Constants;
using FOAEA3.Model.Interfaces.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FOAEA3.API.LicenceDenial.Controllers;

[Route("api/v1/[controller]")]
[ApiController]
public class OutgoingProvincialLicenceDenialResultsController : FoaeaControllerBase
{
    [HttpGet("Version")]
    public ActionResult<string> GetVersion() => Ok("OutgoingProvincialLicenceDenialResults API Version 1.0");

    [HttpGet("DB")]
    [Authorize(Roles = Roles.Admin)]
    public ActionResult<string> GetDatabase([FromServices] IRepositories repositories) => Ok(repositories.MainDB.ConnectionString);

    [HttpGet("")]
    public async Task<ActionResult<List<LicenceDenialOutgoingProvincialData>>> GetProvincialOutgoingData(
                                                            [FromQuery] int maxRecords,
                                                            [FromQuery] string activeState,
                                                            [FromQuery] string recipientCode,
                                                            [FromQuery] bool isXML,
                                                            [FromServices] IRepositories repositories)
    {
        var manager = new LicenceDenialManager(repositories, config, User);

        var data = await manager.GetProvincialOutgoingData(maxRecords, activeState, recipientCode, isXML);

        return Ok(data);
    }
}
