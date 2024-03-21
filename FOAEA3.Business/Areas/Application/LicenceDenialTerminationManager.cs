using FOAEA3.Business.Security;
using System.Security.Claims;

namespace FOAEA3.Business.Areas.Application;

internal partial class LicenceDenialTerminationManager : ApplicationManager
{
    public LicenceDenialApplicationData LicenceDenialTerminationApplication { get; private set; }

    public LicenceDenialTerminationManager(LicenceDenialApplicationData licenceDenialTermination, IRepositories repositories, IFoaeaConfigurationHelper config, ClaimsPrincipal user) :
        base(licenceDenialTermination, repositories, config, user)
    {
        SetupLicenceDenialTermination(licenceDenialTermination);
    }

    public LicenceDenialTerminationManager(LicenceDenialApplicationData licenceDenialTermination, IRepositories repositories, IFoaeaConfigurationHelper config, FoaeaUser user) :
        base(licenceDenialTermination, repositories, config, user)
    {
        SetupLicenceDenialTermination(licenceDenialTermination);
    }

    public LicenceDenialTerminationManager(IRepositories repositories, IFoaeaConfigurationHelper config, ClaimsPrincipal user) :
        this(new LicenceDenialApplicationData(), repositories, config, user)
    {

    }

    public LicenceDenialTerminationManager(IRepositories repositories, IFoaeaConfigurationHelper config, FoaeaUser user) :
        this(new LicenceDenialApplicationData(), repositories, config, user)
    {

    }

    private void SetupLicenceDenialTermination(LicenceDenialApplicationData licenceDenialTermination)
    {
        LicenceDenialTerminationApplication = licenceDenialTermination;

        StateEngine.ValidStateChange[ApplicationState.INITIAL_STATE_0].Add(ApplicationState.APPLICATION_ACCEPTED_10);
    }

    public override async Task<bool> LoadApplication(string enfService, string controlCode)
    {
        // get data from Appl
        bool isSuccess = await base.LoadApplication(enfService, controlCode);

        if (isSuccess)
        {
            // get additional data from LicSusp table 
            var licenceDenialDB = DB.LicenceDenialTable;
            var data = await licenceDenialDB.GetLicenceDenialData(enfService, appl_L03_CtrlCd: controlCode);

            if (data != null)
                LicenceDenialTerminationApplication.Merge(data);
        }

        return isSuccess;
    }

    public async Task<bool> CreateApplication(string controlCodeForL01, DateTime requestDate)
    {
        if (!IsValidCategory("L03"))
            return false;

        // bool success = base.CreateApplication();
        var originalL01 = await GetOriginalLicenceDenialApplication(controlCodeForL01, requestDate);

        if (originalL01 is null)
            return false;

        SetL03ValuesBasedOnL01(originalL01, requestDate);

        bool success = await base.CreateApplication();

        var originalLicenceDenialManager = new LicenceDenialManager(originalL01, DB, Config, CurrentUser);

        if (!success)
        {
            var failedSubmitterManager = new FailedSubmitAuditManager(DB, LicenceDenialTerminationApplication);
            await failedSubmitterManager.AddToFailedSubmitAudit(FailedSubmitActivityAreaType.L03);
        }
        else if (LicenceDenialTerminationApplication.AppLiSt_Cd != ApplicationState.INVALID_APPLICATION_1)
        {
            originalL01.LicSusp_Dbtr_LastAddr_Ln = LicenceDenialTerminationApplication.LicSusp_Dbtr_LastAddr_Ln;
            originalL01.LicSusp_Dbtr_LastAddr_Ln1 = LicenceDenialTerminationApplication.LicSusp_Dbtr_LastAddr_Ln1;
            originalL01.LicSusp_Dbtr_LastAddr_CityNme = LicenceDenialTerminationApplication.LicSusp_Dbtr_LastAddr_CityNme;
            originalL01.LicSusp_Dbtr_LastAddr_PrvCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_LastAddr_PrvCd;
            originalL01.LicSusp_Dbtr_LastAddr_CtryCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_LastAddr_CtryCd;
            originalL01.LicSusp_Dbtr_LastAddr_PCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_LastAddr_PCd;

            bool isValid;
            string reasonText;

            string cityName = LicenceDenialTerminationApplication.LicSusp_Dbtr_LastAddr_CityNme;
            string provinceCode = LicenceDenialTerminationApplication.LicSusp_Dbtr_LastAddr_PrvCd;
            string postalCode = LicenceDenialTerminationApplication.LicSusp_Dbtr_LastAddr_PCd;
            string countryCode = LicenceDenialTerminationApplication.LicSusp_Dbtr_Brth_CtryCd;

            (isValid, reasonText) = await Validation.IsValidPostalCode(postalCode, provinceCode, cityName, countryCode);
            if (!isValid)
                EventManager.AddEvent(EventCode.C50772_INVALID_POSTAL_CODE, reasonText);

            originalL01.LicSusp_Appl_CtrlCd = LicenceDenialTerminationApplication.Appl_CtrlCd;
            originalL01.LicSusp_LiStCd = 14;

            await originalLicenceDenialManager.CancelApplication();
        }

        string msg = string.Empty;

        var activeL01s = await DB.LicenceDenialTable.GetActiveLO1ApplsForDebtor(Appl_EnfSrv_Cd, Appl_CtrlCd);
        foreach (var dr in activeL01s)
            msg += dr.Value + " ";

        if (msg.Trim().Length < 0)
            EventManager.AddEvent(EventCode.C50936_THERE_EXISTS_ONE_OR_MORE_ACTIVE_LICENCE_DENIAL_APPLICATIONS_FOR_THIS_DEBTOR_IN_YOUR_JURISDICTION, msg);

        // WARNING: is this needed? can the L03 be at any state other than 10 or maybe 1 but never 15?
        switch (LicenceDenialTerminationApplication.AppLiSt_Cd)
        {
            case ApplicationState.EXPIRED_15:
                EventManager.AddEvent(EventCode.C50860_APPLICATION_COMPLETED);
                break;
            case ApplicationState.APPLICATION_ACCEPTED_10:
                originalLicenceDenialManager.EventManager.AddEvent(EventCode.C50781_L03_ACCEPTED, queue: EventQueue.EventLicence, activeState: "X");
                EventManager.AddEvent(EventCode.C50780_APPLICATION_ACCEPTED);
                break;
            case ApplicationState.INVALID_APPLICATION_1:
                LicenceDenialTerminationApplication.AppLiSt_Cd = ApplicationState.APPLICATION_REJECTED_9;
                LicenceDenialTerminationApplication.ActvSt_Cd = "X";
                await DB.LicenceDenialTable.UpdateLicenceDenialData(LicenceDenialTerminationApplication);
                EventManager.AddEvent(EventCode.C51020_APPLICATION_UPDATED);
                break;
            default:
                // no event created
                break;
        }

        await originalLicenceDenialManager.EventManager.SaveEvents();
        await EventManager.SaveEvents();

        await DB.LicenceDenialTable.CloseSameDayLicenceEvent(originalL01.Appl_EnfSrv_Cd, originalL01.Appl_CtrlCd, Appl_CtrlCd);

        return success;
    }

    private async Task<LicenceDenialApplicationData> GetOriginalLicenceDenialApplication(string controlCodeForL01, DateTime requestDate)
    {
        var licenceDenialManager = new LicenceDenialManager(DB, Config, CurrentUser);
        if (!await licenceDenialManager.LoadApplication(LicenceDenialTerminationApplication.Appl_EnfSrv_Cd, controlCodeForL01))
        {
            LicenceDenialTerminationApplication.Messages.AddError("Cannot create an L03 for an L01 that doesn't exist.");
            return null;
        }

        var originalL01 = licenceDenialManager.LicenceDenialApplication;

        if (originalL01.AppLiSt_Cd.In(ApplicationState.APPLICATION_REJECTED_9, ApplicationState.MANUALLY_TERMINATED_14))
        {
            LicenceDenialTerminationApplication.Messages.AddError($"Cannot create an L03 for an L01 ({originalL01.Appl_CtrlCd}) that has been cancelled or rejected.");
            return null;
        }

        if (originalL01.AppCtgy_Cd != "L01")
        {
            LicenceDenialTerminationApplication.Messages.AddError($"Invalid category type ({originalL01.AppCtgy_Cd}). Expected L01.");
            return null;
        }

        if (!string.IsNullOrEmpty(LicenceDenialTerminationApplication.Appl_CtrlCd) && (await ApplicationExists()))
        {
            LicenceDenialTerminationApplication.Messages.AddError("Application already exists in database.");
            return null;
        }

        originalL01.LicSusp_TermRequestDte = requestDate;

        return originalL01;
    }

    public async Task<bool> ProcessLicenceDenialTerminationResponse(string appl_EnfSrv_Cd, string appl_CtrlCd)
    {
        if (!await LoadApplication(appl_EnfSrv_Cd, appl_CtrlCd))
        {
            LicenceDenialTerminationApplication.Messages.AddError(SystemMessage.APPLICATION_NOT_FOUND);
            return false;
        }

        if (!IsValidCategory("L03"))
            return false;

        if (LicenceDenialTerminationApplication.AppLiSt_Cd.NotIn(ApplicationState.APPLICATION_ACCEPTED_10, ApplicationState.PARTIALLY_SERVICED_12))
        {
            LicenceDenialTerminationApplication.Messages.AddError("Invalid State for the current application.  Valid states allowed are 10 and 12.");
            return false;
        }

        var lastResponse = await DB.LicenceDenialResponseTable.GetLastResponseData(appl_EnfSrv_Cd, appl_CtrlCd);

        LicenceDenialTerminationApplication.Appl_LastUpdate_Dte = DateTime.Now;
        LicenceDenialTerminationApplication.Appl_LastUpdate_Usr = DB.CurrentSubmitter;

        if (LicenceDenialTerminationApplication.AppLiSt_Cd == ApplicationState.APPLICATION_ACCEPTED_10)
        {
            LicenceDenialTerminationApplication.AppLiSt_Cd = ApplicationState.PARTIALLY_SERVICED_12;
        }
        else if (LicenceDenialTerminationApplication.AppLiSt_Cd == ApplicationState.PARTIALLY_SERVICED_12)
        {
            LicenceDenialTerminationApplication.AppLiSt_Cd = ApplicationState.EXPIRED_15;
            LicenceDenialTerminationApplication.ActvSt_Cd = "C";
        }

        await UpdateApplicationNoValidation();

        await DB.LicenceDenialTable.UpdateLicenceDenialData(LicenceDenialTerminationApplication);

        EventManager.AddEvent(EventCode.C50828_LICENSE_RESPONSE_RECEIVED, lastResponse.EnfSrv_Cd);

        await EventManager.SaveEvents();

        return (LicenceDenialTerminationApplication.AppLiSt_Cd == ApplicationState.EXPIRED_15);

    }

    protected override void TrimSpaces()
    {
        base.TrimSpaces();

        LicenceDenialTerminationApplication.LicSusp_CourtNme = LicenceDenialTerminationApplication.LicSusp_CourtNme?.Trim();
        LicenceDenialTerminationApplication.PymPr_Cd = LicenceDenialTerminationApplication.PymPr_Cd?.Trim();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplNme = LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplNme?.Trim();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_Ln = LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_Ln?.Trim();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_Ln1 = LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_Ln1?.Trim();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_CityNme = LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_CityNme?.Trim();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_PrvCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_PrvCd?.Trim();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_CtryCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_CtryCd?.Trim();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_PCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_PCd?.Trim();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EyesColorCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_EyesColorCd?.Trim();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_HeightUOMCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_HeightUOMCd?.Trim();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_Brth_CityNme = LicenceDenialTerminationApplication.LicSusp_Dbtr_Brth_CityNme?.Trim();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_Brth_CtryCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_Brth_CtryCd?.Trim();
    }

    public override void MakeUpperCase()
    {
        base.MakeUpperCase();

        LicenceDenialTerminationApplication.LicSusp_CourtNme = LicenceDenialTerminationApplication.LicSusp_CourtNme?.ToUpper();
        LicenceDenialTerminationApplication.PymPr_Cd = LicenceDenialTerminationApplication.PymPr_Cd?.ToUpper();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplNme = LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplNme?.ToUpper();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_Ln = LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_Ln?.ToUpper();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_Ln1 = LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_Ln1?.ToUpper();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_CityNme = LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_CityNme?.ToUpper();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_PrvCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_PrvCd?.ToUpper();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_CtryCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_CtryCd?.ToUpper();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_PCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_EmplAddr_PCd?.ToUpper();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_EyesColorCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_EyesColorCd?.ToUpper();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_HeightUOMCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_HeightUOMCd?.ToUpper();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_Brth_CityNme = LicenceDenialTerminationApplication.LicSusp_Dbtr_Brth_CityNme?.ToUpper();
        LicenceDenialTerminationApplication.LicSusp_Dbtr_Brth_CtryCd = LicenceDenialTerminationApplication.LicSusp_Dbtr_Brth_CtryCd?.ToUpper();
    }

    public async Task<bool> CancelApplication()
    {
        if (!IsValidCategory("I01"))
            return false;

        string appl_CommSubm_Text = LicenceDenialTerminationApplication.Appl_CommSubm_Text;

        // var newAppl_Source_RfrNr = LicenceDenialTerminationApplication.Appl_Source_RfrNr;
        var newAppl_Dbtr_Addr_Ln = LicenceDenialTerminationApplication.Appl_Dbtr_Addr_Ln;
        var newAppl_Dbtr_Addr_Ln1 = LicenceDenialTerminationApplication.Appl_Dbtr_Addr_Ln1;
        var newAppl_Dbtr_Addr_CityNme = LicenceDenialTerminationApplication.Appl_Dbtr_Addr_CityNme;
        var newAppl_Dbtr_Addr_PrvCd = LicenceDenialTerminationApplication.Appl_Dbtr_Addr_PrvCd;
        var newAppl_Dbtr_Addr_CtryCd = LicenceDenialTerminationApplication.Appl_Dbtr_Addr_CtryCd;
        var newAppl_Dbtr_Addr_PCd = LicenceDenialTerminationApplication.Appl_Dbtr_Addr_PCd;

        if (!await LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd))
        {
            LicenceDenialTerminationApplication.Messages.AddError($"No application was found in the database for {Appl_EnfSrv_Cd}-{Appl_CtrlCd}");
            return false;
        }

        LicenceDenialTerminationApplication.Appl_LastUpdate_Usr = DB.CurrentSubmitter;
        LicenceDenialTerminationApplication.Appl_LastUpdate_Dte = DateTime.Now;

        LicenceDenialTerminationApplication.Appl_CommSubm_Text = appl_CommSubm_Text ?? LicenceDenialTerminationApplication.Appl_CommSubm_Text;

        LicenceDenialTerminationApplication.Appl_Dbtr_Addr_Ln = newAppl_Dbtr_Addr_Ln;
        LicenceDenialTerminationApplication.Appl_Dbtr_Addr_Ln1 = newAppl_Dbtr_Addr_Ln1;
        LicenceDenialTerminationApplication.Appl_Dbtr_Addr_CityNme = newAppl_Dbtr_Addr_CityNme;
        LicenceDenialTerminationApplication.Appl_Dbtr_Addr_PrvCd = newAppl_Dbtr_Addr_PrvCd;
        LicenceDenialTerminationApplication.Appl_Dbtr_Addr_CtryCd = newAppl_Dbtr_Addr_CtryCd;
        LicenceDenialTerminationApplication.Appl_Dbtr_Addr_PCd = newAppl_Dbtr_Addr_PCd;

        if (LicenceDenialTerminationApplication.ActvSt_Cd != "A")
        {
            EventManager.AddEvent(EventCode.C50841_CAN_ONLY_CANCEL_AN_ACTIVE_APPLICATION, activeState: "C");
            await EventManager.SaveEvents();
            return false;
        }

        await SetNewStateTo(ApplicationState.MANUALLY_TERMINATED_14);

        await UpdateApplicationNoValidation();

        await EventManager.SaveEvents();

        if (LicenceDenialTerminationApplication.Medium_Cd != "FTP")
            LicenceDenialTerminationApplication.Messages.AddInformation(EventCode.C50620_VALID_APPLICATION);

        return true;
    }

    public override async Task UpdateApplication()
    {
        var current = new LicenceDenialTerminationManager(DB, Config, CurrentUser);
        await current.LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd);

        // keep these stored values
        LicenceDenialTerminationApplication.Appl_Create_Dte = current.LicenceDenialTerminationApplication.Appl_Create_Dte;
        LicenceDenialTerminationApplication.Appl_Create_Usr = current.LicenceDenialTerminationApplication.Appl_Create_Usr;

        DB.CurrentSubmitter = LicenceDenialTerminationApplication.Subm_SubmCd;

        await base.UpdateApplication();
    }

    private void SetL03ValuesBasedOnL01(LicenceDenialApplicationData originalL01, DateTime requestDate)
    {
        var newL03 = LicenceDenialTerminationApplication;

        newL03.LicSusp_TermRequestDte = requestDate;

        string appl_CommSubm_Text = LicenceDenialTerminationApplication.Appl_CommSubm_Text;
        if ((DB.CurrentSubmitter != originalL01.Subm_SubmCd) &&
            (newL03.Appl_EnfSrv_Cd.Trim() != originalL01.Appl_EnfSrv_Cd.Trim()))
        {
            appl_CommSubm_Text = $"*** LO3 submitted by {DB.CurrentSubmitter} ***\n" + appl_CommSubm_Text;
        }

        newL03.Appl_Dbtr_SurNme = originalL01.Appl_Dbtr_SurNme;
        newL03.Appl_Dbtr_FrstNme = originalL01.Appl_Dbtr_FrstNme;
        newL03.Appl_Dbtr_MddleNme = originalL01.Appl_Dbtr_MddleNme;
        newL03.Appl_Dbtr_Brth_Dte = originalL01.Appl_Dbtr_Brth_Dte;
        newL03.Appl_Dbtr_Gendr_Cd = originalL01.Appl_Dbtr_Gendr_Cd;
        newL03.Appl_Dbtr_Entrd_SIN = originalL01.Appl_Dbtr_Entrd_SIN;
        newL03.Appl_Dbtr_Cnfrmd_SIN = originalL01.Appl_Dbtr_Cnfrmd_SIN;

        switch (originalL01.AppLiSt_Cd)
        {
            case ApplicationState.INVALID_APPLICATION_1:
            case ApplicationState.SIN_CONFIRMATION_PENDING_3:
            case ApplicationState.SIN_NOT_CONFIRMED_5:
            case ApplicationState.PENDING_ACCEPTANCE_SWEARING_6:
            case ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7:
            case ApplicationState.APPLICATION_REJECTED_9:
                newL03.AppLiSt_Cd = ApplicationState.EXPIRED_15;
                newL03.ActvSt_Cd = "C";
                break;
            case ApplicationState.MANUALLY_TERMINATED_14:
                newL03.AppLiSt_Cd = ApplicationState.INVALID_APPLICATION_1;
                newL03.ActvSt_Cd = "A";
                break;
            case ApplicationState.APPLICATION_ACCEPTED_10:
            case ApplicationState.PARTIALLY_SERVICED_12:
                newL03.AppLiSt_Cd = ApplicationState.APPLICATION_ACCEPTED_10;
                newL03.ActvSt_Cd = "A";
                break;
        }

        newL03.Appl_SIN_Cnfrmd_Ind = 0;
        newL03.Appl_Dbtr_LngCd = "E";

        newL03.Appl_Dbtr_Addr_CityNme = null;
        newL03.Appl_Dbtr_Addr_CtryCd = null;
        newL03.Appl_Dbtr_Addr_Ln = null;
        newL03.Appl_Dbtr_Addr_Ln1 = null;
        newL03.Appl_Dbtr_Addr_PCd = null;
        newL03.Appl_Dbtr_Addr_PrvCd = null;
        newL03.Appl_RecvAffdvt_Dte = null;
        newL03.Appl_Reactv_Dte = null;
        newL03.Subm_Affdvt_SubmCd = null;
        newL03.Appl_JusticeNr = null;
        newL03.Appl_Group_Batch_Cd = null;
        newL03.Appl_Affdvt_DocTypCd = null;
        newL03.Appl_Dbtr_RtrndBySrc_SIN = null;

        newL03.Appl_LastUpdate_Usr = newL03.Subm_SubmCd;
        newL03.Appl_LastUpdate_Dte = DateTime.Now;

        newL03.Appl_Create_Usr = newL03.Subm_SubmCd;
        newL03.Appl_Create_Dte = DateTime.Now;

        newL03.Appl_CommSubm_Text = appl_CommSubm_Text;

        if (DB.CurrentSubmitter.IsInternalAgentSubmitter())
        {
            newL03.Medium_Cd = "PAP";
        }
        else
        {
            newL03.Medium_Cd = originalL01.Medium_Cd;
            newL03.Appl_Rcptfrm_Dte = DateTime.Now.Date;
        }

    }

}
