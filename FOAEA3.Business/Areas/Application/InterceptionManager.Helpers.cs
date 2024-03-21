using FOAEA3.Business.Areas.BackendProcesses;

namespace FOAEA3.Business.Areas.Application;

internal partial class InterceptionManager : ApplicationManager
{
    public async Task<List<PaymentPeriodData>> GetPaymentPeriods()
    {
        return await DB.InterceptionTable.GetPaymentPeriods();
    }

    public async Task<string> EISOHistoryDeleteBySIN(string thisSIN, bool removeSIN = false)
    {
        return await DB.InterceptionTable.EISOHistoryDeleteBySIN(thisSIN, removeSIN);
    }

    public async Task<string> FixDebtorIdForSin(string newSIN)
    {
        string message = string.Empty;
        var debtorIds = await DB.InterceptionTable.GetJusticeNumberDataForSin(newSIN);

        if (debtorIds.Count > 1)
        {
            message = "A conflict of Multiple Debtor ID's for this SIN have been corrected for the following applications.\n";

            var firstDebtorIdData = debtorIds[0];
            string firstDebtorId = firstDebtorIdData.DebtorID;

            foreach (var thisDebtor in debtorIds)
            {
                if (thisDebtor.DebtorID != firstDebtorId)
                {
                    string nextJusticeID = await NextJusticeID(firstDebtorId + firstDebtorIdData.DebtorIDSuffix);
                    string nextSuffix = nextJusticeID[^1..];
                    await DB.InterceptionTable.ModifyMultipleDebtorIds(firstDebtorId, nextSuffix,
                                                                       thisDebtor.Appl_EnfSrv_Cd, thisDebtor.Appl_CtrlCd);
                    message += $" {thisDebtor.Appl_EnfSrv_Cd.Trim()}-{thisDebtor.Appl_CtrlCd.Trim()}";
                }
            }
        }

        return message;
    }

    private async Task SendDebtorLetter()
    {
        // TODO: this checks will currently fail in the old code, but will work here... should it?
        var events = await EventManager.GetEventBF(InterceptionApplication.Subm_SubmCd, Appl_CtrlCd,
                                             EventCode.C54005_CREATE_A_DEBTOR_LETTER_EVENT_IN_EVNTDBTR, "A");

        if (events.Count == 0)
            EventManager.AddBFEvent(EventCode.C54005_CREATE_A_DEBTOR_LETTER_EVENT_IN_EVNTDBTR,
                effectiveDateTime: DateTime.Now, appState: ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7);

    }

    private static string GetDebtorID(string justiceID)
    {
        if (!string.IsNullOrEmpty(justiceID))
            return justiceID.Trim()[..7];
        else
            return null;
    }

    private static string DebtorPrefix(string name)
    {
        name = name.Trim();
        name = name.Replace("'", "");
        if (name.Length >= 3)
            return name[..3];
        else
            return name.PadRight(3, '-');
    }

    private async Task<string> GenerateDebtorID(string debtorLastName)
    {
        return await DB.InterceptionTable.GetDebtorId(DebtorPrefix(debtorLastName)); ;
    }

    private async Task<string> NextJusticeID(string justiceID)
    {
        string justiceSuffix = justiceID.Substring(justiceID.Length - 1, 1);
        string debtorID = justiceID[..7];
        if (string.IsNullOrEmpty(justiceID))
            justiceSuffix = "A";
        else
            justiceSuffix = GetNextSuffixLetter(justiceSuffix);

        if (justiceSuffix == "@")
            justiceSuffix = "A";

        string newJusticeNumber = debtorID.Trim() + justiceSuffix.Trim();

        bool isAlreadyUser = await DB.InterceptionTable.IsAlreadyUsedJusticeNumber(newJusticeNumber);

        if (isAlreadyUser)
            justiceSuffix = await NextJusticeID(justiceSuffix);

        nextJusticeID_callCount++;
        if (nextJusticeID_callCount > 26)
            throw new Exception("Infinite loop!");

        return justiceSuffix;
    }

    private static string GetNextSuffixLetter(string previousJusticeSuffix)
    {
        const string JUSTICE_ID_SUFFIX = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";

        int position = JUSTICE_ID_SUFFIX.IndexOf(previousJusticeSuffix);
        if ((position >= 25) || (position == -1) || (position == JUSTICE_ID_SUFFIX.Length - 1))
            return "@";
        else
            return JUSTICE_ID_SUFFIX.Substring(position + 1, 1);
    }

    private bool IsESD_MEP(string enforcementServiceCode)
    {
        return Config.ESDsites.Contains(enforcementServiceCode);
    }

    private async Task ProcessSummSmryBFN(string debtorId, EventCode eventBFNreasonCode)
    {
        var summSmryData = await DBfinance.SummonsSummaryRepository.GetSummonsSummary(debtorId: debtorId);

        foreach (var summSmryItem in summSmryData)
            if ((summSmryItem.Appl_EnfSrv_Cd != Appl_EnfSrv_Cd) && (summSmryItem.Appl_CtrlCd != Appl_CtrlCd))
                await ProcessBringForwardNotification(summSmryItem.Appl_EnfSrv_Cd, summSmryItem.Appl_CtrlCd, eventBFNreasonCode);
    }

    private async Task ProcessBringForwardNotification(string applEnfSrvCode, string applControlCode, EventCode eventBFNreasonCode)
    {
        var bfApplicationManager = new InterceptionManager(DB, DBfinance, Config, CurrentUser);

        await bfApplicationManager.LoadApplication(applEnfSrvCode, applControlCode);
        var bfApplication = bfApplicationManager.InterceptionApplication;
        var bfEventManager = new ApplicationEventManager(bfApplication, DB);
        var bfEventDetailManager = new ApplicationEventDetailManager(bfApplication, DB);

        var eventBFNs = (await bfEventManager.GetApplicationEventsForQueue(EventQueue.EventBFN))
                            .Where(m => m.ActvSt_Cd == "A");

        var eventBFNDetails = (await bfEventDetailManager.GetApplicationEventDetailsForQueue(EventQueue.EventBFN_dtl))
                                .Where(m => m.ActvSt_Cd == "A");

        foreach (var thisEventBFN in eventBFNs)
        {
            EventCode thisEventCode = EventCode.UNDEFINED;
            if (thisEventBFN.Event_Reas_Cd.HasValue)
                thisEventCode = thisEventBFN.Event_Reas_Cd.Value;

            switch (thisEventCode)
            {
                case EventCode.C56003_CANCELLED_OR_COMPLETED_BFN:
                    eventBFNreasonCode = EventCode.C56002_NEW_BFN_FOR_EXISTING_DEBTOR;
                    thisEventBFN.ActvSt_Cd = "C";
                    thisEventBFN.Event_Compl_Dte = DateTime.Now;
                    break;
                case EventCode.C56001_NEW_BFN_FOR_NEW_DEBTOR:
                    eventBFNreasonCode = EventCode.UNDEFINED;
                    break;
                case EventCode.C56002_NEW_BFN_FOR_EXISTING_DEBTOR:
                    eventBFNreasonCode = EventCode.UNDEFINED;
                    break;
                case EventCode.UNDEFINED:
                    eventBFNreasonCode = EventCode.C56001_NEW_BFN_FOR_NEW_DEBTOR;
                    break;
            }

        }

        foreach (var thisEventBFNdetail in eventBFNDetails)
        {
            EventCode thisEventDetailCode = EventCode.UNDEFINED;
            if (thisEventBFNdetail.Event_Reas_Cd.HasValue)
                thisEventDetailCode = thisEventBFNdetail.Event_Reas_Cd.Value;

            if (thisEventDetailCode == EventCode.C56003_CANCELLED_OR_COMPLETED_BFN)
            {
                thisEventBFNdetail.ActvSt_Cd = "C";
                thisEventBFNdetail.Event_Compl_Dte = DateTime.Now;
                thisEventBFNdetail.Event_Reas_Text = $"Application {bfApplication.Appl_EnfSrv_Cd}-{bfApplication.Appl_CtrlCd} became active, STOP BLOCK not sent";
            }
        }

        await bfEventManager.SaveEvents();
        await bfEventDetailManager.SaveEventDetails();
    }

    private async Task<DateTime> RecalculateFixedAmountRecalcDateAfterVariation(InterceptionFinancialHoldbackData newFinTerms, DateTime variationCalcDate)
    {
        DateTime fixedAmountRecalcDate = variationCalcDate;
        DateTime currDateTime = variationCalcDate;
        DateTime ctrlFaDtePayable = currDateTime;

        var summSmry = await DBfinance.SummonsSummaryRepository.GetSummonsSummary(Appl_EnfSrv_Cd, Appl_CtrlCd);
        var workActSummons = await DBfinance.ActiveSummonsRepository.GetActiveSummonsCore(ctrlFaDtePayable, Appl_EnfSrv_Cd, Appl_CtrlCd);

        if (workActSummons is not null)
        {
            var activeSummonData = GetActiveSummonsForVariation(ctrlFaDtePayable, newFinTerms, summSmry.FirstOrDefault());

            if (activeSummonData is not null)
            {
                DateTime calcFaDtePayable;

                // if payable date prior to variation, use system date as payable date

                if ((ctrlFaDtePayable > activeSummonData.Start_Dte) &&
                    (activeSummonData.IntFinH_VarIss_Dte.HasValue) &&
                    (ctrlFaDtePayable < activeSummonData.IntFinH_VarIss_Dte))
                    calcFaDtePayable = currDateTime;
                else
                    calcFaDtePayable = ctrlFaDtePayable;

                // if summon has been varied, use variation issue date of latest variation
                // to determine current period

                DateTime calcStartDate = activeSummonData.IntFinH_VarIss_Dte ?? activeSummonData.Start_Dte;

                string fixedAmountPrdFreqCd = activeSummonData.IntFinH_DefHldbAmn_Period ?? string.Empty;

                // adjust calcStartDate so that it matches the SummSmry StartDate (as if the periods
                // had been calculated from that start date)

                if (InterceptionApplication.Appl_RecvAffdvt_Dte is null)
                {
                    await AddSystemError(DB, InterceptionApplication.Messages, Config.Recipients.EmailRecipients,
                                   $"Appl_RecvAffdvt_Dte is null for {Appl_EnfSrv_Cd}-{Appl_CtrlCd}. Cannot recalculate fixed amount recalc date after variation!");
                    return fixedAmountRecalcDate;
                }

                DateTime acceptedDate = InterceptionApplication.Appl_RecvAffdvt_Dte.Value.Date;
                calcStartDate = AmountOwedProcess.AdjustCalcAcceptedDateBasedOnAcceptedDateAndPeriod(calcStartDate, acceptedDate, fixedAmountPrdFreqCd);

                int currentPeriod = AmountOwedProcess.CalculatePeriodCount(fixedAmountPrdFreqCd, calcFaDtePayable.Date, calcStartDate.Date);

                DateTime? newDate = AmountOwedProcess.CalculateRecalcDateFromStartDateForPeriodCount(fixedAmountPrdFreqCd, currentPeriod, calcStartDate);

                if (newDate.HasValue)
                    fixedAmountRecalcDate = newDate.Value;

            }

        }

        return fixedAmountRecalcDate;

    }

    private ActiveSummonsData GetActiveSummonsForVariation(DateTime ctrlFaDtePayable, InterceptionFinancialHoldbackData intFinHdata, SummonsSummaryData summSmryData)
    {
        ActiveSummonsData activeSummons = null;

        var appl = InterceptionApplication;

        if ((ctrlFaDtePayable >= summSmryData.Start_Dte) && (ctrlFaDtePayable <= summSmryData.End_Dte))
        {
            DateTime thisVarEnterDte = default;
            if (!intFinHdata.IntFinH_VarIss_Dte.HasValue)
                if (intFinHdata.IntFinH_RcvtAffdvt_Dte.HasValue)
                    thisVarEnterDte = intFinHdata.IntFinH_RcvtAffdvt_Dte.Value;

            string hldbTypeCode = "T";
            if (!string.IsNullOrEmpty(intFinHdata.HldbTyp_Cd))
                hldbTypeCode = intFinHdata.HldbTyp_Cd;

            decimal mxmTtl_Money = intFinHdata.IntFinH_MxmTtl_Money ?? 0M;
            decimal perPym_Money = intFinHdata.IntFinH_PerPym_Money ?? 0M;
            byte cmlPrPym_Ind = intFinHdata.IntFinH_CmlPrPym_Ind ?? 0;
            int defHldbPrcnt = intFinHdata.IntFinH_DefHldbPrcnt ?? 0;
            decimal defHldbAmn_Money = intFinHdata.IntFinH_DefHldbAmn_Money ?? 0M;
            DateTime varIss_Dte = intFinHdata.IntFinH_VarIss_Dte ?? default;
            string pymPrCd = intFinHdata.PymPr_Cd ?? string.Empty;
            string defHldbAmnPrCd = intFinHdata.IntFinH_DefHldbAmn_Period ?? string.Empty;

            activeSummons = new ActiveSummonsData
            {
                Subm_SubmCd = appl.Subm_SubmCd,
                Appl_JusticeNr = appl.Appl_JusticeNr,
                IntFinH_LmpSum_Money = intFinHdata.IntFinH_LmpSum_Money,
                IntFinH_PerPym_Money = perPym_Money,
                IntFinH_MxmTtl_Money = mxmTtl_Money,
                PymPr_Cd = pymPrCd,
                IntFinH_CmlPrPym_Ind = cmlPrPym_Ind,
                HldbCtg_Cd = intFinHdata.HldbCtg_Cd,
                IntFinH_DefHldbPrcnt = defHldbPrcnt,
                IntFinH_DefHldbAmn_Money = defHldbAmn_Money,
                IntFinH_DefHldbAmn_Period = defHldbAmnPrCd,
                HldbTyp_Cd = hldbTypeCode,
                Start_Dte = summSmryData.Start_Dte,
                FeeDivertedTtl_Money = summSmryData.FeeDivertedTtl_Money,
                LmpSumDivertedTtl_Money = summSmryData.LmpSumDivertedTtl_Money,
                PerPymDivertedTtl_Money = summSmryData.PerPymDivertedTtl_Money,
                HldbAmtTtl_Money = summSmryData.HldbAmtTtl_Money,
                Appl_TotalAmnt = summSmryData.Appl_TotalAmnt,
                IntFinH_Dte = intFinHdata.IntFinH_Dte,
                End_Dte = summSmryData.End_Dte,
                Appl_RecvAffdvt_Dte = summSmryData.Start_Dte,
                IntFinH_VarIss_Dte = varIss_Dte,
                LmpSumOwedTtl_Money = summSmryData.LmpSumOwedTtl_Money,
                PerPymOwedTtl_Money = summSmryData.PerPymOwedTtl_Money,
                Appl_EnfSrv_Cd = intFinHdata.Appl_EnfSrv_Cd,
                VarEnterDte = thisVarEnterDte,
                Appl_CtrlCd = intFinHdata.Appl_CtrlCd
            };

            if (!intFinHdata.IntFinH_VarIss_Dte.HasValue)
                activeSummons.IntFinH_VarIss_Dte = null;

        }

        return activeSummons;
    }
    private async Task IncrementGarnSmry(bool isNewApplication = false)
    {

        var garnSmryDB = DBfinance.GarnSummaryRepository;

        if (isNewApplication || InterceptionApplication.AppLiSt_Cd.In(ApplicationState.SIN_NOT_CONFIRMED_5,
                                                                      ApplicationState.APPLICATION_REJECTED_9,
                                                                      ApplicationState.APPLICATION_SUSPENDED_35))
        {
            int fiscalMonthCounter = DateTime.Now.GetFiscalMonth();
            int fiscalYear = DateTime.Now.GetFiscalYear();
            string enfOfficeCode = InterceptionApplication.Subm_SubmCd.Substring(3, 1);

            var garnSummaryData = await garnSmryDB.GetGarnSummary(Appl_EnfSrv_Cd, enfOfficeCode, fiscalMonthCounter, fiscalYear);

            GarnSummaryData thisGarnSmryData;

            if (garnSummaryData.Count == 0)
            {
                int totalActiveSummonsCount = await DB.InterceptionTable.GetTotalActiveSummons(Appl_EnfSrv_Cd, enfOfficeCode);
                thisGarnSmryData = new GarnSummaryData
                {
                    EnfSrv_Cd = Appl_EnfSrv_Cd,
                    EnfOff_Cd = enfOfficeCode,
                    AcctYear = fiscalYear,
                    AcctMonth = fiscalMonthCounter,
                    Ttl_ActiveSummons_Count = totalActiveSummonsCount,
                    Mth_ActiveSummons_Count = 0,
                    Mth_ActionedSummons_Count = 0,
                    Mth_LumpSumActive_Amount = 0,
                    Mth_PeriodicActive_Amount = 0,
                    Mth_FeesActive_Amount = 0,
                    Mth_FeesDiverted_Amount = 0,
                    Mth_LumpSumDiverted_Amount = 0,
                    Mth_PeriodicDiverted_Amount = 0,
                    Mth_FeesOwed_Amount = 0,
                    Mth_FeesRemitted_Amount = 0,
                    Mth_FeesCollected_Amount = 0,
                    Mth_FeesDisbursed_Amount = 0,
                    Mth_Uncollected_Amount = 0,
                    Mth_FeesSatisfied_Count = 0,
                    Mth_FeesUnsatisfied_Count = 0,
                    Mth_Garnisheed_Amount = 0,
                    Mth_DivertActions_Count = 0,
                    Mth_Variation1_Count = 0,
                    Mth_Variation2_Count = 0,
                    Mth_Variation3_Count = 0,
                    Mth_Variations_Count = 0,
                    Mth_SummonsReceived_Count = 0,
                    Mth_SummonsCancelled_Count = 0,
                    Mth_SummonsRejected_Count = 0,
                    Mth_SummonsSatisfied_Count = 0,
                    Mth_SummonsExpired_Count = 0,
                    Mth_SummonsSuspended_Count = 0,
                    Mth_SummonsArchived_Count = 0,
                    Mth_SummonsSIN_Count = 0,
                    Mth_Action_Count = 0,
                    Mth_FAAvailable_Amount = 0,
                    Mth_FA_Count = 0,
                    Mth_CRAction_Count = 0,
                    Mth_CRFee_Amount = 0,
                    Mth_CRPaid_Amount = 0
                };

                await garnSmryDB.CreateGarnSummary(thisGarnSmryData);
            }
            else
                thisGarnSmryData = garnSummaryData.First();

            if (isNewApplication)
            {
                if (thisGarnSmryData.Mth_SummonsReceived_Count.HasValue)
                    thisGarnSmryData.Mth_SummonsReceived_Count++;
                else
                    thisGarnSmryData.Mth_SummonsReceived_Count = 1;
            }

            switch (InterceptionApplication.AppLiSt_Cd)
            {
                case ApplicationState.SIN_NOT_CONFIRMED_5:
                    if (thisGarnSmryData.Mth_SummonsSIN_Count.HasValue)
                        thisGarnSmryData.Mth_SummonsSIN_Count++;
                    else
                        thisGarnSmryData.Mth_SummonsSIN_Count = 1;
                    break;

                case ApplicationState.APPLICATION_REJECTED_9:
                    if (thisGarnSmryData.Mth_SummonsRejected_Count.HasValue)
                        thisGarnSmryData.Mth_SummonsRejected_Count++;
                    else
                        thisGarnSmryData.Mth_SummonsRejected_Count = 1;
                    break;

                case ApplicationState.APPLICATION_SUSPENDED_35:
                    if (thisGarnSmryData.Mth_SummonsSuspended_Count.HasValue)
                        thisGarnSmryData.Mth_SummonsSuspended_Count++;
                    else
                        thisGarnSmryData.Mth_SummonsSuspended_Count = 1;
                    break;
            }

            await garnSmryDB.UpdateGarnSummary(thisGarnSmryData);

        }

    }

    private async Task<DateTime> ChangeStateForFinancialTerms(string oldState, string newState, short intFinH_LifeStateCode)
    {

        var interceptionDB = DB.InterceptionTable;

        var defaultHoldback = InterceptionApplication.IntFinH ?? await interceptionDB.GetInterceptionFinancialTerms(Appl_EnfSrv_Cd, Appl_CtrlCd, oldState);

        DateTime intFinH_Date = defaultHoldback.IntFinH_Dte;

        defaultHoldback.ActvSt_Cd = newState;

        defaultHoldback.IntFinH_LiStCd = intFinH_LifeStateCode;

        if (newState == "A")
        {
            defaultHoldback.IntFinH_RcvtAffdvt_Dte = DateTime.Now;
            defaultHoldback.IntFinH_Affdvt_SubmCd = "FO2SSS";
        }

        var sourceSpecificHoldbacks = InterceptionApplication.HldbCnd ?? await interceptionDB.GetHoldbackConditions(Appl_EnfSrv_Cd, Appl_CtrlCd, intFinH_Date, oldState);

        foreach (var sourceSpecificHoldback in sourceSpecificHoldbacks)
        {
            sourceSpecificHoldback.ActvSt_Cd = newState;
            sourceSpecificHoldback.HldbCnd_LiStCd = 2; // valid
        }

        await interceptionDB.UpdateInterceptionFinancialTerms(defaultHoldback);
        await interceptionDB.UpdateHoldbackConditions(sourceSpecificHoldbacks);

        return intFinH_Date;

    }

    private async Task DeletePendingFinancialTerms()
    {

        var interceptionDB = DB.InterceptionTable;

        var defaultHoldback = await interceptionDB.GetInterceptionFinancialTerms(Appl_EnfSrv_Cd, Appl_CtrlCd, "P");

        DateTime intFinH_Date = defaultHoldback.IntFinH_Dte;

        var sourceSpecificHoldbacks = await interceptionDB.GetHoldbackConditions(Appl_EnfSrv_Cd, Appl_CtrlCd, intFinH_Date, "P");

        await interceptionDB.DeleteHoldbackConditions(sourceSpecificHoldbacks);
        await interceptionDB.DeleteInterceptionFinancialTerms(defaultHoldback);
    }

    private async Task<DateTime> GetLastVariationDate()
    {
        var interceptionDB = DB.InterceptionTable;

        var defaultHoldback = await interceptionDB.GetInterceptionFinancialTerms(Appl_EnfSrv_Cd, Appl_CtrlCd, "P");

        return defaultHoldback.IntFinH_Dte;
    }

    private async Task CreateSummonsSummary(string debtorID, string justiceSuffix, DateTime startDate)
    {
        var summSummary = new SummonsSummaryData
        {
            Appl_EnfSrv_Cd = Appl_EnfSrv_Cd,
            Appl_CtrlCd = Appl_CtrlCd,
            Dbtr_Id = debtorID,
            Appl_JusticeNrSfx = justiceSuffix,
            Start_Dte = startDate,
            End_Dte = startDate.AddYears(5).AddDays(-1),
            SummSmry_Recalc_Dte = startDate
        };

        var existingData = await DBfinance.SummonsSummaryRepository.GetSummonsSummary(Appl_EnfSrv_Cd, Appl_CtrlCd);

        if (!existingData.Any())
            await DBfinance.SummonsSummaryRepository.CreateSummonsSummary(summSummary);
        else
            // TODO: should we get a warning that instead of creating we are updating an existing summsmry?
            await DBfinance.SummonsSummaryRepository.UpdateSummonsSummary(summSummary);
    }

    private async Task NotifyMatchingActiveApplications(EventCode eventCode)
    {
        var matchedApplications = await DB.InterceptionTable.FindMatchingActiveApplications(Appl_EnfSrv_Cd, Appl_CtrlCd,
                                                                                            InterceptionApplication.Appl_Dbtr_Cnfrmd_SIN,
                                                                                            InterceptionApplication.Appl_Crdtr_FrstNme,
                                                                                            InterceptionApplication.Appl_Crdtr_SurNme);

        if (matchedApplications.Any())
        {
            string newApplicationKey = $"{Appl_EnfSrv_Cd}-{InterceptionApplication.Subm_SubmCd}-{Appl_CtrlCd}";
            foreach (var matchedApplication in matchedApplications)
            {
                EventManager.AddEvent(eventCode, eventReasonText: newApplicationKey, appState: matchedApplication.AppLiSt_Cd,
                                      submCd: matchedApplication.Subm_SubmCd, recipientSubm: matchedApplication.Subm_Recpt_SubmCd,
                                      enfSrv: matchedApplication.Appl_EnfSrv_Cd, controlCode: matchedApplication.Appl_CtrlCd);
            }
        }
    }

    private async Task StopBlockFunds(ApplicationState requestedState, ApplicationState previousState)
    {
        string debtorID = GetDebtorID(InterceptionApplication.Appl_JusticeNr);
        int summSmryCount = 0;

        var summSmryInfoForDebtor = await DBfinance.SummonsSummaryRepository.GetSummonsSummary(debtorId: debtorID);

        foreach (var summSmryInfo in summSmryInfoForDebtor)
        {
            if (summSmryInfo.ActualEnd_Dte is null)
                summSmryCount++;
        }

        summSmryCount--;

        if (summSmryCount <= 0)
        {
            var effective10daysFromNow = DateTime.Now.AddDays(10);
            EventManager.AddEvent(EventCode.C56003_CANCELLED_OR_COMPLETED_BFN, queue: EventQueue.EventBFN,
                                  effectiveDateTime: effective10daysFromNow);
        }

        var summSmryForCurrentAppl = (await DBfinance.SummonsSummaryRepository.GetSummonsSummary(Appl_EnfSrv_Cd, Appl_CtrlCd))
                                        .FirstOrDefault();
        if ((summSmryForCurrentAppl is null) && (previousState >= ApplicationState.APPLICATION_ACCEPTED_10))
        {
            await AddSystemError(DB, InterceptionApplication.Messages, Config.Recipients.SystemErrorRecipients,
                           $"Could not find summSmry record for {Appl_EnfSrv_Cd}-{Appl_CtrlCd} in StopBlockFunds!");
            return;
        }

        if (summSmryForCurrentAppl is not null)
        {
            switch (requestedState)
            {
                case ApplicationState.FULLY_SERVICED_13:
                case ApplicationState.MANUALLY_TERMINATED_14:
                    summSmryForCurrentAppl.ActualEnd_Dte = DateTime.Now;
                    break;
                case ApplicationState.EXPIRED_15:
                    summSmryForCurrentAppl.ActualEnd_Dte = summSmryForCurrentAppl.End_Dte;
                    break;
                default:
                    // Throw New Exception("Invalid State for the current application.")
                    break;
            }

            await DBfinance.SummonsSummaryRepository.UpdateSummonsSummary(summSmryForCurrentAppl);
        }

        await DB.InterceptionTable.EISOHistoryDeleteBySIN(InterceptionApplication.Appl_Dbtr_Cnfrmd_SIN, false);

    }

    private async Task<bool> FinancialTermsHaveBeenModified()
    {
        var currentAppManager = new InterceptionManager(DB, DBfinance, Config, CurrentUser);
        await currentAppManager.LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd);
        var currentApp = currentAppManager.InterceptionApplication;
        var newApp = InterceptionApplication;

        // compare intFinH
        if (!currentApp.IntFinH.ValuesEqual(newApp.IntFinH))
            return true;

        // compare holdbackCnd
        if (currentApp.HldbCnd.Count != newApp.HldbCnd.Count)
            return true;

        foreach (var holdbackCondition in currentApp.HldbCnd)
        {
            var newCondition = newApp.HldbCnd.Where(m => m.Appl_EnfSrv_Cd == holdbackCondition.Appl_EnfSrv_Cd &&
                                                         m.Appl_CtrlCd == holdbackCondition.Appl_CtrlCd &&
                                                         m.IntFinH_Dte == holdbackCondition.IntFinH_Dte &&
                                                         m.EnfSrv_Cd == holdbackCondition.EnfSrv_Cd).FirstOrDefault();
            if ((newCondition == null) || (!newCondition.ValuesEqual(holdbackCondition)))
                return true;
        }

        return false;
    }

    private async Task<bool> FinancialTermsAreHigher()
    {
        var currentAppManager = new InterceptionManager(DB, DBfinance, Config, CurrentUser);
        await currentAppManager.LoadApplication(Appl_EnfSrv_Cd, Appl_CtrlCd);
        var currentApp = currentAppManager.InterceptionApplication;
        var newApp = InterceptionApplication;

        decimal currentPeriodicYearlyTotal = CalculateYearlyTotal(currentApp.IntFinH.PymPr_Cd, currentApp.IntFinH.IntFinH_PerPym_Money);
        decimal newPeriodicYearlyTotal = CalculateYearlyTotal(newApp.IntFinH.PymPr_Cd, newApp.IntFinH.IntFinH_PerPym_Money);

        if ((currentApp.IntFinH.IntFinH_LmpSum_Money < newApp.IntFinH.IntFinH_LmpSum_Money) ||
            (currentApp.IntFinH.IntFinH_DefHldbAmn_Money > newApp.IntFinH.IntFinH_DefHldbAmn_Money) ||
            (currentApp.IntFinH.IntFinH_DefHldbPrcnt > newApp.IntFinH.IntFinH_DefHldbPrcnt) ||
            (currentApp.IntFinH.IntFinH_CmlPrPym_Ind > newApp.IntFinH.IntFinH_CmlPrPym_Ind) ||
            (currentPeriodicYearlyTotal < newPeriodicYearlyTotal))
        {
            return true;
        }

        if (currentApp.HldbCnd.Count == newApp.HldbCnd.Count)
        {
            foreach (var currentHoldback in currentApp.HldbCnd)
            {
                if (currentHoldback is not null)
                {
                    var newHoldback = newApp.HldbCnd.Where(m => m.EnfSrv_Cd == currentHoldback.EnfSrv_Cd).FirstOrDefault();
                    if (newHoldback is null)
                        return true;
                    else
                    {
                        if ((currentHoldback.HldbCnd_MxmPerChq_Money < newHoldback.HldbCnd_MxmPerChq_Money) ||
                            (currentHoldback.HldbCnd_SrcHldbAmn_Money > newHoldback.HldbCnd_SrcHldbAmn_Money) ||
                            (currentHoldback.HldbCnd_SrcHldbPrcnt > newHoldback.HldbCnd_SrcHldbPrcnt))
                            return true;
                    }
                }
            }
        }
        else
            return true;

        return false;
    }

    private static decimal CalculateYearlyTotal(string periodicCode, decimal? periodicAmount)
    {
        decimal result = 0M;

        if (periodicAmount is not null)
        {
            result = periodicCode switch
            {
                "A" => 52 * periodicAmount.Value,
                "B" => 26 * periodicAmount.Value,
                "C" => 12 * periodicAmount.Value,
                "D" => 4 * periodicAmount.Value,
                "E" => 2 * periodicAmount.Value,
                "F" => 1 * periodicAmount.Value,
                "G" => 24 * periodicAmount.Value,
                _ => 0M
            };
        }

        return result;
    }

}
