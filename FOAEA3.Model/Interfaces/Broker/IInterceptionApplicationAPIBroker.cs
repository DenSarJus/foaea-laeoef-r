﻿namespace FOAEA3.Model.Interfaces.Broker
{
    public interface IInterceptionApplicationAPIBroker
    {
        IAPIBrokerHelper ApiHelper { get; }
        InterceptionApplicationData GetApplication(string dat_Appl_EnfSrvCd, string dat_Appl_CtrlCd);
        InterceptionApplicationData CreateInterceptionApplication(InterceptionApplicationData interceptionApplication);
        InterceptionApplicationData UpdateInterceptionApplication(InterceptionApplicationData interceptionApplication);
        InterceptionApplicationData VaryInterceptionApplication(InterceptionApplicationData interceptionApplication);
        InterceptionApplicationData TransferInterceptionApplication(InterceptionApplicationData interceptionApplication,
                                                                 string newRecipientSubmitter,
                                                                 string newIssuingSubmitter);

        InterceptionApplicationData ValidateFinancialCoreValues(InterceptionApplicationData application);
    }
}
