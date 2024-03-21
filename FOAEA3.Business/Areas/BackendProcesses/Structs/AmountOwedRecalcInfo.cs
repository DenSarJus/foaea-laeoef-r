namespace FOAEA3.Business.Areas.BackendProcesses.Structs;

public struct AmountOwedRecalcInfo
{
    public OwedTotal OwedData { get; set; }
    public DateTime? NewRecalcDate { get; set; }
    public DateTime? NewFixedAmountRecalcDate { get; set; }
    public PeriodInfo PeriodData { get; set; }
    public decimal? LumpSumDivertedTotal { get; set; }
    public decimal? PerAmtDivertedTotal { get; set; }
}
