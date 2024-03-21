using FOAEA3.Business.Areas.BackendProcesses.Structs;
using FOAEA3.Business.BackendProcesses.Enums;

namespace FOAEA3.Business.Areas.BackendProcesses;

public class AmountOwedProcess
{
    private readonly IRepositories DB;
    private readonly IRepositories_Finance DBfinance;

    public AmountOwedProcess(IRepositories repositories, IRepositories_Finance repositoriesFinance)
    {
        DB = repositories;
        DBfinance = repositoriesFinance;
    }

    public async Task Run(List<SummonsSummaryData> summSmryData = null)
    {

        var prodAudit = DB.ProductionAuditTable;
        var dbSummonsSummary = DBfinance.SummonsSummaryRepository;

        await prodAudit.Insert("Amount Owed", "Amount Owed Started", "O");

        if (summSmryData is null)
            summSmryData = await dbSummonsSummary.GetAmountOwedRecords();
        await CalculateAndUpdateAmountOwed(summSmryData);

        await prodAudit.Insert("Amount Owed", "Amount Owed Completed", "O");
    }

    public async Task<SummonsSummaryData> GetSummonsSummaryData(string enfSrv, string applCtrl)
    {
        var dbSummonsSummary = DBfinance.SummonsSummaryRepository;

        return (await dbSummonsSummary.GetSummonsSummary(enfSrv, applCtrl)).FirstOrDefault();
    }

    public async Task<(SummonsSummaryData summSmryNewData, SummonsSummaryFixedAmountData summSmryFixedAmountNewData)> CalculateAndUpdateAmountOwedForVariation(string enfSrv, string applCtrl)
    {
        var dbSummonsSummary = DBfinance.SummonsSummaryRepository;

        var summSmryData = await dbSummonsSummary.GetSummonsSummary(enfSrv, applCtrl);
        (SummonsSummaryData summSmryNewData,
         SummonsSummaryFixedAmountData summSmryFixedAmountNewData) = await CalculateAndUpdateAmountOwed(summSmryData, isVariation: true);

        return (summSmryNewData, summSmryFixedAmountNewData);
    }

    private async Task<(SummonsSummaryData, SummonsSummaryFixedAmountData)> CalculateAndUpdateAmountOwed(List<SummonsSummaryData> summSmryData,
                                                     DateTime? payableDateOverride = null,
                                                     bool isVariation = false)
    {
        var prodAudit = DB.ProductionAuditTable;

        SummonsSummaryData summSmryNewData = null;
        SummonsSummaryFixedAmountData summSmryFixedAmountNewData = null;

        DateTime payableDate = payableDateOverride ?? DateTime.Now;

        int rowNum = 0;

        if (!isVariation)
            await prodAudit.Insert("Amount Owed", $"Total number of I01 applications to be recalculated: {summSmryData.Count}", "S");
        else
        {
            if (summSmryData.Count == 1)
            {
                var variationData = summSmryData[0];
                await prodAudit.Insert("Amount Owed", $"Variation recalculations for {variationData.Appl_EnfSrv_Cd}-{variationData.Appl_CtrlCd}", "S");
            }
            else
                await prodAudit.Insert("Amount Owed", $"No summSmry data found?!", "S");
        }

        foreach (SummonsSummaryData row in summSmryData)
        {

            AmountOwedRecalcInfo newAmountOwedAndDiverted = await CalculateActualAmountOwed(row.Appl_EnfSrv_Cd, row.Appl_CtrlCd, payableDate, row.SummSmry_Vary_Cnt, row.Start_Dte, isVariation);

            if (newAmountOwedAndDiverted.NewRecalcDate != null)
            {
                (summSmryNewData, summSmryFixedAmountNewData) = await SaveData(row,
                                                                        newAmountOwedAndDiverted.OwedData,
                                                                        newAmountOwedAndDiverted.NewRecalcDate,
                                                                        newAmountOwedAndDiverted.NewFixedAmountRecalcDate,
                                                                        newAmountOwedAndDiverted.LumpSumDivertedTotal,
                                                                        newAmountOwedAndDiverted.PerAmtDivertedTotal,
                                                                        newAmountOwedAndDiverted.PeriodData,
                                                                        payableDate,
                                                                        isVariation);

                if (rowNum > 0 && rowNum % 1000 == 0)
                    await prodAudit.Insert("Amount Owed", $"Record Processed - {rowNum}", "S");

                rowNum++;
            }

        }

        if (!isVariation)
            await prodAudit.Insert("Amount Owed", $"Record Processed - {rowNum}", "O");

        return (summSmryNewData, summSmryFixedAmountNewData);
    }

    public async Task<AmountOwedRecalcInfo> CalculateActualAmountOwed(string enfSrvCd, string ctrlCd, DateTime payableDate, short summSmry_Vary_Cnt, DateTime start_Dte, bool isVariation)
    {
        var dbActiveSummons = DBfinance.ActiveSummonsRepository;

        if (await dbActiveSummons.GetActiveSummonsCore(payableDate, enfSrvCd, ctrlCd) != default)
        {
            ActiveSummonsData activeSummonsData = await dbActiveSummons.GetActiveSummonsData(payableDate, ctrlCd, enfSrvCd, isVariation);

            if (activeSummonsData is not null && activeSummonsData.Appl_RecvAffdvt_Dte is not null)
            {
                (string paymentPeriodCode, string fixedAmountPeriodCode) = GetPeriodTypeCodes(activeSummonsData);

                DateTime acceptedDate = activeSummonsData.Appl_RecvAffdvt_Dte.Value;

                if (activeSummonsData.IntFinH_NextRecalcDate_Cd.HasValue && activeSummonsData.IntFinH_NextRecalcDate_Cd != 0)
                    acceptedDate = AdjustCalcAcceptedDateBasedOnCustomNextRecalcDate(acceptedDate, activeSummonsData.IntFinH_NextRecalcDate_Cd.Value, paymentPeriodCode);

                if (summSmry_Vary_Cnt > 0)
                    acceptedDate = acceptedDate.Date; // why only when there has been a variation? remnant of old logic kept for now

                PeriodInfo periodInfo = CalcAcceptedDateAndPeriodFrequencyUsed(activeSummonsData, paymentPeriodCode, fixedAmountPeriodCode, acceptedDate);

                bool isValid = true;
                if (periodInfo.PeriodFrequency == EPeriodFrequency.Both && (paymentPeriodCode != fixedAmountPeriodCode || paymentPeriodCode != "C"))
                    // only allow both periodic frequency and holdback frequency if they are the same and if the periodic is monthly
                    isValid = false;

                if (isValid)
                {

                    (int currentPeriodCount, int currentPeriodCountFixedAmount) = CalculateCurrentPeriodCounts(payableDate, paymentPeriodCode,
                                                                                                               fixedAmountPeriodCode, periodInfo,
                                                                                                               periodInfo.CalcAcceptedDate.Date);

                    if (isVariation)
                    {
                        var dbGarnPeriod = DBfinance.GarnPeriodRepository;

                        var lmpSumDivertedTtl_Money = activeSummonsData.LmpSumDivertedTtl_Money;
                        var perPymDivertedTtl_Money = activeSummonsData.PerPymDivertedTtl_Money;

                        if (periodInfo.PeriodFrequency == EPeriodFrequency.Both)
                        {
                            (lmpSumDivertedTtl_Money, perPymDivertedTtl_Money) = await dbGarnPeriod.UpdateGarnPeriod(enfSrvCd, ctrlCd, activeSummonsData.IntFinH_LmpSum_Money,
                                                          activeSummonsData.IntFinH_PerPym_Money ?? 0.0m,
                                                          AdjustCalcAcceptedDateBasedOnAcceptedDateAndPeriod(periodInfo.CalcAcceptedDate, acceptedDate, paymentPeriodCode),
                                                          lmpSumDivertedTtl_Money, perPymDivertedTtl_Money);
                        }
                        else
                        {
                            (lmpSumDivertedTtl_Money, perPymDivertedTtl_Money) = await dbGarnPeriod.UpdateGarnPeriod(enfSrvCd, ctrlCd, activeSummonsData.IntFinH_LmpSum_Money,
                                                          activeSummonsData.IntFinH_PerPym_Money ?? 0.0m,
                                                          periodInfo.CalcAcceptedDate,
                                                          lmpSumDivertedTtl_Money, perPymDivertedTtl_Money);
                        }

                        activeSummonsData.LmpSumDivertedTtl_Money = lmpSumDivertedTtl_Money;
                        activeSummonsData.PerPymDivertedTtl_Money = perPymDivertedTtl_Money;

                    }

                    (DateTime? recalcDate, DateTime? fixedAmountRecalcDate) = CalculateRecalcDates(acceptedDate, periodInfo,
                                                                                                   paymentPeriodCode, fixedAmountPeriodCode,
                                                                                                   periodInfo.CalcAcceptedDate,
                                                                                                   currentPeriodCount, currentPeriodCountFixedAmount);
                    (OwedTotal owedData, recalcDate) = await CalculateTotalOwed(enfSrvCd, ctrlCd, activeSummonsData, start_Dte,
                                                                       periodInfo, currentPeriodCount, payableDate,
                                                                       summSmry_Vary_Cnt > 0 ? acceptedDate.Date : acceptedDate, recalcDate);

                    return new AmountOwedRecalcInfo
                    {
                        OwedData = owedData,
                        NewRecalcDate = recalcDate,
                        NewFixedAmountRecalcDate = fixedAmountRecalcDate,
                        PeriodData = periodInfo,
                        LumpSumDivertedTotal = activeSummonsData.LmpSumDivertedTtl_Money,
                        PerAmtDivertedTotal = activeSummonsData.PerPymDivertedTtl_Money
                    };

                }
                else
                {
                    // send email notification about error in amount owed periodic calculations
                    var config = new FoaeaConfigurationHelper();

                    var dbNotification = DB.NotificationService;
                    await dbNotification.SendEmail(subject: "Amount Owed Periodic Calculation Error",
                                             recipient: config.Recipients.EmailRecipients,
                                             body: $"{enfSrvCd}-{ctrlCd}: Invalid periodic amounts \n" +
                                                   $"  Fixed Amount Period: {fixedAmountPeriodCode}\n" +
                                                   $"  Ongoing Period: {paymentPeriodCode}\n" +
                                                   "Cannot have both periodic frequency and holdback frequency if they are the different or if the periodic is not monthly.\n");

                    return new AmountOwedRecalcInfo
                    {
                        OwedData = new OwedTotal(),
                        NewRecalcDate = new DateTime(),
                        NewFixedAmountRecalcDate = new DateTime(),
                        PeriodData = new PeriodInfo(),
                        LumpSumDivertedTotal = activeSummonsData.LmpSumDivertedTtl_Money,
                        PerAmtDivertedTotal = activeSummonsData.PerPymDivertedTtl_Money
                    };
                }
            }
            else
            {
                return new AmountOwedRecalcInfo
                {
                    OwedData = default,
                    NewRecalcDate = null,
                    NewFixedAmountRecalcDate = null,
                    PeriodData = default,
                    LumpSumDivertedTotal = null,
                    PerAmtDivertedTotal = null
                };
            }

        }
        else
        {
            // not active (probably suspended), set next recalc dates to Jan 1, 3000

            var recalcDateSuspended = new DateTime(3000, 1, 1);
            var periodInfoDefault = new PeriodInfo
            {
                PeriodFrequency = EPeriodFrequency.None
            };
            var owedTotalZeroes = new OwedTotal
            {
                FeesOwedTotal = 0.0m,
                LumpSumOwedTotal = 0.0m,
                PeriodicPaymentOwedTotal = 0.0m
            };

            return new AmountOwedRecalcInfo
            {
                OwedData = owedTotalZeroes,
                NewRecalcDate = recalcDateSuspended,
                NewFixedAmountRecalcDate = recalcDateSuspended,
                PeriodData = periodInfoDefault,
                LumpSumDivertedTotal = null,
                PerAmtDivertedTotal = null
            };
        }
    }

    private async Task<(OwedTotal, DateTime?)> CalculateTotalOwed(string appl_EnfSrv_Cd, string appl_CtrlCd,
                                                ActiveSummonsData activeSummonsForDebtor,
                                                DateTime startDate, PeriodInfo periodData, int currentPeriod,
                                                DateTime payableDate,
                                                DateTime acceptedDate, DateTime? periodRecalcDate)
    {
        var dbSummonsSummary = DBfinance.SummonsSummaryRepository;
        var dbInterception = DB.InterceptionTable;
        var dbDivertFunds = DBfinance.DivertFundsRepository;

        bool isFeeCumulative = await dbInterception.IsFeeCumulativeForApplication(appl_EnfSrv_Cd, appl_CtrlCd);

        decimal totalDivertedForCurrentPeriod = await dbDivertFunds.GetTotalDivertedForPeriod(appl_EnfSrv_Cd, appl_CtrlCd, currentPeriod);

        decimal finTermPerPaymentAmount = activeSummonsForDebtor.IntFinH_PerPym_Money ?? 0.0m;
        decimal periodicPaymentDivertedTotal = activeSummonsForDebtor.PerPymDivertedTtl_Money;
        decimal periodicPaymentOwedAmount = 0.0m;

        if ((periodData.PeriodFrequency == EPeriodFrequency.Periodic || periodData.PeriodFrequency == EPeriodFrequency.Both) &&
            (periodData.StartDateUsed == EStartDateUsed.Periodic || periodData.StartDateUsed == EStartDateUsed.Same))
        {
            if (activeSummonsForDebtor.IntFinH_CmlPrPym_Ind.HasValue && activeSummonsForDebtor.IntFinH_CmlPrPym_Ind.Value == 1)
            {
                decimal periodicPaymentOwedCumulativeAmount = currentPeriod * finTermPerPaymentAmount;
                periodicPaymentOwedAmount = periodicPaymentOwedCumulativeAmount - periodicPaymentDivertedTotal;
            }
            else
                periodicPaymentOwedAmount = finTermPerPaymentAmount - totalDivertedForCurrentPeriod;
        }

        decimal lumpDivertedTotal = activeSummonsForDebtor.LmpSumDivertedTtl_Money;
        decimal finTermLumpSumAmount = activeSummonsForDebtor.IntFinH_LmpSum_Money;
        decimal lumpSumOwedTotal = finTermLumpSumAmount - lumpDivertedTotal;

        DateTime finTermSummEffectiveDate = startDate;
        int summYearsActiveCount;

        if (payableDate >= finTermSummEffectiveDate)
        {
            summYearsActiveCount = payableDate.Year - finTermSummEffectiveDate.Year;

            if (finTermSummEffectiveDate.AddYears(summYearsActiveCount) <= payableDate)
                summYearsActiveCount++;
        }
        else
            summYearsActiveCount = 0;

        if (!periodRecalcDate.HasValue)
            periodRecalcDate = acceptedDate.AddYears(summYearsActiveCount);

        decimal feesOwedTotal = await dbSummonsSummary.GetFeesOwedTotal(summYearsActiveCount, finTermSummEffectiveDate, isFeeCumulative);
        decimal feesDivertedTotal = await dbDivertFunds.GetTotalFeesDiverted(appl_EnfSrv_Cd, appl_CtrlCd, isFeeCumulative);
        feesOwedTotal -= feesDivertedTotal;

        return (new OwedTotal { FeesOwedTotal = feesOwedTotal, LumpSumOwedTotal = lumpSumOwedTotal, PeriodicPaymentOwedTotal = periodicPaymentOwedAmount },
                periodRecalcDate);

    }

    private static PeriodInfo CalcAcceptedDateAndPeriodFrequencyUsed(ActiveSummonsData activeSummonsForDebtor, string paymentPeriodCode, string defaultHoldbackPeriodCode, DateTime acceptedDate)
    {
        DateTime? variationCalcDate = activeSummonsForDebtor.IntFinH_VarIss_Dte;

        DateTime calcAcceptedDate = variationCalcDate ?? acceptedDate;

        if (variationCalcDate.HasValue && activeSummonsForDebtor.IntFinH_NextRecalcDate_Cd.HasValue &&
            activeSummonsForDebtor.IntFinH_NextRecalcDate_Cd.Value > 0)
        {
            calcAcceptedDate = AdjustCalcAcceptedDateBasedOnCustomNextRecalcDate(calcAcceptedDate,
                                                                                 activeSummonsForDebtor.IntFinH_NextRecalcDate_Cd.Value,
                                                                                 activeSummonsForDebtor.PymPr_Cd);
        }

        EPeriodFrequency nextPeriodFrequencyCodeUsed = EPeriodFrequency.None;
        EStartDateUsed nextStartDateTypeUsed = EStartDateUsed.Same;

        if (string.IsNullOrEmpty(paymentPeriodCode) && string.IsNullOrEmpty(defaultHoldbackPeriodCode))
        {
            // do nothing, keep CalcAcceptedDate as is
        }
        else if (!string.IsNullOrEmpty(paymentPeriodCode) && string.IsNullOrEmpty(defaultHoldbackPeriodCode))
        {
            calcAcceptedDate = AdjustCalcAcceptedDateBasedOnAcceptedDateAndPeriod(calcAcceptedDate, acceptedDate, paymentPeriodCode);
            nextPeriodFrequencyCodeUsed = EPeriodFrequency.Periodic;
            nextStartDateTypeUsed = EStartDateUsed.Periodic;
        }
        else if (string.IsNullOrEmpty(paymentPeriodCode)) // && (!string.IsNullOrEmpty(defaultHoldbackPeriodCode)))
        {
            calcAcceptedDate = AdjustCalcAcceptedDateBasedOnAcceptedDateAndPeriod(calcAcceptedDate, acceptedDate, defaultHoldbackPeriodCode);
            nextPeriodFrequencyCodeUsed = EPeriodFrequency.FixedAmount;
        }
        else // gets complicated. Pick the nearest date.
        {
            if (AdjustCalcAcceptedDateBasedOnAcceptedDateAndPeriod(calcAcceptedDate, acceptedDate, paymentPeriodCode) == AdjustCalcAcceptedDateBasedOnAcceptedDateAndPeriod(calcAcceptedDate, acceptedDate, defaultHoldbackPeriodCode))
            {
                calcAcceptedDate = AdjustCalcAcceptedDateBasedOnAcceptedDateAndPeriod(calcAcceptedDate, acceptedDate, paymentPeriodCode);
                nextPeriodFrequencyCodeUsed = EPeriodFrequency.Both;
                nextStartDateTypeUsed = EStartDateUsed.Same;
            }

            else if (AdjustCalcAcceptedDateBasedOnAcceptedDateAndPeriod(calcAcceptedDate, acceptedDate, paymentPeriodCode) < AdjustCalcAcceptedDateBasedOnAcceptedDateAndPeriod(calcAcceptedDate, acceptedDate, defaultHoldbackPeriodCode))
            {
                calcAcceptedDate = AdjustCalcAcceptedDateBasedOnAcceptedDateAndPeriod(calcAcceptedDate, acceptedDate, paymentPeriodCode);
                nextPeriodFrequencyCodeUsed = EPeriodFrequency.Both;
                nextStartDateTypeUsed = EStartDateUsed.Periodic;
            }
            else
            {
                calcAcceptedDate = AdjustCalcAcceptedDateBasedOnAcceptedDateAndPeriod(calcAcceptedDate, acceptedDate, defaultHoldbackPeriodCode);
                nextPeriodFrequencyCodeUsed = EPeriodFrequency.Both;
                nextStartDateTypeUsed = EStartDateUsed.FixedAmount;
            }
        }

        return new PeriodInfo { CalcAcceptedDate = calcAcceptedDate, PeriodFrequency = nextPeriodFrequencyCodeUsed, StartDateUsed = nextStartDateTypeUsed };
    }

    private static (string paymentPeriodCode, string) GetPeriodTypeCodes(ActiveSummonsData activeSummonsForDebtor)
    {
        string paymentPeriodCode = string.Empty;
        string fixedAmountPeriodCode = activeSummonsForDebtor.IntFinH_DefHldbAmn_Period;

        if (!string.IsNullOrEmpty(fixedAmountPeriodCode))
            fixedAmountPeriodCode = fixedAmountPeriodCode.Trim();

        if (!string.IsNullOrEmpty(activeSummonsForDebtor.PymPr_Cd) &&
            activeSummonsForDebtor.IntFinH_PerPym_Money.HasValue && activeSummonsForDebtor.IntFinH_PerPym_Money.Value > 0.0m)
            paymentPeriodCode = activeSummonsForDebtor.PymPr_Cd.Trim();

        return (paymentPeriodCode, fixedAmountPeriodCode);
    }

    private static (int paymentPeriodCount, int fixedAmountPeriodCount) CalculateCurrentPeriodCounts(DateTime payableDate,
                                                                                              string paymentPeriodCode,
                                                                                              string fixedAmountPeriodCode,
                                                                                              PeriodInfo periodData,
                                                                                              DateTime calcAcceptedDateNoTime)
    {
        DateTime payableDateNoTime = payableDate.Date;
        int paymentPeriodCount = 0;
        int fixedAmountPeriodCount = 0;

        switch (periodData.PeriodFrequency)
        {
            case EPeriodFrequency.Both:
                paymentPeriodCount = CalculatePeriodCount(paymentPeriodCode, payableDateNoTime, calcAcceptedDateNoTime);
                fixedAmountPeriodCount = CalculatePeriodCount(fixedAmountPeriodCode, payableDateNoTime, calcAcceptedDateNoTime);
                break;
            case EPeriodFrequency.Periodic:
                paymentPeriodCount = CalculatePeriodCount(paymentPeriodCode, payableDateNoTime, calcAcceptedDateNoTime);
                break;
            case EPeriodFrequency.FixedAmount:
                fixedAmountPeriodCount = CalculatePeriodCount(fixedAmountPeriodCode, payableDateNoTime, calcAcceptedDateNoTime);
                break;
            default:
                paymentPeriodCount = 0;
                break;
        }

        return (paymentPeriodCount, fixedAmountPeriodCount);
    }

    public static int CalculatePeriodCount(string paymentPeriodCode, DateTime payableDateNoTime, DateTime calcAcceptedDateNoTime)
    {

        int currentPeriodCount = 0;
        decimal days;

        if (payableDateNoTime >= calcAcceptedDateNoTime)
        {
            switch (paymentPeriodCode)
            {
                case PaymentPeriodicCode.WEEKLY:
                    days = (decimal)(payableDateNoTime - calcAcceptedDateNoTime).TotalDays;
                    currentPeriodCount = (int)Math.Floor(days / 7.0m) + 1;
                    break;

                case PaymentPeriodicCode.BIWEEKLY:
                    days = (decimal)(payableDateNoTime - calcAcceptedDateNoTime).TotalDays;
                    currentPeriodCount = (int)Math.Floor(days / 14.0m) + 1;
                    break;
                case PaymentPeriodicCode.MONTHLY:
                    currentPeriodCount = payableDateNoTime.MonthDifference(calcAcceptedDateNoTime);
                    if (calcAcceptedDateNoTime.Day <= payableDateNoTime.Day)
                        currentPeriodCount++;
                    break;
                case PaymentPeriodicCode.QUARTERLY:
                    currentPeriodCount = calcAcceptedDateNoTime.GetQuarters(payableDateNoTime);
                    if (calcAcceptedDateNoTime.AddMonths(3 * currentPeriodCount) <= payableDateNoTime)
                        currentPeriodCount++;
                    break;
                case PaymentPeriodicCode.SEMI_ANNUALLY:
                    currentPeriodCount = (int)Math.Floor(payableDateNoTime.MonthDifference(calcAcceptedDateNoTime) / 6.0);
                    if (calcAcceptedDateNoTime.AddMonths(currentPeriodCount * 6) <= payableDateNoTime)
                        currentPeriodCount++;
                    break;
                case PaymentPeriodicCode.SEMI_MONTHLY:
                    currentPeriodCount = calcAcceptedDateNoTime.MonthDifference(payableDateNoTime) * 2;

                    if (calcAcceptedDateNoTime.Day < 15 && payableDateNoTime.Day > 14)
                        currentPeriodCount += 2;

                    if (calcAcceptedDateNoTime.Day > 14 && payableDateNoTime.Day > 14)
                        currentPeriodCount++;

                    if (calcAcceptedDateNoTime.Day < 15 && payableDateNoTime.Day < 15)
                        currentPeriodCount++;

                    break;
                case PaymentPeriodicCode.ANNUALLY:
                    currentPeriodCount = payableDateNoTime.Year - calcAcceptedDateNoTime.Year;
                    if (calcAcceptedDateNoTime.AddYears(currentPeriodCount) <= payableDateNoTime)
                        currentPeriodCount++;
                    break;
                default:
                    currentPeriodCount = 0;
                    break;
            }
        }

        return currentPeriodCount;

    }


    private static (DateTime?, DateTime?) CalculateRecalcDates(DateTime acceptedDate, PeriodInfo periodData,
                                                               string paymentPeriodCode, string fixedAmountPeriodCode,
                                                               DateTime calcAcceptedDateNoTime,
                                                               int currentPeriodCount, int currentPeriodCountFixedAmount)
    {
        DateTime? newRecalcDate;
        DateTime? newFixedAmountRecalcDate;

        newRecalcDate = null;
        newFixedAmountRecalcDate = null;
        if (periodData.PeriodFrequency == EPeriodFrequency.Both)
        {
            DateTime newStartDate = AdjustCalcAcceptedDateBasedOnAcceptedDateAndPeriod(calcAcceptedDateNoTime, acceptedDate, paymentPeriodCode);
            newRecalcDate = CalculateRecalcDateFromStartDateForPeriodCount(paymentPeriodCode, currentPeriodCount, newStartDate);

            DateTime newStartDateForFixedAmount = AdjustCalcAcceptedDateBasedOnAcceptedDateAndPeriod(calcAcceptedDateNoTime, acceptedDate, fixedAmountPeriodCode);
            newFixedAmountRecalcDate = CalculateRecalcDateFromStartDateForPeriodCount(fixedAmountPeriodCode, currentPeriodCountFixedAmount, newStartDateForFixedAmount);
        }
        else if (periodData.PeriodFrequency == EPeriodFrequency.Periodic)
        {
            newRecalcDate = CalculateRecalcDateFromStartDateForPeriodCount(paymentPeriodCode, currentPeriodCount, periodData.CalcAcceptedDate);
        }
        else if (periodData.PeriodFrequency == EPeriodFrequency.FixedAmount)
        {
            newFixedAmountRecalcDate = CalculateRecalcDateFromStartDateForPeriodCount(fixedAmountPeriodCode, currentPeriodCountFixedAmount, periodData.CalcAcceptedDate);
        }
        else
            newRecalcDate = CalculateRecalcDateFromStartDateForPeriodCount("", currentPeriodCount, periodData.CalcAcceptedDate);

        return (newRecalcDate, newFixedAmountRecalcDate);
    }

    public static DateTime? CalculateRecalcDateFromStartDateForPeriodCount(string paymentPeriodCode, int currentPeriodCount,
                                                                            DateTime CalcAcceptedDate)
    {
        DateTime? summSmryRecalcDate;

        switch (paymentPeriodCode)
        {
            case PaymentPeriodicCode.WEEKLY: summSmryRecalcDate = CalcAcceptedDate.AddDays(7 * currentPeriodCount); break;
            case PaymentPeriodicCode.BIWEEKLY: summSmryRecalcDate = CalcAcceptedDate.AddDays(14 * currentPeriodCount); break;
            case PaymentPeriodicCode.MONTHLY: summSmryRecalcDate = CalcAcceptedDate.AddMonths(1 * currentPeriodCount); break;
            case PaymentPeriodicCode.QUARTERLY: summSmryRecalcDate = CalcAcceptedDate.AddMonths(3 * currentPeriodCount); break;
            case PaymentPeriodicCode.SEMI_ANNUALLY: summSmryRecalcDate = CalcAcceptedDate.AddMonths(6 * currentPeriodCount); break;
            case PaymentPeriodicCode.ANNUALLY: summSmryRecalcDate = CalcAcceptedDate.AddMonths(12 * currentPeriodCount); break;
            case PaymentPeriodicCode.SEMI_MONTHLY:
                if (DateTime.Now.Day > 15)
                {
                    summSmryRecalcDate = DateTime.Now.AddMonths(1);
                    summSmryRecalcDate = summSmryRecalcDate.Value.AddDays(0 - DateTime.Now.Day + 1); // go to first day
                }
                else
                {
                    // go to 15th of this month
                    summSmryRecalcDate = DateTime.Now.AddDays(15 - DateTime.Now.Day);
                }

                break;
            default:
                summSmryRecalcDate = null;
                break;
        }

        return summSmryRecalcDate;

    }

    public static DateTime AdjustCalcAcceptedDateBasedOnAcceptedDateAndPeriod(DateTime calcAcceptedDate, DateTime acceptedDate,
                                                                              string periodCode)
    {

        DateTime newCalcAcceptedDate = acceptedDate;

        // recalculate calcAcceptedDate from beginning using period
        // if there has been no variations, shouldn't this always be the same as the acceptedDate?

        while (newCalcAcceptedDate < calcAcceptedDate)
        {
            switch (periodCode)
            {
                case PaymentPeriodicCode.WEEKLY: newCalcAcceptedDate = newCalcAcceptedDate.AddDays(7); break;
                case PaymentPeriodicCode.BIWEEKLY: newCalcAcceptedDate = newCalcAcceptedDate.AddDays(14); break;
                case PaymentPeriodicCode.MONTHLY: newCalcAcceptedDate = newCalcAcceptedDate.AddMonths(1); break;
                case PaymentPeriodicCode.QUARTERLY: newCalcAcceptedDate = newCalcAcceptedDate.AddMonths(3); break;
                case PaymentPeriodicCode.SEMI_ANNUALLY: newCalcAcceptedDate = newCalcAcceptedDate.AddMonths(6); break;
                case PaymentPeriodicCode.SEMI_MONTHLY:
                    if (newCalcAcceptedDate.Day >= 15)
                    {   // The 1st day of the next month
                        DateTime tmp = newCalcAcceptedDate.AddMonths(1);
                        newCalcAcceptedDate = new DateTime(tmp.Year, tmp.Month, 1);
                    }
                    else
                    {   // The 15th of the current month
                        DateTime tmp = newCalcAcceptedDate;
                        newCalcAcceptedDate = new DateTime(tmp.Year, tmp.Month, 15);
                    }
                    break;
                case PaymentPeriodicCode.ANNUALLY: newCalcAcceptedDate = newCalcAcceptedDate.AddMonths(12); break;
                default:
                    newCalcAcceptedDate = calcAcceptedDate;
                    break;
            }
        }

        return newCalcAcceptedDate;
    }

    private static DateTime AdjustCalcAcceptedDateBasedOnCustomNextRecalcDate(DateTime calcAcceptedDate,
                                                                              int nextRecalcDateCode,
                                                                              string periodCode)
    {

        DateTime adjustedNextRecalcDate;

        switch (periodCode)
        {
            case "A": // weekly
            case "B": // bi-weekly

                int dow = (int)calcAcceptedDate.DayOfWeek;
                int newDow = nextRecalcDateCode - 1; // make code zero-based to match day of week

                adjustedNextRecalcDate = calcAcceptedDate.AddDays(newDow - dow);

                break;

            case "C": // monthly

                int year = calcAcceptedDate.Year;
                int month = calcAcceptedDate.Month;
                int newDay = nextRecalcDateCode;

                if (newDay > DateTime.DaysInMonth(year, month))
                    newDay = DateTime.DaysInMonth(year, month);

                adjustedNextRecalcDate = new DateTime(year, month, newDay);

                break;

            default:

                adjustedNextRecalcDate = calcAcceptedDate;

                break;
        }

        return adjustedNextRecalcDate;

    }

    private async Task<(SummonsSummaryData, SummonsSummaryFixedAmountData)> SaveData(SummonsSummaryData row, OwedTotal owedData,
                                 DateTime? newRecalcDate, DateTime? newFixedAmountRecalcDate,
                                 decimal? newLumpSumDivertedTotal, decimal? newPerDivertedTotal,
                                 PeriodInfo periodData, DateTime payableDate, bool isVariation)
    {
        var dbSummSmry = DBfinance.SummonsSummaryRepository;
        var dbSummSmryFixedAmount = DBfinance.SummonsSummaryFixedAmountRepository;

        // update summSmry owed amounts

        //SummonsSummaryData summSmryData = dbSummSmry.GetSummonsSummary(row.Appl_EnfSrv_Cd, row.Appl_CtrlCd).First();
        SummonsSummaryData summSmryData = row;

        summSmryData.PreBalance = summSmryData.PerPymOwedTtl_Money + summSmryData.LmpSumOwedTtl_Money;

        summSmryData.Appl_TotalAmnt = owedData.LumpSumOwedTotal + owedData.PeriodicPaymentOwedTotal;
        summSmryData.FeeOwedTtl_Money = owedData.FeesOwedTotal;
        summSmryData.LmpSumOwedTtl_Money = owedData.LumpSumOwedTotal;
        summSmryData.PerPymOwedTtl_Money = owedData.PeriodicPaymentOwedTotal;

        if (newLumpSumDivertedTotal.HasValue)
            summSmryData.LmpSumDivertedTtl_Money = newLumpSumDivertedTotal.Value;

        if (newPerDivertedTotal.HasValue)
            summSmryData.PerPymDivertedTtl_Money = newPerDivertedTotal.Value;

        SummonsSummaryFixedAmountData summSmryFixedAmount = null;
        if (periodData.PeriodFrequency == EPeriodFrequency.Both || periodData.PeriodFrequency == EPeriodFrequency.FixedAmount)
        {

            // update fixed amount

            summSmryFixedAmount = await dbSummSmryFixedAmount.GetSummonsSummaryFixedAmount(row.Appl_EnfSrv_Cd, row.Appl_CtrlCd);

            if (summSmryFixedAmount == default)
            {
                if (!isVariation && newFixedAmountRecalcDate.HasValue)
                    await dbSummSmryFixedAmount.CreateSummonsSummaryFixedAmount(row.Appl_EnfSrv_Cd, row.Appl_CtrlCd,
                                                                          newFixedAmountRecalcDate.Value);
            }
            else
            {
                summSmryFixedAmount.SummSmry_LastFixedAmountCalc_Dte = DateTime.Now;
                if (newFixedAmountRecalcDate.HasValue)
                    summSmryFixedAmount.SummSmry_FixedAmount_Recalc_Dte = newFixedAmountRecalcDate.Value;

                if (!isVariation)
                    await dbSummSmryFixedAmount.UpdateSummonsSummaryFixedAmount(summSmryFixedAmount);
            }

        }

        if (periodData.PeriodFrequency != EPeriodFrequency.FixedAmount)
        {

            // update summsmry recalc date

            summSmryData.SummSmry_LastCalc_Dte = payableDate;
            if (newRecalcDate is not null)
                summSmryData.SummSmry_Recalc_Dte = newRecalcDate.Value;

        }

        // save summsmry changes

        if (isVariation)
            summSmryData.SummSmry_Vary_Cnt += 1;
        else
            await dbSummSmry.UpdateSummonsSummary(summSmryData);

        return (summSmryData, summSmryFixedAmount);

    }

}
