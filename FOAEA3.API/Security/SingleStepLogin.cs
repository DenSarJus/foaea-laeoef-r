using FOAEA3.Business.Security;
using FOAEA3.Model.Constants;
using FOAEA3.Model.Interfaces.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FOAEA3.API.Security
{
    public class SingleStepLogin
    {
        public static async Task<ClaimsPrincipal> FoaeaLogin(string user, string password, string submitter, IRepositories db)
        {
            var subject = await db.SubjectTable.GetSubject(user);

            if (subject is null || subject.IsAccountLocked is true)
                return new ClaimsPrincipal();

            string encryptedPassword = subject.Password;
            string salt = subject.PasswordSalt;

            if (!PasswordHelper.IsValidPassword(password, salt, encryptedPassword))
                return new ClaimsPrincipal();

            string userName = subject.SubjectName;

            var submitterData = (await db.SubmitterTable.GetSubmitter(submitter)).FirstOrDefault();

            // verify that submitter is valid for subject
            var roles = await db.SubjectRoleTable.GetSubjectRoles(userName);
            var availableSubmitters = (from role in roles select role.RoleName.ToLower()).ToList();

            if (submitterData is null || submitterData.ActvSt_Cd != "A" || !availableSubmitters.Contains(submitterData.Subm_SubmCd.ToLower()))
                return new ClaimsPrincipal();

            string userRole = submitterData.Subm_Class.ToUpper().Trim();

            if (string.Equals(userName, "system_support", StringComparison.InvariantCultureIgnoreCase))
                userRole += ", " + Roles.Admin;

            if (submitterData.Subm_Trcn_AccsPrvCd)
                userRole += ", " + Duties.Tracing;
            if (submitterData.Subm_Intrc_AccsPrvCd)
                userRole += ", " + Duties.Interception;
            if (submitterData.Subm_Lic_AccsPrvCd)
                userRole += ", " + Duties.LicenceDenial;
            if (submitterData.Subm_Fin_Ind)
                userRole += ", " + Duties.Finance;
            if (submitterData.Subm_LglSgnAuth_Ind)
                userRole += ", " + Duties.Swear_Affidavit;
            if (submitterData.Subm_Audit_File_Ind)
                userRole += ", " + Duties.ReceiveAuditFiles;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.Name, userName),
                new Claim("Submitter", submitter),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };
            SetupRoleClaims(claims, userRole);

            var identity = new ClaimsIdentity(claims, JwtBearerDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            return principal;
        }

        private static void SetupRoleClaims(List<Claim> claims, string securityRole)
        {
            string[] roles = securityRole.Split(",");
            foreach (string role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role.Trim()));
        }
    }
}
