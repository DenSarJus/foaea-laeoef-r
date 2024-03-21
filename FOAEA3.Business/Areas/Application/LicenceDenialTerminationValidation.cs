namespace FOAEA3.Business.Areas.Application;

internal class LicenceDenialTerminationValidation : ApplicationValidation
{
    private LicenceDenialApplicationData LicenceDenialTerminationApplication { get; }


    public LicenceDenialTerminationValidation(LicenceDenialApplicationData licenceDenialTerminationApplication, ApplicationEventManager eventManager,
                                  IRepositories repositories, IFoaeaConfigurationHelper config, FoaeaUser user) :
                                    base(licenceDenialTerminationApplication, eventManager, repositories, config, user)
    {
        LicenceDenialTerminationApplication = licenceDenialTerminationApplication;
    }

    public LicenceDenialTerminationValidation(LicenceDenialApplicationData licenceDenialTerminationApplication, IRepositories repositories,
                                  IFoaeaConfigurationHelper config, FoaeaUser user) :
                                    base(licenceDenialTerminationApplication, repositories, config, user)
    {
        LicenceDenialTerminationApplication = licenceDenialTerminationApplication;
    }

    public override bool IsValidMandatoryData()
    {
        bool isValid = base.IsValidMandatoryData();

        if (string.IsNullOrEmpty(LicenceDenialTerminationApplication.LicSusp_Dbtr_LastAddr_Ln?.Trim()) ||
            string.IsNullOrEmpty(LicenceDenialTerminationApplication.LicSusp_Dbtr_LastAddr_CityNme?.Trim()) ||
            string.IsNullOrEmpty(LicenceDenialTerminationApplication.LicSusp_Dbtr_LastAddr_PrvCd?.Trim()) ||
            string.IsNullOrEmpty(LicenceDenialTerminationApplication.LicSusp_Dbtr_LastAddr_CtryCd?.Trim()) ||
            string.IsNullOrEmpty(LicenceDenialTerminationApplication.LicSusp_Dbtr_LastAddr_PCd?.Trim()) ||
            string.IsNullOrEmpty(LicenceDenialTerminationApplication.LicSusp_Appl_CtrlCd?.Trim()))
        {
            isValid = false;
        }

        return isValid;
    }
}
