using FOAEA3.Common.Models;
using FOAEA3.Model;
using FOAEA3.Model.Interfaces;
using FOAEA3.Model.Interfaces.Repository;
using System;

namespace FOAEA3.Business.Areas.Application
{
    internal class LicenceDenialValidation : ApplicationValidation
    {
        private LicenceDenialApplicationData LicenceDenialApplication { get; }


        public LicenceDenialValidation(LicenceDenialApplicationData licenceDenialApplication, ApplicationEventManager eventManager,
                                      IRepositories repositories, IFoaeaConfigurationHelper config, FoaeaUser user) :
                                        base(licenceDenialApplication, eventManager, repositories, config, user)
        {
            LicenceDenialApplication = licenceDenialApplication;
        }

        public LicenceDenialValidation(LicenceDenialApplicationData licenceDenialApplication, IRepositories repositories,
                                       IFoaeaConfigurationHelper config, FoaeaUser user) :
                                         base(licenceDenialApplication, repositories, config, user)
        {
            LicenceDenialApplication = licenceDenialApplication;
        }

        public override bool IsValidMandatoryData()
        {
            bool isValid = base.IsValidMandatoryData();

            if ((LicenceDenialApplication.LicSusp_NoticeSentToDbtr_Dte == DateTime.MinValue) ||
                (LicenceDenialApplication.LicSusp_SupportOrder_Dte == DateTime.MinValue) ||
                string.IsNullOrEmpty(LicenceDenialApplication.LicSusp_CourtNme?.Trim()))
            {
                isValid = false;
            }

            return isValid;
        }

        public bool IsC78()
        {
            return LicenceDenialApplication.Appl_Create_Dte > Config.L01NoAffidavitCutoffDate;
        }
    }
}
