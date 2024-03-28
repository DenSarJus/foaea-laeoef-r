using DBHelper;
using FileBroker.Model;
using FileBroker.Model.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FileBroker.Data.DB;

public class DBSecurityToken : ISecurityTokenRepository
{
    private IDBToolsAsync MainDB { get; }

    public DBSecurityToken(IDBToolsAsync mainDB)
    {
        MainDB = mainDB;
    }

    public async Task Create(SecurityTokenData securityToken)
    {
        var parameters = new Dictionary<string, object>
            {
                {"Token", securityToken.Token},
                {"TokenExpiration", securityToken.TokenExpiration},
                {"RefreshToken", securityToken.RefreshToken},
                {"RefreshTokenExpiration", securityToken.RefreshTokenExpiration},
                {"UserId", securityToken.UserId},
                {"EmailAddress", securityToken.EmailAddress},
                {"SecurityRole", securityToken.SecurityRole}
            };

        if (!string.IsNullOrEmpty(securityToken.FromRefreshToken))
            parameters.Add("FromRefreshToken", securityToken.FromRefreshToken);

        await MainDB.ExecProcAsync("SecurityToken_Insert", parameters);
    }

    public async Task<SecurityTokenData> GetTokenData(string token)
    {
        var parameters = new Dictionary<string, object>
        {
            {"Token", token}
        };

        var data = await MainDB.GetDataFromStoredProcAsync<SecurityTokenData>("SecurityToken_Select", parameters, FillDataFromReader);
        return data.FirstOrDefault();
    }

    public async Task MarkTokenAsExpired(string token)
    {
        var parameters = new Dictionary<string, object>
        {
            {"Token", token}
        };

        await MainDB.ExecProcAsync("SecurityToken_MarkAsExpired", parameters);
    }

    private void FillDataFromReader(IDBHelperReader rdr, SecurityTokenData data)
    {
        data.Token = rdr["Token"] as string;
        data.TokenExpiration = (DateTime)rdr["TokenExpiration"];
        data.RefreshToken = rdr["RefreshToken"] as string;
        data.RefreshTokenExpiration = (DateTime)rdr["RefreshTokenExpiration"];
        data.UserId = (int)rdr["UserId"];
        data.SecurityRole = rdr["SecurityRole"] as string;
        data.EmailAddress = rdr["EmailAddress"] as string;
        data.FromRefreshToken = rdr["FromRefreshToken"] as string; // can be null 
    }
}
