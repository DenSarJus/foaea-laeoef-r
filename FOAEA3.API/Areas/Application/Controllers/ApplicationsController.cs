using FOAEA3.API.Filters;
using FOAEA3.Business.Areas.Application;
using FOAEA3.Common;
using FOAEA3.Common.Helpers;
using FOAEA3.Model;
using FOAEA3.Model.Base;
using FOAEA3.Model.Constants;
using FOAEA3.Model.Interfaces.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FOAEA3.API.Areas.Application.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class ApplicationsController : FoaeaControllerBase
{
    [AllowAnonymous]
    [HttpGet("Version")]
    public ActionResult<string> GetVersion() => Ok("Applications API Version 1.0");

    [HttpGet("DB")]
    [Authorize(Roles = Roles.Admin)]
    public ActionResult<string> GetDatabase([FromServices] IRepositories repositories) => Ok(repositories.MainDB.ConnectionString);

    [HttpGet("{id}")]
    [HttpGet("{id}/friendly")]
    [Authorize(Policy = Policies.ApplicationReadAccess)]
    [ApplicationDataFriendlyResultFilter]
    public async Task<ActionResult<ApplicationData>> GetApplication(
                                [FromRoute] ApplKey id,
                                [FromServices] IRepositories repositories)
    {
        var appl = new ApplicationData();
        var applManager = new ApplicationManager(appl, repositories, config, User);

        if (await applManager.LoadApplication(id.EnfSrv, id.CtrlCd))
            return Ok(appl);
        else
            return NotFound();
    }

    [HttpPut("{id}/ValidateCoreValues")]
    public async Task<ActionResult<ApplicationData>> ValidateCoreValues([FromRoute] ApplKey id,
                                                            [FromServices] IRepositories repositories)
    {
        var appl = await APIBrokerHelper.GetDataFromRequestBody<InterceptionApplicationData>(Request);

        if ((id.CtrlCd != appl.Appl_CtrlCd) || (id.EnfSrv != appl.Appl_EnfSrv_Cd))
            return BadRequest("route enf.srv+control code does not match body enf.srv+control code");

        var currentUser = await UserHelper.ExtractDataFromUser(User, repositories);
        var applicationValidation = new ApplicationValidation(appl, repositories, config, currentUser);

        bool isValid = await applicationValidation.ValidateCodeValues();

        if (isValid)
            return Ok(appl);
        else
            return UnprocessableEntity(appl);
    }

    [HttpGet("{id}/SINresults")]
    public async Task<ActionResult<DataList<SINResultData>>> GetSINResults([FromRoute] ApplKey id,
                                                                    [FromServices] IRepositories repositories)
    {
        var appl = new ApplicationData();
        var applManager = new ApplicationManager(appl, repositories, config, User);
        var sinManager = new ApplicationSINManager(appl, applManager);

        if (await applManager.LoadApplication(id.EnfSrv, id.CtrlCd))
            return Ok(await sinManager.GetSINResults());
        else
            return NotFound();
    }

    [HttpGet("{id}/SINresultsWithHistory")]
    public async Task<ActionResult<DataList<SINResultWithHistoryData>>> GetSINResultsWithHistory([FromRoute] ApplKey id,
                                                                                [FromServices] IRepositories repositories)
    {
        var appl = new ApplicationData();
        var applManager = new ApplicationManager(appl, repositories, config, User);
        var sinManager = new ApplicationSINManager(appl, applManager);

        if (await applManager.LoadApplication(id.EnfSrv, id.CtrlCd))
            return Ok(await sinManager.GetSINResultsWithHistory());
        else
            return NotFound();
    }

    [HttpPut("{id}/SinConfirmation")]
    public async Task<ActionResult<ApplicationData>> SINconfirmation([FromRoute] ApplKey id,
                                                         [FromServices] IRepositories repositories,
                                                         [FromServices] IRepositories_Finance repositoriesFinance)
    {
        var sinConfirmationData = await APIBrokerHelper.GetDataFromRequestBody<SINConfirmationData>(Request);

        var application = new ApplicationData();

        var appManager = new ApplicationManager(application, repositories, config, User);
        await appManager.LoadApplication(id.EnfSrv, id.CtrlCd);

        ApplicationSINManager sinManager;

        switch (application.AppCtgy_Cd)
        {
            case "T01":
                var tracingManager = new TracingManager(repositories, config, User);
                await tracingManager.LoadApplication(id.EnfSrv, id.CtrlCd);
                sinManager = new ApplicationSINManager(tracingManager.TracingApplication, tracingManager);
                break;
            case "I01":
                var interceptionManager = new InterceptionManager(repositories, repositoriesFinance, config, User);
                await interceptionManager.LoadApplication(id.EnfSrv, id.CtrlCd);
                sinManager = new ApplicationSINManager(interceptionManager.InterceptionApplication, interceptionManager);
                break;
            case "L01":
                var licenceDenialManager = new LicenceDenialManager(repositories, config, User);
                await licenceDenialManager.LoadApplication(id.EnfSrv, id.CtrlCd);
                sinManager = new ApplicationSINManager(licenceDenialManager.LicenceDenialApplication, licenceDenialManager);
                break;
            default:
                sinManager = new ApplicationSINManager(application, appManager);
                break;
        }

        await sinManager.SINconfirmation(isSinConfirmed: sinConfirmationData.IsSinConfirmed,
                                         confirmedSin: sinConfirmationData.ConfirmedSIN,
                                         lastUpdateUser: repositories.CurrentSubmitter);

        return Ok(application);
    }

    [HttpGet("ConfirmedSIN")]
    public async Task<ActionResult<List<ApplicationData>>> GetApplicationsForConfirmedSin([FromServices] IRepositories repositories)
    {
        string confirmedSin = (await APIBrokerHelper.GetDataFromRequestBody<StringData>(Request)).Data;

        var appManager = new ApplicationManager(new ApplicationData(), repositories, config, User);

        return Ok(await appManager.GetApplicationsForSin(confirmedSin));
    }

    [HttpGet("Stats")]
    public async Task<ActionResult<List<StatsOutgoingProvincialData>>> GetOutgoingProvincialStatusData([FromServices] IRepositories repositories,
                                                                                                       [FromQuery] int maxRecords,
                                                                                                       [FromQuery] string activeState,
                                                                                                       [FromQuery] string recipientCode)
    {
        var appl = new ApplicationData();
        var applManager = new ApplicationManager(appl, repositories, config, User);

        return await applManager.GetProvincialStatsOutgoingData(maxRecords, activeState, recipientCode);
    }

}
