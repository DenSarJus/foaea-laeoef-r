namespace FOAEA3.Business.Areas.Application;

internal partial class LicenceDenialTerminationManager
{
    protected override async Task Process_02_AwaitingValidation()
    {
        await SetNewStateTo(ApplicationState.APPLICATION_ACCEPTED_10);
    }
}
