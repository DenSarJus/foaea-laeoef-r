using System.Threading.Tasks;

namespace FileBroker.Model.Interfaces;

public interface ISecurityTokenRepository
{
    Task Create(SecurityTokenData securityToken);
    Task<SecurityTokenData> GetTokenData(string token);
    Task MarkTokenAsExpired(string token);
}
