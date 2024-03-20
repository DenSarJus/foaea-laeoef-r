using FOAEA3.Model.Enums;
using System.Threading.Tasks;

namespace FOAEA3.Business.Areas.Application
{
    internal partial class LicenceDenialManager
    {
        protected override async Task Process_04_SinConfirmed()
        {
            await base.Process_04_SinConfirmed();

            await SetNewStateTo(ApplicationState.PENDING_ACCEPTANCE_SWEARING_6); // all new L01 applications are C78
        }

        protected override async Task Process_06_PendingAcceptanceSwearing()
        {
            await base.Process_06_PendingAcceptanceSwearing();

            await Validation.AddDuplicateSINWarningEvents();

            await SetNewStateTo(ApplicationState.APPLICATION_ACCEPTED_10); // all new L01 applications are C78
        }

        protected override async Task Process_12_PartiallyServiced()
        {
            await base.Process_12_PartiallyServiced();

            var licenceResponseData = await DB.LicenceDenialResponseTable.GetLastResponseData(Appl_EnfSrv_Cd, Appl_CtrlCd);

            if (licenceResponseData != null)
            {
                switch (licenceResponseData.RqstStat_Cd)
                {
                    case 3:
                        LicenceDenialApplication.LicSusp_AnyLicReinst_Ind = 1;
                        EventManager.AddEvent(EventCode.C50824_A_DEBTOR_LICENCE_HAS_BEEN_SUSPENDED);
                        break;
                    case 5:
                        EventManager.AddEvent(EventCode.C50827_ASSISTANCE_REQUESTED_TO_CORRECTLY_IDENTIFY_DEBTOR,
                                              queue: EventQueue.EventAM);
                        break;
                    case 8:
                        LicenceDenialApplication.LicSusp_AnyLicRvkd_Ind = 1;
                        break;
                    default:
                        break;
                }
            }
        }

    }
}
