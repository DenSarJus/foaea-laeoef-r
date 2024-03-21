namespace FOAEA3.Business.Areas.Application;

internal partial class ApplicationManager
{
    public void InvalidStateChange(ApplicationState oldState, ApplicationState newState)
    {
        EventManager.AddEvent(EventCode.C50933_INVALID_OPERATION_FROM_THE_CURRENT_LIFE_STATE,
                              $"Inv. action {(int)oldState} -> {(int)newState}", activeState: "C",
                              updateSubm: DB.UpdateSubmitter);
        Application.Messages.AddError(EventCode.C50933_INVALID_OPERATION_FROM_THE_CURRENT_LIFE_STATE);
    }

    public virtual async Task Process_00_InitialState()
    {
        Application.AppLiSt_Cd = ApplicationState.INITIAL_STATE_0;

        ApplicationState newState = ApplicationState.AWAITING_VALIDATION_2; // default

        Validation.VerifyPresets();

        if (!Validation.IsValidMandatoryData())
        {
            EventManager.AddEvent(EventCode.C50505_MISSING_MANDATORY_FIELDSDEFAULT_FIELD_USED);
            newState = ApplicationState.INVALID_APPLICATION_1;
        }

        if (!ValidationHelper.IsValidSinNumberMod10(Application.Appl_Dbtr_Entrd_SIN, allowEmpty: true))
        {
            EventManager.AddEvent(EventCode.C50523_INVALID_SIN);
            newState = ApplicationState.INVALID_APPLICATION_1;
            Application.Messages.AddError(ReferenceData.Instance().FoaEvents[EventCode.C50523_INVALID_SIN].Description);
        }

        if (Application.Appl_Dbtr_Brth_Dte.HasValue && !Validation.IsValidDebtorAge())
        {
            EventManager.AddEvent(EventCode.C50531_DEBTOR_MUST_BE_AT_LEAST_15_YEARS_OLD);
            newState = ApplicationState.INVALID_APPLICATION_1;
            Application.Messages.AddError(ReferenceData.Instance().FoaEvents[EventCode.C50531_DEBTOR_MUST_BE_AT_LEAST_15_YEARS_OLD].Description);
        }

        bool isValid;
        string reasonText;

        string postalCode = Application.Appl_Dbtr_Addr_PCd;
        string provinceCode = Application.Appl_Dbtr_Addr_PrvCd;
        string cityName = Application.Appl_Dbtr_Addr_CityNme;
        string countryCode = Application.Appl_Dbtr_Addr_CtryCd;

        if (IsAddressMandatory &&
            (string.IsNullOrEmpty(postalCode) || string.IsNullOrEmpty(provinceCode) || string.IsNullOrEmpty(cityName) ||
            string.IsNullOrEmpty(countryCode) || string.IsNullOrEmpty(Application.Appl_Dbtr_Addr_Ln)))
        {
            newState = ApplicationState.INVALID_APPLICATION_1;
            Application.Messages.AddError("Incomplete Debtor Address");
        }

        if (!string.IsNullOrEmpty(postalCode) && !string.IsNullOrEmpty(provinceCode) && !string.IsNullOrEmpty(cityName) &&
            !string.IsNullOrEmpty(countryCode))
        {
            (isValid, reasonText) = await Validation.IsValidPostalCode(postalCode, provinceCode, cityName, countryCode);
            if (!isValid)
            {
                EventManager.AddEvent(EventCode.C50772_INVALID_POSTAL_CODE, reasonText);
                if (Application.Medium_Cd != "FTP")
                {
                    newState = ApplicationState.INVALID_APPLICATION_1;
                    Application.Messages.AddError(ReferenceData.Instance().FoaEvents[EventCode.C50772_INVALID_POSTAL_CODE].Description);
                }
            }
        }

        if (!Validation.IsValidGender())
        {
            EventManager.AddEvent(EventCode.C50515_GENDER_CODE_INCORECT__MALE_ASSIGNED);
            Application.Appl_Dbtr_Gendr_Cd = "M";
        }

        await Validation.ValidateControlCode();

        Validation.ValidateDebtorSurname();

        if (!Validation.IsValidComment())
        {
            Application.Messages.AddError(Resources.ErrorResource.COMMENTS_TOO_LONG_500);
        }

        await SetNewStateTo(newState);
    }

    protected virtual Task Process_01_InvalidApplication()
    {
        Application.AppLiSt_Cd = ApplicationState.INVALID_APPLICATION_1;

        Application.Appl_Dbtr_Cnfrmd_SIN = null;
        Application.Appl_SIN_Cnfrmd_Ind = 0;

        EventManager.AddEvent(EventCode.C50600_INVALID_APPLICATION);

        return Task.CompletedTask;
    }

    protected virtual async Task Process_02_AwaitingValidation()
    {
        Application.AppLiSt_Cd = ApplicationState.AWAITING_VALIDATION_2;

        if (String.IsNullOrEmpty(Application.Appl_Dbtr_Entrd_SIN))
            await SetNewStateTo(ApplicationState.SIN_CONFIRMATION_PENDING_3);
        else
        {
            ApplicationState nextState = ApplicationState.SIN_CONFIRMATION_PENDING_3;

            if (await Validation.IsSameDayApplication())
            {
                EventManager.AddEvent(EventCode.C50530_AN_APPLICATION_USING_THIS_SIN_WAS_ENTERED_IN_SAME_CATEGORY_TODAY);
                nextState = ApplicationState.INVALID_APPLICATION_1;
            }

            await SetNewStateTo(nextState);
        }

    }

    protected virtual Task Process_03_SinConfirmationPending()
    {
        Application.AppLiSt_Cd = ApplicationState.SIN_CONFIRMATION_PENDING_3;

        EventManager.AddEvent(EventCode.C50640_SIN_SENT_TO_HRD_FOR_VALIDATION);
        EventManager.AddSINEvent(EventCode.C50640_SIN_SENT_TO_HRD_FOR_VALIDATION);

        return Task.CompletedTask;
    }

    protected virtual Task Process_04_SinConfirmed()
    {
        Application.AppLiSt_Cd = ApplicationState.SIN_CONFIRMED_4;

        return Task.CompletedTask;
    }

    protected virtual async Task Process_05_SinNotConfirmed()
    {
        Application.AppLiSt_Cd = ApplicationState.SIN_NOT_CONFIRMED_5;
        EventManager.AddEvent(EventCode.C50680_CHANGE_OR_SUPPLY_ADDITIONAL_DEBTOR_INFORMATION_SEE_SIN_VERIFICATION_RESULTS_PAGE_IN_FOAEA_FOR_SPECIFIC_DETAILS,
                              eventReasonText: await GetSINResultsEventText());
    }

    protected virtual Task Process_06_PendingAcceptanceSwearing()
    {
        Application.AppLiSt_Cd = ApplicationState.PENDING_ACCEPTANCE_SWEARING_6;

        return Task.CompletedTask;
    }

    protected virtual Task Process_07_ValidAffidavitNotReceived()
    {
        Application.AppLiSt_Cd = ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7;

        return Task.CompletedTask;
    }

    protected virtual Task Process_09_ApplicationRejected()
    {
        ApplicationState previousState = Application.AppLiSt_Cd;

        Application.ActvSt_Cd = "J";
        Application.AppLiSt_Cd = ApplicationState.APPLICATION_REJECTED_9;

        if (previousState == ApplicationState.PENDING_ACCEPTANCE_SWEARING_6)
            EventManager.AddEvent(EventCode.C50763_AFFIDAVIT_REJECTED_BY_FOAEA);
        else
            EventManager.AddEvent(EventCode.C50760_APPLICATION_REJECTED_AS_CONDITIONS_NOT_MET_IN_TIMEFRAME);

        return Task.CompletedTask;
    }

    protected virtual Task Process_10_ApplicationAccepted()
    {
        Application.AppLiSt_Cd = ApplicationState.APPLICATION_ACCEPTED_10;

        return Task.CompletedTask;
    }

    protected virtual Task Process_11_ApplicationReinstated()
    {
        Application.AppLiSt_Cd = ApplicationState.APPLICATION_REINSTATED_11;

        return Task.CompletedTask;
    }

    protected virtual Task Process_12_PartiallyServiced()
    {
        Application.AppLiSt_Cd = ApplicationState.PARTIALLY_SERVICED_12;

        return Task.CompletedTask;
    }

    protected virtual Task Process_13_FullyServiced()
    {
        Application.AppLiSt_Cd = ApplicationState.FULLY_SERVICED_13;

        return Task.CompletedTask;
    }

    protected virtual Task Process_14_ManuallyTerminated()
    {
        Application.ActvSt_Cd = "X";
        Application.AppLiSt_Cd = ApplicationState.MANUALLY_TERMINATED_14;
        EventManager.AddEvent(EventCode.C50843_APPLICATION_CANCELLED, activeState: "X");

        return Task.CompletedTask;
    }

    protected virtual Task Process_15_Expired()
    {
        Application.ActvSt_Cd = "C";
        Application.AppLiSt_Cd = ApplicationState.EXPIRED_15;
        EventManager.AddEvent(EventCode.C50860_APPLICATION_COMPLETED);

        return Task.CompletedTask;
    }

    protected virtual Task Process_17_FinancialTermsVaried()
    {
        Application.AppLiSt_Cd = ApplicationState.FINANCIAL_TERMS_VARIED_17;

        return Task.CompletedTask;
    }

    protected virtual Task Process_19_AwaitingDocumentsForVariation()
    {
        Application.AppLiSt_Cd = ApplicationState.AWAITING_DOCUMENTS_FOR_VARIATION_19;

        return Task.CompletedTask;
    }

    protected virtual Task Process_35_ApplicationSuspended()
    {
        Application.AppLiSt_Cd = ApplicationState.APPLICATION_SUSPENDED_35;

        return Task.CompletedTask;
    }

    protected virtual Task Process_91_InvalidVariationSource()
    {
        Application.AppLiSt_Cd = ApplicationState.INVALID_VARIATION_SOURCE_91;

        return Task.CompletedTask;
    }

    protected virtual Task Process_92_InvalidVariationFinTerms()
    {
        Application.AppLiSt_Cd = ApplicationState.INVALID_VARIATION_FINTERMS_92;

        return Task.CompletedTask;
    }

    protected virtual Task Process_93_ValidFinancialVariation()
    {
        Application.AppLiSt_Cd = ApplicationState.VALID_FINANCIAL_VARIATION_93;

        return Task.CompletedTask;
    }
}
