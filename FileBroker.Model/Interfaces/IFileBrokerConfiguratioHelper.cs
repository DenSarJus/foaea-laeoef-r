using FOAEA3.Model;
using System.Collections.Generic;

namespace FileBroker.Model.Interfaces;

public interface IFileBrokerConfigurationHelper
{
    ApiConfig ApiRootData { get; }
    ProvincialAuditFileConfig AuditConfig { get; }
    ProvinceConfig ProvinceConfig { get; }
    string EmailRecipient { get; }
    string OpsRecipient { get; }
    string FileBrokerConnection { get; }
    FileBrokerLoginData FileBrokerLogin { get; }
    FoaeaLoginData FoaeaLogin { get; }
    string FTProot { get; }
    string FTPbackupRoot { get; }
    bool LogConsoleOutputToFile { get; }
    List<string> ProductionServers { get; }
    string TermsAcceptedTextEnglish { get; }
    string TermsAcceptedTextFrench { get; }
    TokenConfig Tokens { get; }
}
