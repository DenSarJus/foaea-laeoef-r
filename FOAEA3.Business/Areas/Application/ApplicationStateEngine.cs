namespace FOAEA3.Business.Areas.Application;

internal class ApplicationStateEngine
{
    private readonly Func<Task> Process_00_InitialState;
    private readonly Func<Task> Process_01_InvalidApplication;
    private readonly Func<Task> Process_02_AwaitingValidation;
    private readonly Func<Task> Process_03_SinConfirmationPending;
    private readonly Func<Task> Process_04_SinConfirmed;
    private readonly Func<Task> Process_05_SinNotConfirmed;
    private readonly Func<Task> Process_06_PendingAcceptanceSwearing;
    private readonly Func<Task> Process_07_ValidAffidavitNotReceived;
    private readonly Func<Task> Process_09_ApplicationRejected;
    private readonly Func<Task> Process_10_ApplicationAccepted;
    private readonly Func<Task> Process_11_ApplicationReinstated;
    private readonly Func<Task> Process_12_PartiallyServiced;
    private readonly Func<Task> Process_13_FullyServiced;
    private readonly Func<Task> Process_14_ManuallyTerminated;
    private readonly Func<Task> Process_15_Expired;
    private readonly Func<Task> Process_17_FinancialTermsVaried;
    private readonly Func<Task> Process_19_AwaitingDocumentsForVariation;
    private readonly Func<Task> Process_35_ApplicationSuspended;
    private readonly Func<Task> Process_91_InvalidVariationSource;
    private readonly Func<Task> Process_92_InvalidVariationFinTerms;
    private readonly Func<Task> Process_93_ValidFinancialVariation;
    private readonly Action<ApplicationState, ApplicationState> InvalidStateChange;

    public ApplicationStateEngine(
            Func<Task> process_00_InitialState,
            Func<Task> process_01_InvalidApplication,
            Func<Task> process_02_AwaitingValidation,
            Func<Task> process_03_SinConfirmationPending,
            Func<Task> process_04_SinConfirmed,
            Func<Task> process_05_SinNotConfirmed,
            Func<Task> process_06_PendingAcceptanceSwearing,
            Func<Task> process_07_ValidAffidavitNotReceived,
            Func<Task> process_09_ApplicationRejected,
            Func<Task> process_10_ApplicationAccepted,
            Func<Task> process_11_ApplicationReinstated,
            Func<Task> process_12_PartiallyServiced,
            Func<Task> process_13_FullyServiced,
            Func<Task> process_14_ManuallyTerminated,
            Func<Task> process_15_Expired,
            Func<Task> process_17_FinancialTermsVaried,
            Func<Task> process_19_AwaitingDocumentsForVariation,
            Func<Task> process_35_ApplicationSuspended,
            Func<Task> process_91_InvalidVariationSource,
            Func<Task> process_92_InvalidVariationFinTerms,
            Func<Task> process_93_ValidFinancialVariation,
            Action<ApplicationState, ApplicationState> invalidStateChange
        )
    {
        Process_00_InitialState = process_00_InitialState;
        Process_01_InvalidApplication = process_01_InvalidApplication;
        Process_02_AwaitingValidation = process_02_AwaitingValidation;
        Process_03_SinConfirmationPending = process_03_SinConfirmationPending;
        Process_04_SinConfirmed = process_04_SinConfirmed;
        Process_05_SinNotConfirmed = process_05_SinNotConfirmed;
        Process_06_PendingAcceptanceSwearing = process_06_PendingAcceptanceSwearing;
        Process_07_ValidAffidavitNotReceived = process_07_ValidAffidavitNotReceived;
        Process_09_ApplicationRejected = process_09_ApplicationRejected;
        Process_10_ApplicationAccepted = process_10_ApplicationAccepted;
        Process_11_ApplicationReinstated = process_11_ApplicationReinstated;
        Process_12_PartiallyServiced = process_12_PartiallyServiced;
        Process_13_FullyServiced = process_13_FullyServiced;
        Process_14_ManuallyTerminated = process_14_ManuallyTerminated;
        Process_15_Expired = process_15_Expired;
        Process_17_FinancialTermsVaried = process_17_FinancialTermsVaried;
        Process_19_AwaitingDocumentsForVariation = process_19_AwaitingDocumentsForVariation;
        Process_35_ApplicationSuspended = process_35_ApplicationSuspended;
        Process_91_InvalidVariationSource = process_91_InvalidVariationSource;
        Process_92_InvalidVariationFinTerms = process_92_InvalidVariationFinTerms;
        Process_93_ValidFinancialVariation = process_93_ValidFinancialVariation;
        InvalidStateChange = invalidStateChange;
    }

    public Dictionary<ApplicationState, List<ApplicationState>> ValidStateChange = new()
    {
        {
            ApplicationState.INITIAL_STATE_0,
            new List<ApplicationState> {
                    ApplicationState.INVALID_APPLICATION_1,
                    ApplicationState.AWAITING_VALIDATION_2
                }
        },
        {
            ApplicationState.INVALID_APPLICATION_1,
            new List<ApplicationState> {
                    ApplicationState.INITIAL_STATE_0,
                    ApplicationState.APPLICATION_REJECTED_9,
                    ApplicationState.MANUALLY_TERMINATED_14
                }
        },
        {
            ApplicationState.AWAITING_VALIDATION_2,
            new List<ApplicationState> {
                    ApplicationState.INVALID_APPLICATION_1,
                    ApplicationState.SIN_CONFIRMATION_PENDING_3,
                    ApplicationState.SIN_CONFIRMED_4
                }
        },
        {
            ApplicationState.SIN_CONFIRMATION_PENDING_3,
            new List<ApplicationState> {
                    ApplicationState.SIN_CONFIRMATION_PENDING_3,
                    ApplicationState.SIN_CONFIRMED_4,
                    ApplicationState.SIN_NOT_CONFIRMED_5,
                    ApplicationState.APPLICATION_REJECTED_9,
                    ApplicationState.MANUALLY_TERMINATED_14
                }
        },
        {
            ApplicationState.SIN_CONFIRMED_4,
            new List<ApplicationState> {
                    ApplicationState.PENDING_ACCEPTANCE_SWEARING_6
                }
        },
        {
            ApplicationState.SIN_NOT_CONFIRMED_5,
            new List<ApplicationState> {
                    ApplicationState.INITIAL_STATE_0,
                    ApplicationState.INVALID_APPLICATION_1,
                    ApplicationState.SIN_CONFIRMED_4,
                    ApplicationState.SIN_NOT_CONFIRMED_5,
                    ApplicationState.APPLICATION_REJECTED_9,
                    ApplicationState.MANUALLY_TERMINATED_14
                }
        },
        {
            ApplicationState.PENDING_ACCEPTANCE_SWEARING_6,
            new List<ApplicationState> {
                    ApplicationState.INVALID_APPLICATION_1,
                    ApplicationState.PENDING_ACCEPTANCE_SWEARING_6,
                    ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7,
                    ApplicationState.APPLICATION_REJECTED_9,
                    ApplicationState.APPLICATION_ACCEPTED_10,
                    ApplicationState.MANUALLY_TERMINATED_14
                }
        },
        {
            ApplicationState.APPLICATION_ACCEPTED_10,
            new List<ApplicationState> {
                    ApplicationState.APPLICATION_ACCEPTED_10,
                    ApplicationState.PARTIALLY_SERVICED_12,
                    ApplicationState.FULLY_SERVICED_13,
                    ApplicationState.MANUALLY_TERMINATED_14,
                    ApplicationState.EXPIRED_15
                }
        },
        {
            ApplicationState.PARTIALLY_SERVICED_12,
            new List<ApplicationState> {
                    ApplicationState.PARTIALLY_SERVICED_12,
                    ApplicationState.FULLY_SERVICED_13,
                    ApplicationState.MANUALLY_TERMINATED_14,
                    ApplicationState.EXPIRED_15
                }
        }
    };

    private Dictionary<ApplicationState, Func<Task>> ApplicationStateAction =>
               new()
               {
                   { ApplicationState.INITIAL_STATE_0, Process_00_InitialState },
                   { ApplicationState.INVALID_APPLICATION_1, Process_01_InvalidApplication },
                   { ApplicationState.AWAITING_VALIDATION_2, Process_02_AwaitingValidation },
                   { ApplicationState.SIN_CONFIRMATION_PENDING_3, Process_03_SinConfirmationPending },
                   { ApplicationState.SIN_CONFIRMED_4, Process_04_SinConfirmed },
                   { ApplicationState.SIN_NOT_CONFIRMED_5, Process_05_SinNotConfirmed },
                   { ApplicationState.PENDING_ACCEPTANCE_SWEARING_6, Process_06_PendingAcceptanceSwearing },
                   { ApplicationState.VALID_AFFIDAVIT_NOT_RECEIVED_7, Process_07_ValidAffidavitNotReceived },
                   { ApplicationState.APPLICATION_REJECTED_9, Process_09_ApplicationRejected },
                   { ApplicationState.APPLICATION_ACCEPTED_10, Process_10_ApplicationAccepted },
                   { ApplicationState.APPLICATION_REINSTATED_11, Process_11_ApplicationReinstated },
                   { ApplicationState.PARTIALLY_SERVICED_12, Process_12_PartiallyServiced },
                   { ApplicationState.FULLY_SERVICED_13, Process_13_FullyServiced },
                   { ApplicationState.MANUALLY_TERMINATED_14, Process_14_ManuallyTerminated },
                   { ApplicationState.EXPIRED_15, Process_15_Expired },
                   { ApplicationState.FINANCIAL_TERMS_VARIED_17, Process_17_FinancialTermsVaried },
                   { ApplicationState.AWAITING_DOCUMENTS_FOR_VARIATION_19, Process_19_AwaitingDocumentsForVariation },
                   { ApplicationState.APPLICATION_SUSPENDED_35, Process_35_ApplicationSuspended },
                   { ApplicationState.INVALID_VARIATION_SOURCE_91, Process_91_InvalidVariationSource },
                   { ApplicationState.INVALID_VARIATION_FINTERMS_92, Process_92_InvalidVariationFinTerms },
                   { ApplicationState.VALID_FINANCIAL_VARIATION_93, Process_93_ValidFinancialVariation }
               };

    public bool IsValidStateChange(ApplicationState oldState, ApplicationState newState)
    {
        if (ValidStateChange.TryGetValue(oldState, out List<ApplicationState> validChoices))
            return validChoices.Contains(newState);
        else
            return false;
    }

    public async Task SetNewStateTo(ApplicationState oldState, ApplicationState newState)
    {
        if (IsValidStateChange(oldState, newState))
        {
            var stateAction = ApplicationStateAction[newState];
            await stateAction();
        }
        else
            InvalidStateChange(oldState, newState);
    }
}
