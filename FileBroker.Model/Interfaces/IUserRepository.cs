using System.Threading.Tasks;

namespace FileBroker.Model.Interfaces;

public interface IUserRepository
{
    Task<UserData> GetUserByName(string userName);
    Task<UserData> GetUserById(int userId);
}
