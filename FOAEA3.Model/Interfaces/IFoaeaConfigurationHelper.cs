using System;
using System.Collections.Generic;

namespace FOAEA3.Model.Interfaces
{
    public interface IFoaeaConfigurationHelper
    {
        string FoaeaConnection { get; }
        RecipientsConfig Recipients { get; }
        TokenConfig Tokens { get; }
        List<string> ProductionServers { get; }
        public List<string> AutoSwear { get; }
        public List<string> AutoAccept { get; }
        List<string> ESDsites { get; }
        DeclarationData LicenceDenialDeclaration { get; }
        DeclarationData TracingDeclaration { get; }
        string TaxFormsRootPath { get; }
        DateTime L01NoAffidavitCutoffDate { get; }
    }
}
