using System.Text.RegularExpressions;

namespace FOAEA3.Business.Areas.Application;

internal class InterceptionValidation : ApplicationValidation
{
    private InterceptionApplicationData InterceptionApplication { get; }


    public InterceptionValidation(InterceptionApplicationData interceptionApplication, ApplicationEventManager eventManager,
                                  IRepositories repositories, IFoaeaConfigurationHelper config, FoaeaUser user) :
                                    base(interceptionApplication, eventManager, repositories, config, user)
    {
        InterceptionApplication = interceptionApplication;
    }

    public InterceptionValidation(InterceptionApplicationData interceptionApplication, IRepositories repositories,
                                  IFoaeaConfigurationHelper config, FoaeaUser user) :
                                    base(interceptionApplication, repositories, config, user)
    {
        InterceptionApplication = interceptionApplication;
    }

    public bool ValidFinancialTermsMandatoryData()
    {
        var intFinH = InterceptionApplication.IntFinH;

        if (intFinH is null) return ReturnError("Missing financial terms");
        if (string.IsNullOrEmpty(intFinH.HldbCtg_Cd)) return ReturnError("Missing HldbCtg_Cd in default financial terms");

        if ((intFinH.IntFinH_PerPym_Money.HasValue) && (intFinH.IntFinH_CmlPrPym_Ind is null))
            return ReturnError("Missing IntFinH_CmlPrPym_Ind in default financial terms");

        if ((InterceptionApplication.HldbCnd is not null) && (InterceptionApplication.HldbCnd.Any()))
        {
            foreach (var sourceSpecific in InterceptionApplication.HldbCnd)
            {
                if (string.IsNullOrEmpty(sourceSpecific.EnfSrv_Cd)) ReturnError("Missing EnfSrv_Cd in source specific financial terms");
                if (string.IsNullOrEmpty(sourceSpecific.HldbCtg_Cd)) return ReturnError("Missing HldbCtg_Cd in source specific financial terms");
            }
        }

        return true;
    }

    private bool ReturnError(string message)
    {
        InterceptionApplication.Messages.AddError(message);
        return false;
    }

    public async Task<bool> ValidVariationDefaultHoldbacks()
    {
        bool isValid = true;

        if (!ValidDefaultHoldbackAmount())
            isValid = false;

        if (!ValidVariationIssueDate())
            isValid = false;

        if (!await ValidPaymentPeriodicCode())
            isValid = false;

        if (!ValidPeriodicPaymentAmount())
            isValid = false;

        if (!await ValidLumpSumAmount())
            isValid = false;

        var intFinHdata = InterceptionApplication.IntFinH;

        decimal defHldbAmount = intFinHdata.IntFinH_DefHldbAmn_Money ?? 0.0M;
        int defHldbPercent = intFinHdata.IntFinH_DefHldbPrcnt ?? 0;

        if (!ValidHoldbackCategory(intFinHdata.HldbCtg_Cd, defHldbAmount, defHldbPercent))
            isValid = false;

        if (!await ValidHoldbackTypeCode(intFinHdata.HldbTyp_Cd, defHldbAmount, defHldbPercent))
            isValid = false;

        return isValid;
    }

    public bool ValidVariationSourceSpecificHoldbacks()
    {
        bool isValid = true;

        foreach (var sourceSpecificData in InterceptionApplication.HldbCnd)
        {
            decimal mxmPerChequeAmount = sourceSpecificData.HldbCnd_MxmPerChq_Money ?? 0.0M;
            decimal sourceHoldbackAmount = sourceSpecificData.HldbCnd_SrcHldbAmn_Money ?? 0.0M;
            int sourceHoldbackPercent = sourceSpecificData.HldbCnd_SrcHldbPrcnt ?? 0;

            if (string.IsNullOrEmpty(sourceSpecificData.EnfSrv_Cd) &&
                ((mxmPerChequeAmount > 0.0M) || !string.IsNullOrEmpty(sourceSpecificData.HldbCtg_Cd) ||
                 (sourceHoldbackPercent > 0) || (sourceHoldbackAmount > 0.0M)))
            {
                EventManager.AddEvent(EventCode.C55201_SOURCE_ID_MUST_BE_ENTERED_WHEN_OTHER_FIELDS_ARE_FILLED);
                isValid = false;
            }

            if (!ValidHoldbackCategory(sourceSpecificData.HldbCtg_Cd, sourceHoldbackAmount, sourceHoldbackPercent))
                isValid = false;
        }

        return isValid;
    }

    public async Task CheckCreditorSurname()
    {
        var applications = await DB.InterceptionTable.GetSameCreditorForI01(InterceptionApplication.Appl_CtrlCd,
                                                                                 InterceptionApplication.Subm_SubmCd,
                                                                                 InterceptionApplication.Appl_Dbtr_Entrd_SIN,
                                                                                 InterceptionApplication.Appl_SIN_Cnfrmd_Ind,
                                                                                 InterceptionApplication.ActvSt_Cd);

        if (applications.Count > 0)
        {
            string errorMessage = string.Empty;
            foreach (var application in applications)
            {
                if (!string.IsNullOrEmpty(application.Appl_Crdtr_SurNme) && !string.IsNullOrEmpty(InterceptionApplication.Appl_Crdtr_SurNme))
                {
                    if (application.Appl_Crdtr_SurNme.Trim().ToUpper() == InterceptionApplication.Appl_Crdtr_SurNme.Trim().ToUpper())
                        errorMessage += $"{application.Appl_EnfSrv_Cd.Trim()}-{application.Subm_SubmCd.Trim()}-{application.Appl_CtrlCd.Trim()} ";
                }
            }

            if (!string.IsNullOrEmpty(errorMessage))
                EventManager.AddEvent(EventCode.C50932_THERE_EXISTS_ONE_OR_MORE_ACTIVE_APPLICATIONS_OF_THIS_TYPE_FOR_THE_SAME_DEBTOR___CREDITOR,
                                      eventReasonText: errorMessage.Trim());
        }

    }

    public bool ValidNewFinancialTerms(InterceptionApplicationData currentInterceptionApplication)
    {
        var newFinancialTerms = InterceptionApplication.IntFinH;
        var newSourceSpecificHoldbacks = InterceptionApplication.HldbCnd;

        var currentFinancialTerms = currentInterceptionApplication.IntFinH;

        var holdbackDate = newFinancialTerms.IntFinH_Dte;

        foreach (var holdback in newSourceSpecificHoldbacks)
        {
            if (holdback.IntFinH_Dte != holdbackDate)
            {
                InterceptionApplication.Messages.AddError("Source specific holdback has different IntFinH Date than IntFinH row");
                return false;
            }
        }

        if (newFinancialTerms.IntFinH_Dte < currentFinancialTerms.IntFinH_Dte)
        {
            InterceptionApplication.Messages.AddError("New holdback is dated earlier than the active holdback currently in the system");
            return false;
            // TODO: ask Eric if it is ok for new intfinh to be dated prior to non-active ("I") intfinh?
        }

        if (newFinancialTerms.IntFinH_Dte == currentFinancialTerms.IntFinH_Dte)
        {
            InterceptionApplication.Messages.AddError("New holdback is a duplicate to existing one");
            return false;
            // TODO: ask Eric if it is ok for a new variation to be posted with the same date as an existing active variation, but with different terms?
        }

        return true;

    }

    public bool ValidateFinancialCoreValues()
    {
        bool isValidData = true;
        var appl = InterceptionApplication;
        var intFinH = InterceptionApplication.IntFinH;

        // fix and validate various options
        if (intFinH.IntFinH_DefHldbAmn_Money is null)
        {
            intFinH.IntFinH_DefHldbAmn_Money = 0;
            intFinH.IntFinH_DefHldbAmn_Period = null;
        }
        else
        {
            if (intFinH.IntFinH_DefHldbAmn_Period is null)
            {

                appl.Messages.AddError("Default Holdback Amount (<IntFinH_DefHldbAmn_Money>) provided with no default holdback amount period code (<dat_IntFinH_DefHldbAmn_Period>)");
                isValidData = false;
            }
            else
            {
                if (string.IsNullOrEmpty(intFinH.PymPr_Cd))
                {
                    if (intFinH.IntFinH_DefHldbAmn_Period.ToUpper() != "C")
                    {
                        appl.Messages.AddError("Invalid Default Holdback Amount Period Code (must be monthly) (<dat_IntFinH_DefHldbAmn_Period>)");
                        isValidData = false;
                    }
                }
                else
                {
                    if (intFinH.IntFinH_DefHldbAmn_Period.ToUpper() != intFinH.PymPr_Cd.ToUpper())
                    {
                        appl.Messages.AddError("Default Holdback Amount Period Code and Payment Period Code (both must be Monthly) (<dat_IntFinH_DefHldbAmn_Period> <dat_PymPr_Cd)");
                        isValidData = false;
                    }
                    else
                    {
                        if (intFinH.IntFinH_DefHldbAmn_Period.ToUpper() != "C")
                        {
                            appl.Messages.AddError("Invalid Default Holdback Amount Period Code (must be monthly) (<dat_IntFinH_DefHldbAmn_Period>)");
                            isValidData = false;
                        }
                        if (intFinH.PymPr_Cd.ToUpper() != "C")
                        {
                            appl.Messages.AddError("Invalid Payment Period Code (must be Monthly or N/A when a Default Fixed Amount is chosen) (<dat_PymPr_Cd>)");
                            isValidData = false;
                        }
                    }
                }
            }
        }

        if (intFinH.IntFinH_DefHldbPrcnt is null)
            intFinH.IntFinH_DefHldbPrcnt = 0;
        else if (intFinH.IntFinH_DefHldbPrcnt is < 0 or > 100)
        {
            appl.Messages.AddError("Invalid percentage (<IntFinH_DefHldbPrcnt>) was submitted with an amount < 0 or > 100");
            isValidData = false;
        }

        if (intFinH.IntFinH_DefHldbPrcnt is 0 && intFinH.IntFinH_DefHldbAmn_Money is 0)
        {
            intFinH.HldbTyp_Cd = null;
            intFinH.IntFinH_DefHldbPrcnt = null;
            intFinH.IntFinH_DefHldbAmn_Money = null;
            intFinH.IntFinH_DefHldbAmn_Period = null;
        }
        else if (intFinH.IntFinH_DefHldbPrcnt is > 0 && intFinH.IntFinH_DefHldbAmn_Money is 0)
        {
            intFinH.HldbTyp_Cd = "T";
            intFinH.IntFinH_DefHldbAmn_Money = null;
            intFinH.IntFinH_DefHldbAmn_Period = null;
        }
        else
        {
            intFinH.HldbTyp_Cd = "P";
            intFinH.IntFinH_DefHldbPrcnt = null;
        }

        if (intFinH.IntFinH_PerPym_Money is not null)
        {
            if (intFinH.IntFinH_PerPym_Money == 0)
            {
                intFinH.IntFinH_PerPym_Money = null;
                intFinH.PymPr_Cd = null;
                intFinH.IntFinH_CmlPrPym_Ind = null;
                appl.Messages.AddInformation(ErrorResource.SUCCESS_PERIODIC_PAYMENT_AMOUNT_MISSING);
            }
            else
            {
                if (intFinH.PymPr_Cd is null)
                {
                    appl.Messages.AddError(ErrorResource.PERIODIC_AMOUNT_MISSING_FREQUENCY_CODE);
                    isValidData = false;
                }
                else
                {
                    var rEx = new Regex("^[A-G]$");
                    if (!rEx.IsMatch(intFinH.PymPr_Cd.ToUpper()))
                    {
                        appl.Messages.AddError(ErrorResource.INVALID_FREQUENCY_PAYMENT_CODE);
                        isValidData = false;
                    }
                }
                if (intFinH.IntFinH_CmlPrPym_Ind is null)
                {
                    appl.Messages.AddError(ErrorResource.PERIODIC_PAYMENT_AMOUNT_IS_0_MISSING_CUMULATIVE_PAYMENT_INDICATOR);
                    isValidData = false;
                }
            }
        }
        else
        {
            if (intFinH.PymPr_Cd is not null)
            {
                intFinH.IntFinH_PerPym_Money = null;
                intFinH.PymPr_Cd = null;
                intFinH.IntFinH_CmlPrPym_Ind = null;
                appl.Messages.AddError("Success. Periodic Payment Amount (<dat_IntFinH_Perpym_Money>) was not submitter. All data for Periodic Payment Amount (<dat_IntFinH_Perpym_Money>), Frequency Payment Code (<PymPr_Cd>) and Cumulative Payment Indicator (<dat_IntFinH_CmlPrPym_Ind>) has been removed");
            }
        }

        // CR 672 - The amount in the arrears field (I01) should not be able to be negative via FTP (FTP) 
        if (intFinH.IntFinH_LmpSum_Money < 0)
        {
            appl.Messages.AddError("Lump Sum Amount (<dat_IntFinH_LmpSum_Money>) was submitted with an amount < 0");
            isValidData = false;
        }
        // CR 672 - dat_IntFinH_LmpSum_Money and dat_IntFinH_Perpym_Money both have no value
        if ((intFinH.IntFinH_LmpSum_Money == 0) && (intFinH.IntFinH_PerPym_Money is null))
        {
            appl.Messages.AddError("Lump Sum Amount (<dat_IntFinH_LmpSum_Money>) and Periodic Payment Amount (<dat_IntFinH_Perpym_Money>) both sent without a value.");
            isValidData = false;
        }

        if (intFinH.IntFinH_NextRecalcDate_Cd is not null)
        {
            if ((intFinH.PymPr_Cd is null) || (intFinH.PymPr_Cd.ToUpper().NotIn("A", "B", "C")))
            {
                appl.Messages.AddError("Warning: Next Recalculation Date Code (<dat_IntFinH_NextRecalc_Dte>) can only be used if the payment period is monthly, weekly or bi-weekly. Value will be ignored.");
                intFinH.IntFinH_NextRecalcDate_Cd = null;
            }
            else
            {
                int nextRecalcCode = intFinH.IntFinH_NextRecalcDate_Cd.Value;

                if ((intFinH.PymPr_Cd.ToUpper() == "A") && (nextRecalcCode is < 1 or > 7))
                {
                    appl.Messages.AddError("Warning: Invalid value for Next Recalculation Date Code (<dat_IntFinH_NextRecalc_Dte>). Must be between 1 and 7 for weekly the payment period. Value will be ignored.");
                    intFinH.IntFinH_NextRecalcDate_Cd = null;
                }
                else if ((intFinH.PymPr_Cd.ToUpper() == "B") && (nextRecalcCode is < 1 or > 14))
                {
                    appl.Messages.AddError("Warning: Invalid value for Next Recalculation Date Code (<dat_IntFinH_NextRecalc_Dte>). Must be between 1 and 14 for bi-weekly the payment period. Value will be ignored.");
                    intFinH.IntFinH_NextRecalcDate_Cd = null;
                }
                else if ((intFinH.PymPr_Cd.ToUpper() == "C") && (nextRecalcCode is < 1 or > 31))
                {
                    appl.Messages.AddError("Warning: Invalid value for Next Recalculation Date Code (<dat_IntFinH_NextRecalc_Dte>). Must be between 1 and 31 for monthly the payment period. Value will be ignored.");
                    intFinH.IntFinH_NextRecalcDate_Cd = null;
                }
            }
        }

        return isValidData;
    }


    private bool ValidVariationIssueDate()
    {

        if ((InterceptionApplication.IntFinH.IntFinH_VarIss_Dte is null) ||
            (InterceptionApplication.IntFinH.IntFinH_VarIss_Dte > DateTime.Now))
        {
            EventManager.AddEvent(EventCode.C55101_INVALID_VARIATION_ISSUE_DATE);
            return false;
        }

        return true;
    }

    private bool HasPeriodPaymentAmount()
    {
        return InterceptionApplication.IntFinH.IntFinH_PerPym_Money.HasValue && (InterceptionApplication.IntFinH.IntFinH_PerPym_Money.Value > 0.0M);
    }

    private async Task<bool> ValidLumpSumAmount()
    {
        bool isValid = true;

        decimal maxTotalMoney = InterceptionApplication.IntFinH.IntFinH_MxmTtl_Money ?? 0.0M;

        decimal maxAmntPeriodic = (!string.IsNullOrEmpty(InterceptionApplication.IntFinH.PymPr_Cd)) ?
                                    await CalculateMaxAmountPeriodicForPeriodCode(InterceptionApplication.IntFinH.PymPr_Cd) :
                                    0.0M;

        decimal maxAmnt = maxAmntPeriodic + InterceptionApplication.IntFinH.IntFinH_LmpSum_Money;

        if (maxTotalMoney > maxAmnt)
        {
            EventManager.AddEvent(EventCode.C55105_MAXIMUM_PERIODIC_TOTAL_INCONSISTENT_WITH_PERIODIC_PAYMENTS);
            isValid = false;
        }

        return isValid;
    }

    private async Task<bool> ValidPaymentPeriodicCode()
    {
        bool isValid = true;

        if (!string.IsNullOrEmpty(InterceptionApplication.IntFinH.PymPr_Cd))
        {
            if (!await ValidPaymentPeriod(InterceptionApplication.IntFinH.PymPr_Cd))
            {
                EventManager.AddEvent(EventCode.C55102_INVALID_PERIODIC_INDICATOR);
                isValid = false;
            }
            if (!HasPeriodPaymentAmount())
            {
                EventManager.AddEvent(EventCode.C55103_PERIODIC_INDICATOR_INCONSISTENT_WITH_SUMMONS_PAYMENT_AMOUNT);
                isValid = false;
            }
        }
        else if (HasPeriodPaymentAmount())
        {
            EventManager.AddEvent(EventCode.C55103_PERIODIC_INDICATOR_INCONSISTENT_WITH_SUMMONS_PAYMENT_AMOUNT);
            isValid = false;
        }

        return isValid;
    }

    private async Task<decimal> CalculateMaxAmountPeriodicForPeriodCode(string paymentPeriodicCode)
    {
        decimal maxAmntPeriodic = 0.0M;

        if (InterceptionApplication.IntFinH.IntFinH_PerPym_Money is null)
        {
            await ApplicationManager.AddSystemError(DB, InterceptionApplication.Messages, Config.Recipients.SystemErrorRecipients,
                                              $"CalculateMaxAmountPeriodicForPeriodCode for {InterceptionApplication.Appl_EnfSrv_Cd}-{InterceptionApplication.Appl_CtrlCd}" +
                                              $" (with periodic code {paymentPeriodicCode}) was called even though IntFinH_PerPym_Money is null!");
            return 0.0M;
        }

        if (!string.IsNullOrEmpty(paymentPeriodicCode))
        {
            switch (paymentPeriodicCode)
            {
                case PaymentPeriodicCode.WEEKLY:
                    maxAmntPeriodic = InterceptionApplication.IntFinH.IntFinH_PerPym_Money.Value * 261;
                    break;
                case PaymentPeriodicCode.BIWEEKLY:
                    maxAmntPeriodic = InterceptionApplication.IntFinH.IntFinH_PerPym_Money.Value * 131;
                    break;
                case PaymentPeriodicCode.MONTHLY:
                    maxAmntPeriodic = InterceptionApplication.IntFinH.IntFinH_PerPym_Money.Value * 60;
                    break;
                case PaymentPeriodicCode.QUARTERLY:
                    maxAmntPeriodic = InterceptionApplication.IntFinH.IntFinH_PerPym_Money.Value * 20;
                    break;
                case PaymentPeriodicCode.SEMI_ANNUALLY:
                    maxAmntPeriodic = InterceptionApplication.IntFinH.IntFinH_PerPym_Money.Value * 10;
                    break;
                case PaymentPeriodicCode.ANNUALLY:
                    maxAmntPeriodic = InterceptionApplication.IntFinH.IntFinH_PerPym_Money.Value * 5;
                    break;
                case PaymentPeriodicCode.SEMI_MONTHLY:
                    maxAmntPeriodic = InterceptionApplication.IntFinH.IntFinH_PerPym_Money.Value * 120;
                    break;
            }
        }

        return maxAmntPeriodic;

    }

    private bool ValidHoldbackCategory(string categoryCode, decimal holdbackAmount, int holdbackPercent)
    {
        bool isValid = true;

        switch (categoryCode)
        {
            case HoldbackCategoryCode.NO_HOLDBACK:
                if (holdbackAmount > 0.0M)
                {
                    EventManager.AddEvent(EventCode.C55107_HOLDBACK_AMOUNT_INCONSISTENT_WITH_OTHER_HOLDBACK_FIELDS);
                    isValid = false;
                }
                if (holdbackPercent > 0)
                {
                    EventManager.AddEvent(EventCode.C55106_HOLDBACK_PERC_INCONSISTENT_WITH_OTHER_HOLDBACK_FIELDS);
                    isValid = false;
                }
                break;
            case HoldbackCategoryCode.PERCENTAGE:
                if (holdbackAmount > 0.0M)
                {
                    EventManager.AddEvent(EventCode.C55107_HOLDBACK_AMOUNT_INCONSISTENT_WITH_OTHER_HOLDBACK_FIELDS);
                    isValid = false;
                }
                if (holdbackPercent == 0)
                {
                    EventManager.AddEvent(EventCode.C55106_HOLDBACK_PERC_INCONSISTENT_WITH_OTHER_HOLDBACK_FIELDS);
                    isValid = false;
                }
                break;
            case HoldbackCategoryCode.FIXED_AMOUNT:
                if (holdbackAmount == 0.0M)
                {
                    EventManager.AddEvent(EventCode.C55107_HOLDBACK_AMOUNT_INCONSISTENT_WITH_OTHER_HOLDBACK_FIELDS);
                    isValid = false;
                }
                if (holdbackPercent > 0)
                {
                    EventManager.AddEvent(EventCode.C55106_HOLDBACK_PERC_INCONSISTENT_WITH_OTHER_HOLDBACK_FIELDS);
                    isValid = false;
                }
                break;
        }

        return isValid;
    }

    private bool ValidDefaultHoldbackAmount()
    {
        bool isValid = true;

        decimal defHldbAmount = InterceptionApplication.IntFinH.IntFinH_DefHldbAmn_Money ?? 0.0M;

        string defHldbAmountPr = InterceptionApplication.IntFinH.IntFinH_DefHldbAmn_Period ?? string.Empty;

        if ((defHldbAmount > 0.0M) && string.IsNullOrEmpty(defHldbAmountPr))
        {
            EventManager.AddEvent(EventCode.C55107_HOLDBACK_AMOUNT_INCONSISTENT_WITH_OTHER_HOLDBACK_FIELDS);
            isValid = false;
        }

        return isValid;
    }

    private bool ValidPeriodicPaymentAmount()
    {
        bool isValid = true;

        decimal maxTotalMoney = InterceptionApplication.IntFinH.IntFinH_MxmTtl_Money ?? 0.0M;

        if ((maxTotalMoney > 0.0M) && InterceptionApplication.IntFinH.IntFinH_PerPym_Money.HasValue &&
                                      (InterceptionApplication.IntFinH.IntFinH_PerPym_Money.Value < 0.0M))
        {
            EventManager.AddEvent(EventCode.C55104_MAXIMUM_PERIODIC_AMOUNT_INCONSISTENT_WITH_OTHER_FIELDS);
            isValid = false;
        }

        return isValid;
    }


    private async Task<bool> ValidPaymentPeriod(string paymentPeriodCode)
    {
        var paymentPeriods = await DB.InterceptionTable.GetPaymentPeriods();
        return paymentPeriods.Any(m => (m.PymPr_Cd == paymentPeriodCode.ToUpper()) && (m.ActvSt_Cd == "A"));
    }

    private async Task<bool> ValidHoldbackType(string holdbackTypeCode)
    {
        var holdbackTypes = await DB.InterceptionTable.GetHoldbackTypes();
        return holdbackTypes.Any(m => (m.HldbTyp_Cd == holdbackTypeCode.ToUpper()) && (m.ActvSt_Cd == "A"));
    }

    private async Task<bool> ValidHoldbackTypeCode(string holdbackTypeCode, decimal defHldbAmount, int defHldbPercent)
    {
        bool isValid = true;

        if (!string.IsNullOrEmpty(holdbackTypeCode))
        {
            if (!await ValidHoldbackType(holdbackTypeCode))
            {
                EventManager.AddEvent(EventCode.C55108_INVALID_HOLDBACK_CATEGORY_CODES);
                isValid = false;
            }
        }

        const string PER_TRANSACTION = "P";
        if ((holdbackTypeCode == PER_TRANSACTION) && (InterceptionApplication.IntFinH.HldbCtg_Cd.In(HoldbackCategoryCode.NO_HOLDBACK,
                                                                                HoldbackCategoryCode.PERCENTAGE) ||
                                                     string.IsNullOrEmpty(InterceptionApplication.IntFinH.IntFinH_DefHldbAmn_Period) ||
                                                     (defHldbAmount <= 0.0M) ||
                                                     ((defHldbAmount > 0.0M) && (defHldbPercent > 0))))
        {
            EventManager.AddEvent(EventCode.C55109_HOLDBACK_TYPE_CODE_INCONSISTENT_WITH_OTHER_HOLDBACK_FIELDS);
            isValid = false;
        }

        return isValid;
    }

}
