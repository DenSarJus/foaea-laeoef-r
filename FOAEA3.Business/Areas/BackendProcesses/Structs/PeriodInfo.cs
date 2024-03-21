using FOAEA3.Business.BackendProcesses.Enums;

namespace FOAEA3.Business.Areas.BackendProcesses.Structs
{
    public struct PeriodInfo
    {
        public DateTime CalcAcceptedDate { get; set; }
        public EPeriodFrequency PeriodFrequency { get; set; }
        public EStartDateUsed StartDateUsed { get; set; }
    }
}
