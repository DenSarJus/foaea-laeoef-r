using System.Collections.Generic;
using System.Threading.Tasks;

namespace FileBroker.Model.Interfaces;

public interface IFundsAvailableIncomingRepository
{
    Task<List<FundsAvailableIncomingTrainingData>> GetFundsAvailableIncomingTrainingData(string batchId);
    Task UpdateFundsAvailableIncomingTraining(List<FundsAvailableIncomingTrainingData> data);
}
