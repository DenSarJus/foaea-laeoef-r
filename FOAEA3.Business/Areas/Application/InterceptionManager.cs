using FOAEA3.Business.Areas.BackendProcesses;
using System.Security.Claims;

namespace FOAEA3.Business.Areas.Application;

internal partial class InterceptionManager : ApplicationManager
{
    private const string I01_AFFITDAVIT_DOCUMENT_CODE = "IXX";
    private const string AUTO_REJECT_EXPIRED_TIME_FOR_VARIATION = "BFEventsProcessing Case 50896";

    public InterceptionApplicationData InterceptionApplication { get; private set; }
    private IRepositories_Finance DBfinance { get; set; }
    private InterceptionValidation InterceptionValidation { get; set; }
    public bool? AcceptedWithin30Days { get; set; } = null;
    public bool ESDReceived { get; set; } = true;
    public DateTime? GarnisheeSummonsReceiptDate { get; set; }

    private int nextJusticeID_callCount = 0;

    private enum VariationDocumentAction
    {
        AcceptVariationDocument,
        RejectVariationDocument
    }

    private VariationDocumentAction VariationAction { get; set; }

    public InterceptionManager(IRepositories repositories, IRepositories_Finance repositoriesFinance, IFoaeaConfigurationHelper config, ClaimsPrincipal user) :
        this(new InterceptionApplicationData(), repositories, repositoriesFinance, config, user)
    {

    }

    public InterceptionManager(InterceptionApplicationData interception, IRepositories repositories,
                               IRepositories_Finance repositoriesFinance, IFoaeaConfigurationHelper config, ClaimsPrincipal user) :
        base(interception, repositories, config, user, new InterceptionValidation(interception, repositories, config, null))
    {
        SetupInterception(interception, repositoriesFinance);
    }

    public InterceptionManager(IRepositories repositories, IRepositories_Finance repositoriesFinance, IFoaeaConfigurationHelper config, FoaeaUser user) :
        this(new InterceptionApplicationData(), repositories, repositoriesFinance, config, user)
    {

    }

    public InterceptionManager(InterceptionApplicationData interception, IRepositories repositories,
                               IRepositories_Finance repositoriesFinance, IFoaeaConfigurationHelper config, FoaeaUser user) :
        base(interception, repositories, config, user, new InterceptionValidation(interception, repositories, config, null))
    {
        SetupInterception(interception, repositoriesFinance);
    }

    private void SetupInterception(InterceptionApplicationData interception, IRepositories_Finance repositoriesFinance)
    {
        InterceptionApplication = interception;
        InterceptionValidation = Validation as InterceptionValidation;
        DBfinance = repositoriesFinance;

        // add Interception-specific state changes

        StateEngine.ValidStateChange[ApplicationState.APPLICATION_ACCEPTED_10].Add(ApplicationState.FINANCIAL_TERMS_VARIED_17);
        StateEngine.ValidStateChange[ApplicationState.APPLICATION_ACCEPTED_10].Add(ApplicationState.APPLICATION_SUSPENDED_35);

        StateEngine.ValidStateChange[ApplicationState.PARTIALLY_SERVICED_12].Add(ApplicationState.FINANCIAL_TERMS_VARIED_17);
        StateEngine.ValidStateChange[ApplicationState.PARTIALLY_SERVICED_12].Add(ApplicationState.APPLICATION_SUSPENDED_35);

        StateEngine.ValidStateChange.Add(ApplicationState.FINANCIAL_TERMS_VARIED_17, new List<ApplicationState> {
                        ApplicationState.INVALID_VARIATION_SOURCE_91,
                        ApplicationState.INVALID_VARIATION_FINTERMS_92,
                        ApplicationState.VALID_FINANCIAL_VARIATION_93
                    });

        StateEngine.ValidStateChange.Add(ApplicationState.AWAITING_DOCUMENTS_FOR_VARIATION_19, new List<ApplicationState> {
                        ApplicationState.PARTIALLY_SERVICED_12,
                        ApplicationState.MANUALLY_TERMINATED_14,
                        ApplicationState.EXPIRED_15,
                        ApplicationState.APPLICATION_SUSPENDED_35
                    });

        StateEngine.ValidStateChange.Add(ApplicationState.APPLICATION_SUSPENDED_35, new List<ApplicationState> {
                        ApplicationState.MANUALLY_TERMINATED_14,
                        ApplicationState.EXPIRED_15,
                        ApplicationState.FINANCIAL_TERMS_VARIED_17
                    });

        StateEngine.ValidStateChange.Add(ApplicationState.INVALID_VARIATION_SOURCE_91, new List<ApplicationState> {
                        ApplicationState.VALID_FINANCIAL_VARIATION_93
                    });

        StateEngine.ValidStateChange.Add(ApplicationState.INVALID_VARIATION_FINTERMS_92, new List<ApplicationState> {
                        ApplicationState.VALID_FINANCIAL_VARIATION_93
                    });

        StateEngine.ValidStateChange.Add(ApplicationState.VALID_FINANCIAL_VARIATION_93, new List<ApplicationState> {
                        ApplicationState.PARTIALLY_SERVICED_12,
                        ApplicationState.AWAITING_DOCUMENTS_FOR_VARIATION_19
                    });
    }

    public async Task<bool> LoadApplication(string enfService, string controlCode, bool loadFinancials)
    {
        bool isSuccess = await base.LoadApplication(enfService, controlCode);

        InterceptionApplication.IntFinH = null;
        InterceptionApplication.HldbCnd = null;
        if (isSuccess && loadFinancials)
        {
            string activeState = "A";
            if (InterceptionApplication.AppLiSt_Cd == ApplicationState.SIN_CONFIRMATION_PENDING_3)
                activeState = "P";
            var finTerms = await DB.InterceptionTable.GetInterceptionFinancialTerms(enfService, controlCode, activeState);
            InterceptionApplication.IntFinH = finTerms;

            if (finTerms != null)
            {
                var holdbackConditions = await DB.InterceptionTable.GetHoldbackConditions(enfService, controlCode,
                                                                                               finTerms.IntFinH_Dte, activeState);
                holdbackConditions ??= new List<HoldbackConditionData>();
                InterceptionApplication.HldbCnd = holdbackConditions;
            }
        }

        return isSuccess;
    }

    public override async Task<bool> LoadApplication(string enfService, string controlCode)
    {
        // get data from Appl
        return await LoadApplication(enfService, controlCode, loadFinancials: true);
    }

    public override async Task<bool> CreateApplication()
    {
        if (!IsValidCategory("I01"))
            return false;

        if (!InterceptionValidation.ValidFinancialTermsMandatoryData())
            return false;

        bool success = await base.CreateApplication();
        if (success)
        {
            InterceptionApplication.IntFinH.Appl_EnfSrv_Cd = InterceptionApplication.Appl_EnfSrv_Cd;
            InterceptionApplication.IntFinH.Appl_CtrlCd = InterceptionApplication.Appl_CtrlCd;
            InterceptionApplication.IntFinH.ActvSt_Cd = "P";
            InterceptionApplication.IntFinH.IntFinH_VarIss_Dte = null;

            var finTermsDate = DateTime.Now;
            if (InterceptionApplication.IntFinH.IntFinH_Dte == DateTime.MinValue)
                InterceptionApplication.IntFinH.IntFinH_Dte = finTermsDate;
            else
                finTermsDate = InterceptionApplication.IntFinH.IntFinH_Dte;

            foreach (var sourceSpecificHoldback in InterceptionApplication.HldbCnd)
            {
                sourceSpecificHoldback.Appl_EnfSrv_Cd = InterceptionApplication.Appl_EnfSrv_Cd;
                sourceSpecificHoldback.Appl_CtrlCd = InterceptionApplication.Appl_CtrlCd;
                sourceSpecificHoldback.ActvSt_Cd = "P";

                if (sourceSpecificHoldback.IntFinH_Dte == DateTime.MinValue)
                    sourceSpecificHoldback.IntFinH_Dte = finTermsDate;
            }

            if (InterceptionApplication.IntFinH.IntFinH_PerPym_Money.HasValue &&
                (InterceptionApplication.IntFinH.IntFinH_PerPym_Money.Value == 0))
                InterceptionApplication.IntFinH.IntFinH_PerPym_Money = null;

            if (InterceptionApplication.IntFinH.IntFinH_CmlPrPym_Ind.HasValue &&
                (InterceptionApplication.IntFinH.IntFinH_CmlPrPym_Ind.Value != 1))
                EventManager.AddEvent(EventCode.C51114_FINANCIAL_TERMS_INCLUDE_NONCUMULATIVE_PERIODIC_PAYMENTS);

            await DB.InterceptionTable.CreateInterceptionFinancialTerms(InterceptionApplication.IntFinH);

            await DB.InterceptionTable.CreateHoldbackConditions(InterceptionApplication.HldbCnd);

            if (InterceptionApplication.Medium_Cd != "FTP") InterceptionApplication.Messages.AddInformation(EventCode.C50620_VALID_APPLICATION);

            await IncrementGarnSmry(isNewApplication: true);

            if (Config.ESDsites.Contains(Appl_EnfSrv_Cd) && (InterceptionApplication.Medium_Cd == "FTP"))
                await DB.InterceptionTable.InsertESDrequired(Appl_EnfSrv_Cd, Appl_CtrlCd, ESDrequired.OriginalESDrequired);

            await EventManager.SaveEvents();

        }

        return success;
    }

    public override async Task UpdateApplication()
    {
        InterceptionApplication.Appl_LastUpdate_Usr = DB.UpdateSubmitter;
        InterceptionApplication.Appl_LastUpdate_Dte = DateTime.Now;

        bool success = true;
        if (await FinancialTermsHaveBeenModified())
        {
            success = await VaryApplication();
            if (success && (!await FinancialTermsAreHigher()))
            {
                await AcceptVariation();
            }
        }

        if (success)
            await base.UpdateApplication();
    }

    public async Task UpdateApplicationNoValidationNoFinTerms()
    {
        await base.UpdateApplicationNoValidation();
    }

    public override async Task UpdateApplicationNoValidation()
    {
        await base.UpdateApplicationNoValidation();

        if ((InterceptionApplication.IntFinH is not null) && (InterceptionApplication.IntFinH.IntFinH_Dte != DateTime.MinValue))
        {
            await DB.InterceptionTable.UpdateInterceptionFinancialTerms(InterceptionApplication.IntFinH);

            if ((InterceptionApplication.HldbCnd is not null) && (InterceptionApplication.HldbCnd.Count > 0))
                await DB.InterceptionTable.UpdateHoldbackConditions(InterceptionApplication.HldbCnd);
        }
    }

    public async Task<List<SummonsSummaryData>> GetSummonsSummary(string appl_EnfSrv_Cd = "", string appl_CtrlCd = "", string debtorId = "")
    {
        return await DBfinance.SummonsSummaryRepository.GetSummonsSummary(appl_EnfSrv_Cd, appl_CtrlCd, debtorId);
    }

    public async Task<bool> VaryApplication()
    {
        if (!IsValidCategory("I01"))
            return false;

        // only keep changes that are allowed:
        //   comment, variation issue date and financial terms

        var newIntFinH = InterceptionApplication.IntFinH;
        var newHldbCnd = InterceptionApplication.HldbCnd;

        var newAppl_Source_RfrNr = InterceptionApplication.Appl_Source_RfrNr;
        var variationIssueDate = InterceptionApplication.Appl_Lgl_Dte;
        string appl_CommSubm_Text = InterceptionApplication.Appl_CommSubm_Text;

        var newAppl_Dbtr_Addr_Ln = InterceptionApplication.Appl_Dbtr_Addr_Ln;
        var newAppl_Dbtr_Addr_Ln1 = InterceptionApplication.Appl_Dbtr_Addr_Ln1;
        var newAppl_Dbtr_Addr_CityNme = InterceptionApplication.Appl_Dbtr_Addr_CityNme;
        var newAppl_Dbtr_Addr_PrvCd = InterceptionApplication.Appl_Dbtr_Addr_PrvCd;
        var newAppl_Dbtr_Addr_CtryCd = InterceptionApplication.Appl_Dbtr_Addr_CtryCd;
        var newAppl_Dbtr_Addr_PCd = InterceptionApplication.Appl_Dbtr_Addr_PCd;

        if (!await LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd, loadFinancials: false))
        {
            EventManager.AddEvent(EventCode.C55000_INVALID_VARIATION);
            await EventManager.SaveEvents();

            return false;
        }

        InterceptionApplication.Appl_LastUpdate_Usr = DB.CurrentSubmitter;
        InterceptionApplication.Appl_LastUpdate_Dte = DateTime.Now;

        InterceptionApplication.IntFinH = newIntFinH;
        InterceptionApplication.HldbCnd = newHldbCnd;

        InterceptionApplication.Appl_Source_RfrNr = newAppl_Source_RfrNr;
        InterceptionApplication.Appl_Lgl_Dte = variationIssueDate;
        InterceptionApplication.Appl_CommSubm_Text = appl_CommSubm_Text ?? InterceptionApplication.Appl_CommSubm_Text;

        if (InterceptionApplication.Medium_Cd == "FTP")
            InterceptionApplication.Appl_LastUpdate_Usr = "FO2SSS";

        InterceptionApplication.Appl_Dbtr_Addr_Ln = newAppl_Dbtr_Addr_Ln;
        InterceptionApplication.Appl_Dbtr_Addr_Ln1 = newAppl_Dbtr_Addr_Ln1;
        InterceptionApplication.Appl_Dbtr_Addr_CityNme = newAppl_Dbtr_Addr_CityNme;
        InterceptionApplication.Appl_Dbtr_Addr_PrvCd = newAppl_Dbtr_Addr_PrvCd;
        InterceptionApplication.Appl_Dbtr_Addr_CtryCd = newAppl_Dbtr_Addr_CtryCd;
        InterceptionApplication.Appl_Dbtr_Addr_PCd = newAppl_Dbtr_Addr_PCd;

        if (variationIssueDate < DateTime.Now.Date.AddDays(-30))
        {
            InterceptionApplication.Messages.AddError(ErrorResource.INVALID_VARIATIONS_ISSUE_DATE);
            return false;
        }

        var summSmry = (await DBfinance.SummonsSummaryRepository.GetSummonsSummary(Appl_EnfSrv_Cd, Appl_CtrlCd)).FirstOrDefault();
        if (summSmry is null)
        {
            await AddSystemError(DB, InterceptionApplication.Messages, Config.Recipients.EmailRecipients,
                           $"No summsmry record was found for {Appl_EnfSrv_Cd}-{Appl_CtrlCd}. Cannot vary.");
            return false;
        }

        if (summSmry.Start_Dte >= DateTime.Now)
        {
            EventManager.AddEvent(EventCode.C50881_CANNOT_VARY_TERMS_AT_THIS_TIME);
            await EventManager.SaveEvents();

            return false;
        }

        var currentInterceptionManager = new InterceptionManager(DB, DBfinance, Config, CurrentUser);
        await currentInterceptionManager.LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd, loadFinancials: true);
        var currentInterceptionApplication = currentInterceptionManager.InterceptionApplication;

        if (!StateEngine.IsValidStateChange(currentInterceptionManager.InterceptionApplication.AppLiSt_Cd, ApplicationState.FINANCIAL_TERMS_VARIED_17))
        {
            InvalidStateChange(currentInterceptionManager.InterceptionApplication.AppLiSt_Cd, ApplicationState.FINANCIAL_TERMS_VARIED_17);
            await EventManager.SaveEvents();
            return false;
        }

        if (!InterceptionValidation.ValidNewFinancialTerms(currentInterceptionApplication))
            return false;

        InterceptionApplication.IntFinH.ActvSt_Cd = "P";
        InterceptionApplication.IntFinH.IntFinH_LiStCd = 17;
        InterceptionApplication.IntFinH.Appl_CtrlCd = Appl_CtrlCd;
        InterceptionApplication.IntFinH.Appl_EnfSrv_Cd = Appl_EnfSrv_Cd;

        foreach (var sourceSpecific in InterceptionApplication.HldbCnd)
        {
            sourceSpecific.ActvSt_Cd = "P";
            sourceSpecific.HldbCnd_LiStCd = 17;
            sourceSpecific.Appl_CtrlCd = Appl_CtrlCd;
            sourceSpecific.Appl_EnfSrv_Cd = Appl_EnfSrv_Cd;
        }

        if (InterceptionApplication.IntFinH.IntFinH_CmlPrPym_Ind.HasValue &&
            (InterceptionApplication.IntFinH.IntFinH_CmlPrPym_Ind.Value != 1))
        {
            EventManager.AddEvent(EventCode.C51114_FINANCIAL_TERMS_INCLUDE_NONCUMULATIVE_PERIODIC_PAYMENTS);
        }

        await SetNewStateTo(ApplicationState.FINANCIAL_TERMS_VARIED_17);

        if (InterceptionApplication.AppLiSt_Cd.NotIn(ApplicationState.INVALID_VARIATION_SOURCE_91,
                                                     ApplicationState.INVALID_VARIATION_FINTERMS_92))
        {
            await DB.InterceptionTable.CreateInterceptionFinancialTerms(InterceptionApplication.IntFinH);
            await DB.InterceptionTable.CreateHoldbackConditions(InterceptionApplication.HldbCnd);

            await UpdateApplicationNoValidationNoFinTerms();

            await EventManager.SaveEvents();

            if (InterceptionApplication.Medium_Cd != "FTP")
                InterceptionApplication.Messages.AddInformation(EventCode.C50620_VALID_APPLICATION);

            return true;
        }
        else
        {
            switch (InterceptionApplication.AppLiSt_Cd)
            {
                case ApplicationState.INVALID_VARIATION_SOURCE_91:
                    if (InterceptionApplication.Medium_Cd != "FTP")
                        InterceptionApplication.Messages.AddError(EventCode.C55002_INVALID_FINANCIAL_TERMS);
                    break;

                case ApplicationState.INVALID_VARIATION_FINTERMS_92:
                    if (InterceptionApplication.Medium_Cd != "FTP")
                        InterceptionApplication.Messages.AddError(EventCode.C55001_INVALID_SOURCE_HOLDBACK);
                    break;

                default:
                    if (InterceptionApplication.Medium_Cd != "FTP")
                        InterceptionApplication.Messages.AddError(EventCode.C55000_INVALID_VARIATION);
                    break;
            }

            await EventManager.SaveEvents();

            return false;
        }

    }

    public async Task<List<SummonsSummaryData>> GetFixedAmountRecalcDateRecords()
    {
        return await DBfinance.SummonsSummaryRepository.GetFixedAmountRecalcDateRecords();
    }

    public async Task FullyServiceApplication()
    {
        var applicationManagerCopy = new InterceptionManager(DB, DBfinance, Config, CurrentUser);

        if (!await applicationManagerCopy.LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd, loadFinancials: false))
        {
            string key = ApplKey.MakeKey(Appl_EnfSrv_Cd, Appl_CtrlCd);
            InterceptionApplication.Messages.AddError($"Application {key} does not exists");
            return;
        }

        await SetNewStateTo(ApplicationState.FULLY_SERVICED_13);

        await UpdateApplicationNoValidation();

        await EventManager.SaveEvents();
    }

    public async Task<bool> CancelApplication()
    {
        if (!IsValidCategory("I01"))
            return false;

        string appl_CommSubm_Text = InterceptionApplication.Appl_CommSubm_Text;
        var newIntFinH = InterceptionApplication.IntFinH;
        var newHldbCnd = InterceptionApplication.HldbCnd;

        // var newAppl_Source_RfrNr = InterceptionApplication.Appl_Source_RfrNr;
        var newAppl_Dbtr_Addr_Ln = InterceptionApplication.Appl_Dbtr_Addr_Ln;
        var newAppl_Dbtr_Addr_Ln1 = InterceptionApplication.Appl_Dbtr_Addr_Ln1;
        var newAppl_Dbtr_Addr_CityNme = InterceptionApplication.Appl_Dbtr_Addr_CityNme;
        var newAppl_Dbtr_Addr_PrvCd = InterceptionApplication.Appl_Dbtr_Addr_PrvCd;
        var newAppl_Dbtr_Addr_CtryCd = InterceptionApplication.Appl_Dbtr_Addr_CtryCd;
        var newAppl_Dbtr_Addr_PCd = InterceptionApplication.Appl_Dbtr_Addr_PCd;

        if (!await LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd, loadFinancials: false))
        {
            InterceptionApplication.Messages.AddError($"No application was found in the database for {Appl_EnfSrv_Cd}-{Appl_CtrlCd}");
            return false;
        }

        InterceptionApplication.Appl_LastUpdate_Usr = DB.UpdateSubmitter;
        InterceptionApplication.Appl_LastUpdate_Dte = DateTime.Now;

        InterceptionApplication.Appl_CommSubm_Text = appl_CommSubm_Text ?? InterceptionApplication.Appl_CommSubm_Text;
        InterceptionApplication.IntFinH = newIntFinH;
        InterceptionApplication.HldbCnd = newHldbCnd;

        //InterceptionApplication.Appl_Source_RfrNr = newAppl_Source_RfrNr;
        InterceptionApplication.Appl_Dbtr_Addr_Ln = newAppl_Dbtr_Addr_Ln;
        InterceptionApplication.Appl_Dbtr_Addr_Ln1 = newAppl_Dbtr_Addr_Ln1;
        InterceptionApplication.Appl_Dbtr_Addr_CityNme = newAppl_Dbtr_Addr_CityNme;
        InterceptionApplication.Appl_Dbtr_Addr_PrvCd = newAppl_Dbtr_Addr_PrvCd;
        InterceptionApplication.Appl_Dbtr_Addr_CtryCd = newAppl_Dbtr_Addr_CtryCd;
        InterceptionApplication.Appl_Dbtr_Addr_PCd = newAppl_Dbtr_Addr_PCd;

        if (InterceptionApplication.ActvSt_Cd != "A")
        {
            EventManager.AddEvent(EventCode.C50841_CAN_ONLY_CANCEL_AN_ACTIVE_APPLICATION, activeState: "C");
            await EventManager.SaveEvents();
            return false;
        }

        await SetNewStateTo(ApplicationState.MANUALLY_TERMINATED_14);

        await UpdateApplicationNoValidation();

        await EventManager.SaveEvents();

        if (InterceptionApplication.Medium_Cd != "FTP")
            InterceptionApplication.Messages.AddInformation(EventCode.C50620_VALID_APPLICATION);

        return true;
    }

    public async Task<bool> CompleteApplication()
    {
        if (!IsValidCategory("I01"))
            return false;

        if (!await LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd, loadFinancials: false))
        {
            InterceptionApplication.Messages.AddError($"No application was found in the database for {Appl_EnfSrv_Cd}-{Appl_CtrlCd}");
            return false;
        }

        InterceptionApplication.Appl_LastUpdate_Usr = DB.UpdateSubmitter;
        InterceptionApplication.Appl_LastUpdate_Dte = DateTime.Now;

        await SetNewStateTo(ApplicationState.EXPIRED_15);

        await UpdateApplicationNoValidation();

        await EventManager.SaveEvents();

        if (InterceptionApplication.Medium_Cd != "FTP")
            InterceptionApplication.Messages.AddInformation(EventCode.C50620_VALID_APPLICATION);

        return true;
    }

    public async Task<bool> SuspendApplication()
    {
        if (!IsValidCategory("I01"))
            return false;

        if (!await LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd, loadFinancials: false))
        {
            InterceptionApplication.Messages.AddError($"No application was found in the database for {Appl_EnfSrv_Cd}-{Appl_CtrlCd}");
            return false;
        }

        InterceptionApplication.Appl_LastUpdate_Usr = DB.CurrentSubmitter;
        InterceptionApplication.Appl_LastUpdate_Dte = DateTime.Now;

        if (InterceptionApplication.AppLiSt_Cd == ApplicationState.AWAITING_DOCUMENTS_FOR_VARIATION_19)
            await DeletePendingFinancialInformation();

        await SetNewStateTo(ApplicationState.APPLICATION_SUSPENDED_35);

        await UpdateApplicationNoValidation();

        await IncrementGarnSmry();

        await DB.InterceptionTable.EISOHistoryDeleteBySIN(InterceptionApplication.Appl_Dbtr_Cnfrmd_SIN, false);

        await EventManager.SaveEvents();

        if (InterceptionApplication.Medium_Cd != "FTP") InterceptionApplication.Messages.AddInformation(EventCode.C50620_VALID_APPLICATION);

        return true;
    }

    private async Task DeletePendingFinancialInformation()
    {
        var pendingIntFinHs = await DB.InterceptionTable.GetAllInterceptionFinancialTerms(Appl_EnfSrv_Cd, Appl_CtrlCd);
        var pendingHoldbacks = await DB.InterceptionTable.GetAllHoldbackConditions(Appl_EnfSrv_Cd, Appl_CtrlCd);

        foreach (var pendingIntFinH in pendingIntFinHs)
            if (pendingIntFinH.ActvSt_Cd == "P")
                await DB.InterceptionTable.DeleteInterceptionFinancialTerms(pendingIntFinH);

        foreach (var pendingHoldback in pendingHoldbacks)
            if (pendingHoldback.ActvSt_Cd == "P")
                await DB.InterceptionTable.DeleteHoldbackCondition(pendingHoldback);
    }

    public async Task<bool> AcceptInterception(DateTime supportingDocsDate)
    {
        // only keep changes that are allowed:
        //   Source Reference Number, comment and address fields

        string appl_Source_RfrNr = InterceptionApplication.Appl_Source_RfrNr;
        string appl_CommSubm_Text = InterceptionApplication.Appl_CommSubm_Text;
        string appl_Dbtr_Addr_Ln = InterceptionApplication.Appl_Dbtr_Addr_Ln;
        string appl_Dbtr_Addr_Ln1 = InterceptionApplication.Appl_Dbtr_Addr_Ln1;
        string appl_Dbtr_Addr_CityNme = InterceptionApplication.Appl_Dbtr_Addr_CityNme;
        string appl_Dbtr_Addr_PrvCd = InterceptionApplication.Appl_Dbtr_Addr_PrvCd;
        string appl_Dbtr_Addr_CtryCd = InterceptionApplication.Appl_Dbtr_Addr_CtryCd;
        string appl_Dbtr_Addr_PCd = InterceptionApplication.Appl_Dbtr_Addr_PCd;

        await LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd, loadFinancials: false);

        InterceptionApplication.Appl_Source_RfrNr = appl_Source_RfrNr ?? InterceptionApplication.Appl_Source_RfrNr;
        InterceptionApplication.Appl_CommSubm_Text = appl_CommSubm_Text ?? InterceptionApplication.Appl_CommSubm_Text;
        InterceptionApplication.Appl_Dbtr_Addr_Ln = appl_Dbtr_Addr_Ln ?? InterceptionApplication.Appl_Dbtr_Addr_Ln;
        InterceptionApplication.Appl_Dbtr_Addr_Ln1 = appl_Dbtr_Addr_Ln1 ?? InterceptionApplication.Appl_Dbtr_Addr_Ln1;
        InterceptionApplication.Appl_Dbtr_Addr_CityNme = appl_Dbtr_Addr_CityNme ?? InterceptionApplication.Appl_Dbtr_Addr_CityNme;
        InterceptionApplication.Appl_Dbtr_Addr_PrvCd = appl_Dbtr_Addr_PrvCd ?? InterceptionApplication.Appl_Dbtr_Addr_PrvCd;
        InterceptionApplication.Appl_Dbtr_Addr_CtryCd = appl_Dbtr_Addr_CtryCd ?? InterceptionApplication.Appl_Dbtr_Addr_CtryCd;
        InterceptionApplication.Appl_Dbtr_Addr_PCd = appl_Dbtr_Addr_PCd ?? InterceptionApplication.Appl_Dbtr_Addr_PCd;

        if (!IsValidCategory("I01"))
            return false;

        var interceptionDB = DB.InterceptionTable;
        InterceptionApplication.IntFinH = await interceptionDB.GetInterceptionFinancialTerms(Appl_EnfSrv_Cd, Appl_CtrlCd, "P");
        if (InterceptionApplication.IntFinH is not null)
        {
            InterceptionApplication.IntFinH.IntFinH_Affdvt_SubmCd = DB.CurrentSubmitter;
            InterceptionApplication.IntFinH.IntFinH_RcvtAffdvt_Dte = supportingDocsDate;
            InterceptionApplication.HldbCnd = await interceptionDB.GetHoldbackConditions(Appl_EnfSrv_Cd, Appl_CtrlCd,
                                                                       InterceptionApplication.IntFinH.IntFinH_Dte, "P");
        }

        bool result = await AcceptGarnishee(supportingDocsDate, isAutoAccept: false);

        if (Config.ESDsites.Contains(Appl_EnfSrv_Cd.Trim()) && (InterceptionApplication.Medium_Cd == "FTP"))
            await DB.InterceptionTable.UpdateESDrequired(Appl_EnfSrv_Cd, Appl_CtrlCd, supportingDocsDate);

        await EventManager.SaveEvents();

        return result;
    }

    private async Task<bool> AcceptGarnishee(DateTime supportingDocsDate, bool isAutoAccept = false)
    {
        AcceptedWithin30Days = true;

        if (!isAutoAccept)
        {
            var interceptionDB = DB.InterceptionTable;

            bool isESDsite = IsESD_MEP(Appl_EnfSrv_Cd);

            if (!GarnisheeSummonsReceiptDate.HasValue)
                GarnisheeSummonsReceiptDate = supportingDocsDate;
            else
                GarnisheeSummonsReceiptDate = await interceptionDB.GetGarnisheeSummonsReceiptDate(Appl_EnfSrv_Cd, Appl_CtrlCd, isESDsite);

            var dateDiff = GarnisheeSummonsReceiptDate - InterceptionApplication.Appl_Lgl_Dte;
            if (dateDiff.HasValue && dateDiff.Value.Days > 30)
            {
                AcceptedWithin30Days = false;
                await RejectInterception();

                return false;
            }
        }

        InterceptionApplication.Appl_LastUpdate_Usr = DB.CurrentSubmitter;
        InterceptionApplication.Appl_LastUpdate_Dte = DateTime.Now;

        InterceptionApplication.Subm_Affdvt_SubmCd = DB.CurrentSubmitter;
        InterceptionApplication.Appl_RecvAffdvt_Dte = supportingDocsDate;

        await SetNewStateTo(ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7);

        await UpdateApplicationNoValidation();

        await EventManager.SaveEvents();

        if (InterceptionApplication.Medium_Cd != "FTP")
            InterceptionApplication.Messages.AddInformation(EventCode.C50620_VALID_APPLICATION);

        return true;

    }

    public async Task<List<InterceptionApplicationData>> GetApplicationsForVariationAutoAccept(string enfService)
    {
        var applications = await DB.ApplicationTable.GetApplicationsForAutomation(enfService, medium_Cd: null,
                                                                    ApplicationState.AWAITING_DOCUMENTS_FOR_VARIATION_19,
                                                                    "I01", "A");

        var interceptions = new List<InterceptionApplicationData>();
        foreach (var application in applications)
            interceptions.Add(new InterceptionApplicationData(application));

        return interceptions;
    }

    public async Task<bool> IsRefNumberBlocked()
    {
        return await DB.InterceptionTable.IsRefNumberBlocked(InterceptionApplication.Appl_Source_RfrNr);
    }

    public async Task<bool> IsSinBlocked()
    {
        return await DB.InterceptionTable.IsSinBlocked(InterceptionApplication.Appl_Dbtr_Entrd_SIN);
    }

    public async Task FTBatchNotification_CheckFTTransactionsAdded()
    {
        await DB.InterceptionTable.FTBatchNotification_CheckFTTransactionsAdded();
    }

    public async Task MessageBrokerCRAReconciliation()
    {
        await DB.InterceptionTable.MessageBrokerCRAReconciliation();
    }

    public async Task AutoAcceptVariations(string enfService)
    {
        string processName = $"Process Auto Accept Variation {enfService}";
        await DB.ProductionAuditTable.Insert(processName, "Auto accept variation", "O");

        DB.CurrentSubmitter = "FO2SSS";

        var applAutomation = await GetApplicationsForVariationAutoAccept(enfService);

        foreach (var appl in applAutomation)
        {
            var thisManager = new InterceptionManager(appl, DB, DBfinance, Config, CurrentUser);
            await thisManager.AcceptVariation();
        }

        await DB.ProductionAuditTable.Insert(processName, "Ended", "O");
    }

    public async Task<bool> AcceptVariation()
    {
        if (!IsValidCategory("I01"))
            return false;

        string newComments = InterceptionApplication.Appl_CommSubm_Text?.Trim();

        if (!await LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd, loadFinancials: false))
        {
            InterceptionApplication.Messages.AddError($"No application was found in the database for {Appl_EnfSrv_Cd}-{Appl_CtrlCd}");
            return false;
        }

        if (!string.IsNullOrEmpty(newComments))
            InterceptionApplication.Appl_CommSubm_Text = newComments;

        if (InterceptionApplication.AppLiSt_Cd != ApplicationState.AWAITING_DOCUMENTS_FOR_VARIATION_19)
        {
            await EventManager.SaveEvents();

            return false;
        }

        if (InterceptionApplication.Medium_Cd == "FTP")
            InterceptionApplication.Appl_LastUpdate_Usr = "FO2SSS";
        else
            InterceptionApplication.Appl_LastUpdate_Usr = DB.CurrentSubmitter;
        InterceptionApplication.Appl_LastUpdate_Dte = DateTime.Now;

        EventManager.AddEvent(EventCode.C51111_VARIATION_ACCEPTED);

        // refresh the amount owed values in SummSmry
        var amountOwedProcess = new AmountOwedProcess(DB, DBfinance);
        var (summSmryNewData, _) = await amountOwedProcess.CalculateAndUpdateAmountOwedForVariation(Appl_EnfSrv_Cd, Appl_CtrlCd);

        await SetNewStateTo(ApplicationState.PARTIALLY_SERVICED_12);

        await ChangeStateForFinancialTerms(oldState: "A", newState: "I", 12);
        await ChangeStateForFinancialTerms(oldState: "P", newState: "A", 12);

        var activeFinTerms = await DB.InterceptionTable.GetInterceptionFinancialTerms(Appl_EnfSrv_Cd, Appl_CtrlCd, "A");

        VariationAction = VariationDocumentAction.AcceptVariationDocument;

        // update application

        decimal preBalance = summSmryNewData.PreBalance;

        await DB.InterceptionTable.InsertBalanceSnapshot(Appl_EnfSrv_Cd, Appl_CtrlCd, preBalance,
                                                                  BalanceSnapshotChangeType.VARIATION_ACCEPTED,
                                                                  intFinH_Date: activeFinTerms.IntFinH_Dte);
        await UpdateApplicationNoValidation();

        await DBfinance.SummonsSummaryRepository.UpdateSummonsSummary(summSmryNewData);

        if (!string.IsNullOrEmpty(activeFinTerms.IntFinH_DefHldbAmn_Period))
        {
            var fixedAmountData = DBfinance.SummonsSummaryFixedAmountRepository.GetSummonsSummaryFixedAmount(Appl_EnfSrv_Cd, Appl_CtrlCd);
            if (fixedAmountData is null)
            {
                var newFixedAmountRecalcDateTime = await RecalculateFixedAmountRecalcDateAfterVariation(activeFinTerms, DateTime.Now);
                await DBfinance.SummonsSummaryFixedAmountRepository.CreateSummonsSummaryFixedAmount(Appl_EnfSrv_Cd, Appl_CtrlCd,
                                                                                                        newFixedAmountRecalcDateTime);
            }
        }
        else
            await DBfinance.SummonsSummaryFixedAmountRepository.DeleteSummSmryFixedAmountRecalcDate(Appl_EnfSrv_Cd, Appl_CtrlCd);

        await EventManager.SaveEvents();

        if (InterceptionApplication.Medium_Cd != "FTP") InterceptionApplication.Messages.AddInformation(EventCode.C50620_VALID_APPLICATION);

        return true;
    }

    public async Task RejectInterception()
    {
        InterceptionApplication.Appl_LastUpdate_Usr = DB.CurrentSubmitter;
        InterceptionApplication.Appl_LastUpdate_Dte = DateTime.Now;

        await SetNewStateTo(ApplicationState.APPLICATION_REJECTED_9);

        await UpdateApplicationNoValidation();

        await IncrementGarnSmry();

        await EventManager.SaveEvents();

        if (InterceptionApplication.Medium_Cd != "FTP") InterceptionApplication.Messages.AddInformation(EventCode.C50620_VALID_APPLICATION);
    }

    public async Task<bool> RejectVariation(string applicationRejectReasons)
    {
        if (!IsValidCategory("I01"))
            return false;

        string newComments = InterceptionApplication.Appl_CommSubm_Text?.Trim();

        if (!await LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd, loadFinancials: false))
        {
            InterceptionApplication.Messages.AddError($"No application was found in the database for {Appl_EnfSrv_Cd}-{Appl_CtrlCd}");
            return false;
        }

        if (!string.IsNullOrEmpty(newComments))
            InterceptionApplication.Appl_CommSubm_Text = newComments;

        if (InterceptionApplication.AppLiSt_Cd != ApplicationState.AWAITING_DOCUMENTS_FOR_VARIATION_19)
        {
            await EventManager.SaveEvents();

            return false;
        }

        InterceptionApplication.Appl_LastUpdate_Usr = DB.CurrentSubmitter;
        InterceptionApplication.Appl_LastUpdate_Dte = DateTime.Now;

        if (applicationRejectReasons.Length > 80)
            applicationRejectReasons = applicationRejectReasons?[0..80];

        EventManager.AddEvent(EventCode.C51110_VARIATION_REJECTED, applicationRejectReasons);

        if (newComments == AUTO_REJECT_EXPIRED_TIME_FOR_VARIATION)
            EventManager.AddEvent(EventCode.C50762_VARIATION_REJECTED_BY_FOAEA_AS_VARIATION_DOCUMENT_NOT_RECEIVED_WITHIN_15_DAYS);

        await DeletePendingFinancialTerms();

        VariationAction = VariationDocumentAction.RejectVariationDocument;
        await SetNewStateTo(ApplicationState.PARTIALLY_SERVICED_12);

        await UpdateApplicationNoValidation();

        await EventManager.SaveEvents();

        if (InterceptionApplication.Medium_Cd != "FTP") InterceptionApplication.Messages.AddInformation(EventCode.C50620_VALID_APPLICATION);

        return true;
    }

    public async Task<List<ProcessEISOOUTHistoryData>> GetEISOvalidApplications()
    {
        return await DB.InterceptionTable.GetEISOvalidApplications();
    }

    public async Task<List<EIoutgoingFederalData>> GetEIoutgoingData(string enfSrv)
    {
        return await DB.InterceptionTable.GetEIoutgoingData(enfSrv);
    }

    public override async Task ProcessBringForwards(ApplicationEventData bfEvent)
    {
        bool closeEvent = false;

        TimeSpan diff;
        if (InterceptionApplication.Appl_LastUpdate_Dte.HasValue)
            diff = InterceptionApplication.Appl_LastUpdate_Dte.Value - DateTime.Now;
        else
            diff = TimeSpan.Zero;

        if ((InterceptionApplication.ActvSt_Cd != "A") &&
            ((!bfEvent.Event_Reas_Cd.HasValue) || (
             (bfEvent.Event_Reas_Cd.NotIn(EventCode.C50806_SCHEDULED_TO_BE_REINSTATED__QUARTERLY_TRACING,
                                          EventCode.C50680_CHANGE_OR_SUPPLY_ADDITIONAL_DEBTOR_INFORMATION_SEE_SIN_VERIFICATION_RESULTS_PAGE_IN_FOAEA_FOR_SPECIFIC_DETAILS,
                                          EventCode.C50600_INVALID_APPLICATION)))) &&
            ((diff.Equals(TimeSpan.Zero)) || (Math.Abs(diff.TotalHours) > 24)))
        {
            bfEvent.AppLiSt_Cd = ApplicationState.MANUALLY_TERMINATED_14;
            bfEvent.ActvSt_Cd = "I";

            await EventManager.SaveEvent(bfEvent);
        }
        else
        {
            if (bfEvent.Event_Reas_Cd.HasValue)
            {
                var dbNotification = DB.NotificationService;
                switch (bfEvent.Event_Reas_Cd)
                {
                    case EventCode.C50528_BF_10_DAYS_FROM_RECEIPT_OF_APPLICATION:
                        // do nothing for I01 -- this is now handled by the ESD event processing that every morning
                        break;

                    case EventCode.C50896_AWAITING_DOCUMENTS_FOR_VARIATION:
                        if (InterceptionApplication.AppLiSt_Cd == ApplicationState.AWAITING_DOCUMENTS_FOR_VARIATION_19)
                        {
                            DateTime lastVariationDate = await GetLastVariationDate();
                            int elapsed = Math.Abs((DateTime.Now - lastVariationDate).Days);

                            if (elapsed >= 15)
                                await RejectVariation(AUTO_REJECT_EXPIRED_TIME_FOR_VARIATION);
                            else
                            {
                                DateTime dateForNextBF = DateTime.Now.AddDays(5);
                                EventManager.AddEvent(EventCode.C50896_AWAITING_DOCUMENTS_FOR_VARIATION);
                                EventManager.AddBFEvent(EventCode.C50896_AWAITING_DOCUMENTS_FOR_VARIATION,
                                                        effectiveDateTime: dateForNextBF);
                            }
                        }
                        break;

                    case EventCode.C54005_CREATE_A_DEBTOR_LETTER_EVENT_IN_EVNTDBTR:
                        if (InterceptionApplication.Appl_Rcptfrm_Dte.AddDays(20) < DateTime.Now)
                        {
                            await dbNotification.SendEmail("Debtor Letter create date exceeds 20 day range",
                                "", // System.Configuration.ConfigurationManager.AppSettings("EmailRecipients")
                                $"Debtor letter created for I01 Application {Appl_EnfSrv_Cd} {Appl_CtrlCd} more than 20 days after Garnishee Summons Receipt Date. \n" +
                                $"Debtor name: {InterceptionApplication.Appl_Dbtr_SurNme}, {InterceptionApplication.Appl_Dbtr_FrstNme}\n" +
                                $"Justice ID: {InterceptionApplication.Appl_JusticeNr}\n" +
                                $"Garnishee Summons Receipt Date: {InterceptionApplication.Appl_Rcptfrm_Dte.ToShortDateString()}\n" +
                                $"Debtor letter create date: {DateTime.Now.ToShortDateString()}"
                                );
                        }
                        EventManager.AddEvent(EventCode.UNDEFINED, queue: EventQueue.EventDbtr); // ???
                        break;

                    case EventCode.C54001_BF_EVENT:
                    case EventCode.C54002_TELEPHONE_EVENT:
                        EventManager.AddEvent(bfEvent.Event_Reas_Cd.Value);
                        break;

                    default:
                        EventManager.AddEvent(EventCode.C54003_UNKNOWN_EVNTBF, queue: EventQueue.EventSYS);
                        break;
                }
            }
        }

        if (closeEvent)
        {
            bfEvent.AppLiSt_Cd = InterceptionApplication.AppLiSt_Cd;
            bfEvent.ActvSt_Cd = "C";

            await EventManager.SaveEvent(bfEvent);
        }

        await EventManager.SaveEvents();
    }
}
