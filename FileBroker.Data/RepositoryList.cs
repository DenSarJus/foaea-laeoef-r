using FileBroker.Model.Interfaces;

namespace FileBroker.Data
{
    public struct RepositoryList
    {
        public IApiTransferLogRepository ApiTransferLog { get; set; }
        public IFlatFileSpecificationRepository FlatFileSpecs { get; set; }
        public IFileTableRepository FileTable { get; set; }
        public ISettingsRepository Settings { get; set; }
        public IFileAuditRepository FileAudit { get; set; }
        public IProcessParameterRepository ProcessParameterTable { get; set; }
        public IOutboundAuditRepository OutboundAuditTable { get; set; }
        public IErrorTrackingRepository ErrorTrackingTable { get; set; }
        public IMailServiceRepository MailService { get; set; }
        public ITranslationRepository TranslationTable { get; set; }
        public IFundsAvailableIncomingRepository FundsAvailableIncomingTable { get; set; }
        public IRequestLogRepository RequestLogTable { get; set; }
        public ILoadInboundAuditRepository LoadInboundAuditTable { get; set; }
    }
}
