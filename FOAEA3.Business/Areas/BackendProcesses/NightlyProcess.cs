﻿using FOAEA3.Model.Interfaces.Repository;
using System.Threading.Tasks;

namespace FOAEA3.Business.BackendProcesses
{
    public class NightlyProcess
    {
        public async Task Run(IRepositories repositories, IRepositories_Finance repositoriesFinance)
        {
            CompletedInterceptionsProcess.Run(); // formerly known as AppDaily

            var amountOwedProcess = new AmountOwedProcess(repositories, repositoriesFinance);
            await amountOwedProcess.RunAsync();

            var divertFundProcess = new DivertFundsProcess(repositories, repositoriesFinance);
            await divertFundProcess.RunAsync();

            ChequeReqProcess.Run();
        }
    }
}
