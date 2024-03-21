namespace FOAEA3.Business.Areas.Application;

internal class TracingValidation : ApplicationValidation
{
    private TracingApplicationData TracingApplication { get; }
    private bool HasSignedC78 { get; set; }
    private DateTime? C78EffectiveDateTime { get; set; }

    public TracingValidation(TracingApplicationData tracingApplication, ApplicationEventManager eventManager,
                             IRepositories db, IFoaeaConfigurationHelper config, FoaeaUser user) :
                                base(tracingApplication, eventManager, db, config, user)
    {
        TracingApplication = tracingApplication;
        _ = GetC78infoFromEnfSrv();
    }

    public TracingValidation(TracingApplicationData tracingApplication, IRepositories repositories,
                             IFoaeaConfigurationHelper config, FoaeaUser user) : base(tracingApplication, repositories, config, user)
    {
        TracingApplication = tracingApplication;
        _ = GetC78infoFromEnfSrv();
    }

    public async Task GetC78infoFromEnfSrv()
    {
        if ((TracingApplication is null) || string.IsNullOrEmpty(TracingApplication.Appl_EnfSrv_Cd))
        {
            HasSignedC78 = false;
            C78EffectiveDateTime = null;
        }
        else
        {
            string enfSrv = TracingApplication.Appl_EnfSrv_Cd.Trim();
            var enfSrvData = (await DB.EnfSrvTable.GetEnfService(enforcementServiceCode: enfSrv)).FirstOrDefault();
            if (enfSrvData is not null)
            {
                HasSignedC78 = enfSrvData.HasSignedC78;
                C78EffectiveDateTime = enfSrvData.C78EffectiveDateTime;
            }
            else
            {
                HasSignedC78 = false;
                C78EffectiveDateTime = null;
            }
        }
    }

    public bool IsC78()
    {
        if (TracingApplication.DeclarationIndicator)
            return true;
        else if (HasSignedC78)
            return TracingApplication.Appl_Create_Dte >= C78EffectiveDateTime;
        else
            return false;
    }

    public override bool IsValidMandatoryData()
    {
        bool isSuccess = base.IsValidMandatoryData();

        if (IsC78())
        {
            isSuccess = isSuccess && IsValidDeclaration();
            isSuccess = isSuccess && IsValidPurpose();
            isSuccess = isSuccess && IsValidRequestedTracingData();
            isSuccess = isSuccess && IsValidRequestedSinData();
            isSuccess = isSuccess && IsValidRequestedFinancialsData();
            isSuccess = isSuccess && IsValidRequest();

        }
        else
        {
            if (!string.IsNullOrEmpty(TracingApplication.Medium_Cd) && (TracingApplication.Medium_Cd.ToUpper() == "FTP"))
            {
                isSuccess = IsValidMandatory(isSuccess, TracingApplication.Appl_Crdtr_FrstNme, Resources.ErrorResource.MISSING_CREDITOR_FIRST_NAME);
                isSuccess = IsValidMandatory(isSuccess, TracingApplication.Appl_Crdtr_SurNme, Resources.ErrorResource.MISSING_CREDITOR_SURNAME);
                isSuccess = IsValidMandatory(isSuccess, TracingApplication.FamPro_Cd, Resources.ErrorResource.MISSING_FAMPRO);
                //                    isSuccess = IsValidMandatory(isSuccess, TracingApplication.Trace_Breach_Text, Resources.ErrorResource.MISSING_BREACH);
            }
        }

        return isSuccess;
    }

    private bool IsValidDeclaration()
    {
        string declaration = TracingApplication.Declaration?.Trim();
        if (declaration is not null &&
            (declaration.Equals(Config.TracingDeclaration.English, StringComparison.InvariantCultureIgnoreCase) ||
             declaration.Equals(Config.TracingDeclaration.French, StringComparison.InvariantCultureIgnoreCase)))
        {
            TracingApplication.DeclarationIndicator = true;
            return true;
        }
        else
        {
            TracingApplication.Messages.AddError("Invalid or missing declaration.");
            return false;
        }
    }

    private bool IsValidPurpose()
    {
        if (!TracingApplication.AppReas_Cd.In("1", "2", "3"))
        {
            TracingApplication.Messages.AddError(ErrorResource.INVALID_PURPOSE);
            return false;
        }
        else
            return true;
    }

    private bool IsValidRequestedTracingData()
    {
        if (IsC78() && (TracingApplication.AppReas_Cd == "3") &&
            TracingApplication.TraceInformation.In<short>((short)TracingInformationType.ResidentialAddress_EmployerAddress_Name,
                                                          (short)TracingInformationType.EmployerAddress_Name))
        {
            TracingApplication.Messages.AddError(ErrorResource.INVALID_TRACING_INFORMATION_TYPE);
            return false;
        }
        else
            return true;
    }

    private bool IsValidRequestedSinData()
    {
        if (IsC78() && (TracingApplication.AppReas_Cd != "1") && TracingApplication.IncludeSinInformation)
        {
            TracingApplication.Messages.AddError(ErrorResource.INVALID_REQUEST_SIN_DATA);
            return false;
        }
        else
            return true;
    }

    private bool IsValidRequestedFinancialsData()
    {
        if (IsC78() && (TracingApplication.AppReas_Cd != "1") && TracingApplication.IncludeFinancialInformation)
        {
            TracingApplication.Messages.AddError(ErrorResource.INVALID_REQUEST_FINANCIAL_DATA);
            return false;
        }
        else
            return true;
    }

    private bool IsValidRequest()
    {
        if (IsC78() && !TracingApplication.IncludeFinancialInformation && !TracingApplication.IncludeSinInformation &&
           (TracingApplication.TraceInformation == 0))
        {
            TracingApplication.Messages.AddError(ErrorResource.INVALID_MUST_REQUEST_DATA);
            return false;
        }
        else
            return true;
    }
}
