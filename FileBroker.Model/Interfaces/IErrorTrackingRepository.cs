using System;
using System.Data;
using System.Threading.Tasks;

namespace FileBroker.Model.Interfaces;

public interface IErrorTrackingRepository
{
    Task MessageBrokerError(string errorType, string errorSubject, Exception e, bool displayExceptionError, DataRow row = null);

    Task MessageBrokerError(string errorSubject, string errorMessage);
}
