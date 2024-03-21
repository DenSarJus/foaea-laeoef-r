using FileBroker.Common;
using FileBroker.Model;
using FileBroker.Model.Interfaces;
using FOAEA3.Business.Security;
using FOAEA3.Common.Helpers;
using FOAEA3.Model;
using FOAEA3.Resources.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using FileBrokerModel = FileBroker.Model;

namespace FileBroker.API.Account.Controllers
{
    [Route("api/v1/[controller]")]
    [ApiController]
    public class TokensController : ControllerBase
    {
        [AllowAnonymous]
        [HttpPost("")]
        public async Task<ActionResult> CreateToken([FromBody] FileBrokerLoginData loginData,
                                                    [FromServices] IUserRepository userTable,
                                                    [FromServices] ISecurityTokenRepository securityTokenTable)
        {
            var configHelper = new FileBrokerConfigurationHelper();
            var tokenConfig = configHelper.Tokens;
            if (tokenConfig == null)
                return StatusCode(500);

            var thisUser = await userTable.GetUserByName(loginData.UserName);

            if (!IsValidLogin(loginData, thisUser))
                return BadRequest();

            string apiKey = tokenConfig.Key.ReplaceVariablesWithEnvironmentValues();
            string issuer = tokenConfig.Issuer.ReplaceVariablesWithEnvironmentValues();
            string audience = tokenConfig.Audience.ReplaceVariablesWithEnvironmentValues();
            int expireMinutes = tokenConfig.ExpireMinutes;

            var token = CreateNewToken(apiKey, issuer, audience, expireMinutes, thisUser);

            var securityTokenData = new FileBrokerModel.SecurityTokenData
            {
                Token = token.Token,
                TokenExpiration = token.TokenExpiration,
                RefreshToken = token.RefreshToken,
                RefreshTokenExpiration = token.RefreshTokenExpiration,
                UserId = thisUser.UserId,
                SecurityRole = thisUser.SecurityRole,
                EmailAddress = thisUser.EmailAddress,
                // FromRefreshToken = 
            };
            await securityTokenTable.Create(securityTokenData);

            return Created("", token);
        }

        [AllowAnonymous]
        [HttpPost("Refresh")]
        public async Task<ActionResult> RefreshToken([FromBody] TokenRefreshData refreshData,
                                                     [FromServices] IUserRepository userTable,
                                                     [FromServices] ISecurityTokenRepository securityTokenTable)
        {
            var configHelper = new FileBrokerConfigurationHelper();
            var tokenConfig = configHelper.Tokens;
            if (tokenConfig == null)
                return StatusCode(500);

            string oldToken = Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(oldToken) || oldToken.Length < 8)
                return BadRequest();

            oldToken = oldToken[7..]; // get rid of the word Bearer that is at the beginning

            var lastSecurityToken = await securityTokenTable.GetTokenData(oldToken);

            if (lastSecurityToken is null ||
                !string.Equals(lastSecurityToken.RefreshToken, refreshData.RefreshToken) ||
                lastSecurityToken.RefreshTokenExpiration < DateTime.Now)
            {
                return BadRequest();
            }

            await securityTokenTable.MarkTokenAsExpired(oldToken);

            string apiKey = tokenConfig.Key.ReplaceVariablesWithEnvironmentValues();
            string issuer = tokenConfig.Issuer.ReplaceVariablesWithEnvironmentValues();
            string audience = tokenConfig.Audience.ReplaceVariablesWithEnvironmentValues();
            int expireMinutes = tokenConfig.ExpireMinutes;

            var thisUser = await userTable.GetUserById(lastSecurityToken.UserId);

            var token = CreateNewToken(apiKey, issuer, audience, expireMinutes, thisUser);

            var securityTokenData = new FileBrokerModel.SecurityTokenData
            {
                Token = token.Token,
                TokenExpiration = token.TokenExpiration,
                RefreshToken = token.RefreshToken,
                RefreshTokenExpiration = token.RefreshTokenExpiration,
                UserId = thisUser.UserId,
                SecurityRole = thisUser.SecurityRole,
                EmailAddress = thisUser.EmailAddress,
                FromRefreshToken = lastSecurityToken.RefreshToken
            };

            await securityTokenTable.Create(securityTokenData);

            return Created("", token);

        }

        [HttpPost("ExpireToken")]
        public async Task<ActionResult> MarkTokenAsExpired([FromServices] ISecurityTokenRepository securityTokenTable)
        {
            string oldToken = Request.Headers["Authorization"];
            if (string.IsNullOrEmpty(oldToken) || oldToken.Length < 8)
                return BadRequest();

            oldToken = oldToken[7..]; // get rid of the word Bearer that is at the beginning

            await securityTokenTable.MarkTokenAsExpired(oldToken);

            return Ok();
        }

        private static bool IsValidLogin(FileBrokerLoginData loginData, UserData thisUser)
        {
            if (thisUser == null)
                return false;

            if (!PasswordHelper.IsValidPassword(loginData.Password, thisUser.PasswordSalt, thisUser.EncryptedPassword))
                return false;

            return true;
        }

        private static TokenData CreateNewToken(string apiKey, string issuer, string audience, int expireMinutes, UserData userData)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userData.UserName),
                new Claim(JwtRegisteredClaimNames.Sub, userData.EmailAddress),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, userData.UserName)
            };
            SetupRoleClaims(claims, userData.SecurityRole);

            JwtSecurityToken token = SecurityTokenHelper.GenerateToken(issuer, audience, expireMinutes, apiKey, claims);
            string refreshToken = SecurityTokenHelper.GenerateRefreshToken();

            return new TokenData
            {
                Token = new JwtSecurityTokenHandler().WriteToken(token),
                RefreshToken = refreshToken,
                TokenExpiration = token.ValidTo
            };
        }

        private static void SetupRoleClaims(List<Claim> claims, string securityRole)
        {
            string[] roles = securityRole.Split(",");
            foreach (string role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
        }

    }
}
