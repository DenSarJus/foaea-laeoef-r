namespace FOAEA3.Business.Areas.Application;

internal partial class InterceptionManager : ApplicationManager
{
    protected override async Task Process_02_AwaitingValidation()
    {
        var exGratias = await DB.InterceptionTable.GetExGratias();

        string appEnteredSIN = InterceptionApplication.Appl_Dbtr_Entrd_SIN;
        string appRefNumber = InterceptionApplication.Appl_Source_RfrNr;
        bool exGratiaSinBlocked = exGratias.Any(m => (!string.IsNullOrEmpty(m.SIN.Trim())) &&
                                                     (m.SIN == appEnteredSIN) && m.BlockSIN);
        bool exGratiaRefNrBlocked = exGratias.Any(m => (!string.IsNullOrEmpty(m.Source_Ref_Nr.Trim())) &&
                                                       (m.Source_Ref_Nr == appRefNumber) && m.BlockREF);

        if (exGratiaSinBlocked || exGratiaRefNrBlocked)
        {
            InterceptionApplication.AppLiSt_Cd = ApplicationState.AWAITING_VALIDATION_2;

            EventManager.AddEvent(EventCode.C50600_INVALID_APPLICATION,
                                  eventReasonText: "Awaiting Action by FOAEA / En attente d’une action par AEOEF");

            var dbNotification = DB.NotificationService;

            string subject = "Application blocked due to ExGratia process";
            string body = "Application has been blocked because of: ";

            if (exGratiaRefNrBlocked) body += $"\n\tRef #: {appRefNumber}";
            if (exGratiaSinBlocked) body += $"\n\tSIN : {appEnteredSIN}";

            body += $"\n\n{Appl_EnfSrv_Cd}-{Appl_CtrlCd}";

            await dbNotification.SendEmail(subject, Config.Recipients.ExGratiaRecipients, body);
        }
        else
        {

            if (String.IsNullOrEmpty(InterceptionApplication.Appl_Dbtr_Entrd_SIN))
                await InterceptionValidation.CheckCreditorSurname();

            await base.Process_02_AwaitingValidation();

        }
    }

    protected override async Task Process_04_SinConfirmed()
    {
        await base.Process_04_SinConfirmed();

        InterceptionApplication.Appl_Affdvt_DocTypCd = I01_AFFITDAVIT_DOCUMENT_CODE;
        await SetNewStateTo(ApplicationState.PENDING_ACCEPTANCE_SWEARING_6);
    }

    protected override async Task Process_06_PendingAcceptanceSwearing()
    {
        await base.Process_06_PendingAcceptanceSwearing();

        await Validation.AddDuplicateSINWarningEvents();

        var submitterDB = DB.SubmitterTable;
        string signAuthority = await submitterDB.GetSignAuthorityForSubmitter(InterceptionApplication.Subm_SubmCd);

        EventManager.AddEvent(EventCode.C50701_WAITING_ACCEPTANCE_OF_GARNISHEE_SUMMONS_AT_FOAEA, recipientSubm: signAuthority);
    }

    protected override async Task Process_07_ValidAffidavitNotReceived()
    {
        var expectedNextState = ApplicationState.PENDING_ACCEPTANCE_SWEARING_6;

        if (string.IsNullOrEmpty(InterceptionApplication.Subm_Affdvt_SubmCd) || (!InterceptionApplication.Appl_RecvAffdvt_Dte.HasValue))
            EventManager.AddEvent(EventCode.C51019_REJECTED_AFFIDAVIT);  // ?? this never happens since this is not called except when accepting...?
        else
            expectedNextState = ApplicationState.APPLICATION_ACCEPTED_10;

        await SendDebtorLetter();

        await SetNewStateTo(expectedNextState);
    }

    protected override Task Process_09_ApplicationRejected()
    {
        InterceptionApplication.AppLiSt_Cd = ApplicationState.APPLICATION_REJECTED_9;
        InterceptionApplication.ActvSt_Cd = "J";

        if (!AcceptedWithin30Days.HasValue)
            AcceptedWithin30Days = true;

        if (!AcceptedWithin30Days.Value)
        {
            return Task.CompletedTask;
        }
        else if (InterceptionApplication.AppLiSt_Cd == ApplicationState.PENDING_ACCEPTANCE_SWEARING_6)
        {
            if (ESDReceived)
                EventManager.AddEvent(EventCode.C50761_APPLICATION_REJECTED_AS_SUMMONS_RECEIVED_MORE_THAN_30_DAYS_FROM_ITS_ISSUANCE);
        }
        else if (InterceptionApplication.AppLiSt_Cd.In(ApplicationState.INVALID_APPLICATION_1, ApplicationState.SIN_NOT_CONFIRMED_5))
        {
            EventManager.AddEvent(EventCode.C50760_APPLICATION_REJECTED_AS_CONDITIONS_NOT_MET_IN_TIMEFRAME);
            EventManager.AddEvent(EventCode.C50760_APPLICATION_REJECTED_AS_CONDITIONS_NOT_MET_IN_TIMEFRAME, queue: EventQueue.EventAM);
        }
        else
            EventManager.AddEvent(EventCode.C50591_REJECTED_APPLICATION);

        return Task.CompletedTask;
    }

    protected override async Task Process_10_ApplicationAccepted()
    {
        if (GarnisheeSummonsReceiptDate is null)
        {
            await AddSystemError(DB, InterceptionApplication.Messages, Config.Recipients.EmailRecipients,
                           $"GarnisheeSummonsReceiptDate is null. Cannot accept application {Appl_EnfSrv_Cd}-{Appl_CtrlCd}.");
            return;
        }

        await base.Process_10_ApplicationAccepted();

        var interceptionDB = DB.InterceptionTable;

        string justiceID = await interceptionDB.GetApplicationJusticeNumber(InterceptionApplication.Appl_Dbtr_Cnfrmd_SIN,
                                                                                           Appl_EnfSrv_Cd, Appl_CtrlCd);
        justiceID = justiceID.Trim();

        string debtorID;
        string justiceSuffix;
        EventCode eventBFNreasonCode;

        if (string.IsNullOrEmpty(justiceID))
        {
            eventBFNreasonCode = EventCode.C56001_NEW_BFN_FOR_NEW_DEBTOR;
            debtorID = await GenerateDebtorID(InterceptionApplication.Appl_Dbtr_SurNme);
            justiceSuffix = "A";
        }
        else
        {
            eventBFNreasonCode = EventCode.C56002_NEW_BFN_FOR_EXISTING_DEBTOR;
            debtorID = GetDebtorID(justiceID);
            await ProcessSummSmryBFN(debtorID, eventBFNreasonCode);
            nextJusticeID_callCount = 0;
            justiceSuffix = await NextJusticeID(justiceID);
        }

        await ChangeStateForFinancialTerms(oldState: "P", newState: "A", 10);

        DateTime startDate = GarnisheeSummonsReceiptDate.Value.Date.AddDays(35);

        await CreateSummonsSummary(debtorID, justiceSuffix, startDate);

        if (!string.IsNullOrEmpty(InterceptionApplication.IntFinH.IntFinH_DefHldbAmn_Period))
        {
            var fixedAmountDB = DBfinance.SummonsSummaryFixedAmountRepository;
            var fixedAmountData = await fixedAmountDB.GetSummonsSummaryFixedAmount(Appl_EnfSrv_Cd, Appl_CtrlCd);

            if (fixedAmountData is null)
            {
                await fixedAmountDB.CreateSummonsSummaryFixedAmount(Appl_EnfSrv_Cd, Appl_CtrlCd, startDate);
            }
            else
            {
                // QUESTION when would this already exists?
                fixedAmountData.SummSmry_LastFixedAmountCalc_Dte = DateTime.Now;
                fixedAmountData.SummSmry_FixedAmount_Recalc_Dte = startDate;

                await fixedAmountDB.UpdateSummonsSummaryFixedAmount(fixedAmountData);
            }
        }

        DateTime endDate = startDate.AddYears(5).AddDays(-1);
        string reasonText = $"{debtorID}{justiceSuffix}{startDate.Year}{startDate.DayOfYear:000}" +
                            $"{endDate.Year}{endDate.DayOfYear:000}";

        justiceID = debtorID + justiceSuffix;

        InterceptionApplication.Appl_JusticeNr = justiceID;

        if (eventBFNreasonCode != EventCode.UNDEFINED)
            EventManager.AddEvent(eventBFNreasonCode, queue: EventQueue.EventBFN, activeState: "A");

        EventManager.AddEvent(EventCode.C50780_APPLICATION_ACCEPTED, eventReasonText: reasonText, activeState: "I");

        await NotifyMatchingActiveApplications(EventCode.C50934_AN_APPLICATION_HAS_BEEN_ACCEPTED_FOR_THE_SAME_DEBTOR___CREDITOR_FROM_ANOTHER_JURISDICTION);
    }

    protected override async Task Process_12_PartiallyServiced()
    {
        var currentState = InterceptionApplication.AppLiSt_Cd;

        if (currentState == ApplicationState.AWAITING_DOCUMENTS_FOR_VARIATION_19)
        {
            await base.Process_12_PartiallyServiced();

            if (VariationAction == VariationDocumentAction.AcceptVariationDocument)
            {
                EventManager.AddEvent(EventCode.C51111_VARIATION_ACCEPTED);
                var interceptionDB = DB.InterceptionTable;
                if (await interceptionDB.IsVariationIncrease(Appl_EnfSrv_Cd, Appl_CtrlCd))
                    EventManager.AddEvent(EventCode.C51113_VARIATION_ACCEPTED_WITH_AN_ARREARS_VALUE_SIGNIFICANTLY_GREATER_THAN_THE_PREVIOUS_ARREARS);
            }
            else // reject variation
            {
                var summonsSummaryData = (await DBfinance.SummonsSummaryRepository.GetSummonsSummary(Appl_EnfSrv_Cd, Appl_CtrlCd))
                                            .FirstOrDefault();

                var recalcDate = summonsSummaryData?.SummSmry_Recalc_Dte;
                if ((recalcDate.HasValue) && (recalcDate.Value.Year == 3000) && (recalcDate.Value.Month == 1) && (recalcDate.Value.Day == 1))
                    InterceptionApplication.AppLiSt_Cd = ApplicationState.APPLICATION_SUSPENDED_35;

                EventManager.AddEvent(EventCode.C51110_VARIATION_REJECTED);
            }
        }
        else if (currentState == ApplicationState.APPLICATION_ACCEPTED_10)
        {
            await base.Process_12_PartiallyServiced();

            EventManager.AddEvent(EventCode.C50826_MONEY_HAS_BEEN_RECEIVED);
        }

    }

    protected override async Task Process_13_FullyServiced()
    {
        var previousState = InterceptionApplication.AppLiSt_Cd;

        await base.Process_13_FullyServiced();

        await StopBlockFunds(ApplicationState.FULLY_SERVICED_13, previousState);

        InterceptionApplication.ActvSt_Cd = "C";

        EventManager.AddEvent(EventCode.C50850_APPLICATION_FINANCIALLY_SATISFIED);
    }

    protected override async Task Process_14_ManuallyTerminated()
    {
        var previousState = InterceptionApplication.AppLiSt_Cd;

        await StopBlockFunds(ApplicationState.MANUALLY_TERMINATED_14, previousState);

        InterceptionApplication.ActvSt_Cd = "X";
        InterceptionApplication.AppLiSt_Cd = ApplicationState.MANUALLY_TERMINATED_14;

        if (previousState > ApplicationState.APPLICATION_REJECTED_9)
            EventManager.AddEvent(EventCode.C50843_APPLICATION_CANCELLED, activeState: "I");
        else
            EventManager.AddEvent(EventCode.C50843_APPLICATION_CANCELLED, activeState: "A");

    }

    protected override async Task Process_15_Expired()
    {
        var previousState = InterceptionApplication.AppLiSt_Cd;

        InterceptionApplication.AppLiSt_Cd = ApplicationState.EXPIRED_15;

        EventManager.AddEvent(EventCode.C50860_APPLICATION_COMPLETED, activeState: "I");

        await StopBlockFunds(ApplicationState.EXPIRED_15, previousState);

        InterceptionApplication.ActvSt_Cd = "C";
    }

    protected override async Task Process_17_FinancialTermsVaried()
    {
        await base.Process_17_FinancialTermsVaried();

        var currentApplicationManager = new InterceptionManager(DB, DBfinance, Config, CurrentUser);

        await currentApplicationManager.LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd);

        var currentApplInfo = currentApplicationManager.InterceptionApplication;

        switch (currentApplInfo.AppLiSt_Cd)
        {
            case ApplicationState.APPLICATION_ACCEPTED_10:
            case ApplicationState.PARTIALLY_SERVICED_12:
            case ApplicationState.APPLICATION_SUSPENDED_35:

                if (!await InterceptionValidation.ValidVariationDefaultHoldbacks())
                    await SetNewStateTo(ApplicationState.INVALID_VARIATION_FINTERMS_92);

                else if (!InterceptionValidation.ValidVariationSourceSpecificHoldbacks())
                    await SetNewStateTo(ApplicationState.INVALID_VARIATION_SOURCE_91);

                else
                    await SetNewStateTo(ApplicationState.VALID_FINANCIAL_VARIATION_93);

                break;

            default:

                // should not try to vary application at any other state
                EventManager.AddEvent(EventCode.C55006_APPLICATION_NOT_IN_EFFECT);
                return;
        }
    }

    protected override async Task Process_19_AwaitingDocumentsForVariation()
    {
        await base.Process_19_AwaitingDocumentsForVariation();

        EventManager.AddBFEvent(EventCode.C50896_AWAITING_DOCUMENTS_FOR_VARIATION, effectiveDateTime: DateTime.Now.AddDays(5));
    }

    protected override async Task Process_35_ApplicationSuspended()
    {
        if (InterceptionApplication.AppLiSt_Cd.In(ApplicationState.APPLICATION_ACCEPTED_10, ApplicationState.PARTIALLY_SERVICED_12,
                                                  ApplicationState.AWAITING_DOCUMENTS_FOR_VARIATION_19))
            EventManager.AddEvent(EventCode.C51115_APPLICATION_SUSPENDED, appState: ApplicationState.APPLICATION_SUSPENDED_35);
        else
            EventManager.AddEvent(EventCode.C55006_APPLICATION_NOT_IN_EFFECT);

        await base.Process_35_ApplicationSuspended();
    }

    protected override async Task Process_91_InvalidVariationSource()
    {
        await base.Process_91_InvalidVariationSource();

        EventManager.AddEvent(EventCode.C55001_INVALID_SOURCE_HOLDBACK);
        EventManager.AddEvent(EventCode.C55000_INVALID_VARIATION);
    }

    protected override async Task Process_92_InvalidVariationFinTerms()
    {
        await base.Process_92_InvalidVariationFinTerms();

        EventManager.AddEvent(EventCode.C55002_INVALID_FINANCIAL_TERMS);
        EventManager.AddEvent(EventCode.C55000_INVALID_VARIATION);
    }

    protected override async Task Process_93_ValidFinancialVariation()
    {
        await base.Process_93_ValidFinancialVariation();

        await SetNewStateTo(ApplicationState.AWAITING_DOCUMENTS_FOR_VARIATION_19);

        EventManager.AddEvent(EventCode.C50896_AWAITING_DOCUMENTS_FOR_VARIATION, activeState: "I",
                              updateSubm: DB.UpdateSubmitter);
    }

}
