using DBHelper;
using TestData;
using TestData.Data;
using TestData.TestDataBase;
using Xunit;
using Xunit.Abstractions;
using System.Linq;
using FOAEA3.Resources.Helpers;
using System.Threading.Tasks;
using FOAEA3.Business.Areas.BackendProcesses;

namespace BackendProcesses.Business.Tests
{
    public class AmountOwedTests
    {
        private static int testID = 0;

        private readonly ITestOutputHelper output;
        private readonly DebugLogger logger;

        private readonly AmountOwedProcess backendProcess;

        public AmountOwedTests(ITestOutputHelper output)
        {
            testID++;

            this.output = output;

            logger = new DebugLogger(@"C:\_UnitTestLogging\UnitTestLog.md")
            {
                IncludeTimeStamp = false
            };

            var inMemRepository = new InMemory_Repositories();
            var inMemRepositoryFinance = new InMemory_RepositoriesFinance();

            backendProcess = new AmountOwedProcess(inMemRepository, inMemRepositoryFinance);
        }

        private static void ResetTestData(int periodCount, decimal periodicPaymentOwed, string periodicPaymentCode = "C")
        {
            ApplicationTestData.SetupApplicationTestData(periodCount, periodicPaymentCode);
            InterceptionFinHoldbackTestData.SetupInterceptionFinHoldbackTestData(periodCount, periodicPaymentOwed, periodicPaymentCode);
            SummSmryTestData.SetupAmountOwedTestData(periodCount, periodicPaymentCode);

            GarnPeriodTestData.SetupGarnPeriodTestData(periodCount);
            SummFAFRTestData.SetupSummFAFRTestData(periodCount);
            SummDFTestData.SetupSummDFTestData(periodCount);
        }
                
        [Theory]
        [InlineData(6, 13, "A")] // "A" => weekly
        [InlineData(3, 13, "B")] // "B" => every 2 weeks
        [InlineData(2, 13, "C")] // "C" => monthly
        [InlineData(2, 13, "D")] // "D" => quaterly
        [InlineData(2, 13, "E")] // "E" => semi-annually
        [InlineData(2, 13, "F")] // "F" => annually
        //[InlineData(4, 13, "G")] // "G" => semi-monthly
        public async Task VariousPeriod_NoDivertReceived_PeriodicOwed_Test(int periodCount, decimal periodicPaymentOwed, string periodicPaymentCode)
        {
            // Arrange
            ResetTestData(periodCount, periodicPaymentOwed, periodicPaymentCode);

            LogTestData("VariousPeriod_NoDivertReceived_PeriodicOwed_Test: Initial Data", "ON01", "00002", periodicPaymentCode);

            // Act
            await backendProcess.Run();
            var data = await backendProcess.GetSummonsSummaryData("ON01", "00002");

            LogTestData("VariousPeriod_NoDivertReceived_PeriodicOwed_Test: After Amount Owed Recalc", "ON01", "00002", periodicPaymentCode);

            // Assert
            Assert.Equal((periodCount+1) * periodicPaymentOwed, data.PerPymOwedTtl_Money);
        }

        private void LogTestData(string testName, string enfSrv, string ctrlCd, string periodPaymentCode)
        {
            logger.AppendLine("");
            logger.AppendLine($"# Test# {testID} ({periodPaymentCode})");

            logger.AppendLine("");
            logger.AppendLine($"## TEST: {testName}");

            var a = InMemData.ApplicationTestData.Where(m => m.Appl_EnfSrv_Cd == enfSrv && m.Appl_CtrlCd == ctrlCd).FirstOrDefault();

            logger.AppendLine("");
            logger.AppendLine("**Application**");
            logger.AppendLine("");
            logger.AppendLine("| Item | Value | Item | Value |");
            logger.AppendLine("| ----------- | ----------- | ----------- | ----------- |");
            logger.AppendLine($"| Id | {a.Appl_EnfSrv_Cd}-{a.Appl_CtrlCd} | State | {a.AppLiSt_Cd} |");
            logger.AppendLine($"| Legal Date | {a.Appl_Lgl_Dte} | Date Received | {a.Appl_Rcptfrm_Dte} |");
            logger.AppendLine($"| Recv Affdvt Date | {a.Appl_RecvAffdvt_Dte} | Create Date | {a.Appl_Create_Dte} |");

            var i = InMemData.IntFinHoldbackTestData.Where(m => m.Appl_EnfSrv_Cd == enfSrv && m.Appl_CtrlCd == ctrlCd).FirstOrDefault();

            logger.AppendLine("");
            logger.AppendLine("**Financial Terms**");
            logger.AppendLine("");
            logger.AppendLine("| Item | Value | Item | Value |");
            logger.AppendLine("| ----------- | ----------- | ----------- | ----------- |");
            logger.AppendLine($"| Payment period code | {i.PymPr_Cd} | | |");
            logger.AppendLine($"| Lump sum amount | {i.IntFinH_LmpSum_Money} | Periodic Payments | {i.IntFinH_PerPym_Money} |");
            logger.AppendLine($"| Fin Term Date | {i.IntFinH_Dte} | Fin Term Affdvt Receipt Date | {i.IntFinH_RcvtAffdvt_Dte} |");

            var s = InMemData.SummSmryTestData.Where(m => m.Appl_EnfSrv_Cd == enfSrv && m.Appl_CtrlCd == ctrlCd).FirstOrDefault();

            logger.AppendLine("");
            logger.AppendLine("**SummSmry Data**");
            logger.AppendLine("");
            logger.AppendLine("| Item | Value | Item | Value | Item | Value |");
            logger.AppendLine("| ----------- | ----------- | ----------- | ----------- | ----------- | ----------- |");
            logger.AppendLine($"| Start Date | {s.Start_Dte.ToString(DateTimeExtensions.FOAEA_DATE_FORMAT)} | End Date | {s.End_Dte.ToString(DateTimeExtensions.FOAEA_DATE_FORMAT)} | | |");
            logger.AppendLine($"| Lump Sum Diverted | {s.LmpSumDivertedTtl_Money} | Periodic Diverted | {s.PerPymDivertedTtl_Money} | Fees Diverted | {s.FeeDivertedTtl_Money} |");
            logger.AppendLine($"| Lump Sum Owed | {s.LmpSumOwedTtl_Money} | Periodic Owed | {s.PerPymOwedTtl_Money} | Fees Owed | {s.FeeOwedTtl_Money} |");
            logger.AppendLine($"| Total Amount | {s.Appl_TotalAmnt} |  |  |  |  |");
            logger.AppendLine($"| Last Calc | {s.SummSmry_LastCalc_Dte?.ToString(DateTimeExtensions.FOAEA_DATE_FORMAT)} | Recalc Dte | {s.SummSmry_Recalc_Dte?.ToString(DateTimeExtensions.FOAEA_DATE_FORMAT)} | Vary Count | {s.SummSmry_Vary_Cnt} |");

            logger.AppendLine("");
            logger.AppendLine("<div style='page-break-after: always'></div>");
            logger.AppendLine("");

        }

    }
}
