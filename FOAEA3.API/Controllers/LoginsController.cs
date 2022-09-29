﻿using FOAEA3.API.Security;
using FOAEA3.Business.Security;
using FOAEA3.Common.Helpers;
using FOAEA3.Data.DB;
using FOAEA3.Model;
using FOAEA3.Model.Interfaces;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FOAEA3.API.Controllers
{
    [ApiController]
    [Route("api/v1/[controller]")]
    [Authorize()]
    public class LoginsController : ControllerBase
    {
        [HttpGet("Version")]
        public ActionResult<string> GetVersion() => Ok("Logins API Version 1.0");

        [HttpGet("DB")]
        [Authorize(Roles = "Admin")]
        public ActionResult<string> GetDatabase([FromServices] IRepositories repositories) => Ok(repositories.MainDB.ConnectionString);

        [AllowAnonymous]
        [HttpPost("TestLogin")]
        public async Task<ActionResult> TestLoginAction([FromBody] LoginData2 loginData,
                                                        [FromServices] IRepositories db)
        {
            // WARNING: not for production use!
            var principal = await TestLogin.AutoLogin(loginData.UserName, loginData.Password, loginData.Submitter, db);
            if (principal is not null && principal.Identity is not null)
            {
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);// LoggingHelper.COOKIE_ID, principal);
                return Ok("Successfully logged in.");
            }
            else
            {
                return BadRequest("Login failed.");
            }
        }

        [HttpPost("TestVerify")]
        public ActionResult TestVerify()
        {
            // WARNING: not for production use!
            var user = User.Identity;
            if (user is not null && user.IsAuthenticated)
            {
                //var claims = User.Claims;
                //var userName = string.Empty;
                //var userRole = string.Empty;
                //var submitter = string.Empty;
                //foreach (var claim in claims)
                //{
                //    switch (claim.Type)
                //    {
                //        case ClaimTypes.Name:
                //            userName = claim.Value;
                //            break;
                //        case ClaimTypes.Role:
                //            userRole = claim.Value;
                //            break;
                //        case "Submitter":
                //            submitter = claim.Value;
                //            break;
                //    }
                //}
                // return Ok($"Logged in user: {userName} [{submitter} ({userRole})]");

                return Ok($"Logged in user: " + user.Name);
            }
            else
            {
                return Ok("No user logged in. Please login.");
            }
        }

        [HttpPost("TestLogout")]
        public async Task<ActionResult> TestLogout()
        {
            // WARNING: not for production use!
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            return Ok();
        }

        [HttpGet("CheckPreviousPasswords")]
        public async Task<ActionResult<string>> CheckPreviousPasswordsAsync([FromQuery] string subjectName, [FromQuery] string encryptedNewPassword, [FromServices] IRepositories repositories)
        {
            var loginManager = new LoginManager(repositories);
            var result = await loginManager.CheckPreviousPasswordsAsync(subjectName, encryptedNewPassword);

            return Ok(result ? "true" : "false");
        }

        [HttpPut("SetPassword")]
        public async Task<ActionResult<string>> SetNewPassword([FromQuery] string encryptedNewPassword, [FromServices] IRepositories repositories)
        {
            var subject = await APIBrokerHelper.GetDataFromRequestBodyAsync<SubjectData>(Request);

            var loginManager = new LoginManager(repositories);
            var result = await loginManager.CheckPreviousPasswordsAsync(subject.SubjectName, encryptedNewPassword);

            return Ok(result ? "true" : "false");
        }

        [HttpPost("SendEmail")]
        [Authorize(Roles = "System")]
        public async Task<ActionResult<string>> SendEmail([FromServices] IRepositories repositories)
        {
            var emailData = await APIBrokerHelper.GetDataFromRequestBodyAsync<EmailData>(Request);

            var loginManager = new LoginManager(repositories);
            await loginManager.SendEmailAsync(emailData.Subject, emailData.Recipient, emailData.Body, emailData.IsHTML);

            return Ok(emailData);

        }

        [HttpGet("PostConfirmationCode")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<string>> PostConfirmationCode([FromQuery] int subjectId, [FromQuery] string confirmationCode, [FromServices] IRepositories repositories)
        {
            var dbLogin = new DBLogin(repositories.MainDB);

            await dbLogin.PostConfirmationCodeAsync(subjectId, confirmationCode);

            return Ok(string.Empty);
        }

        [HttpGet("GetEmailByConfirmationCode")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<string>> GetEmailByConfirmationCode([FromQuery] string confirmationCode, [FromServices] IRepositories repositories)
        {
            var dbLogin = new DBLogin(repositories.MainDB);

            return Ok(await dbLogin.GetEmailByConfirmationCodeAsync(confirmationCode));
        }

        [HttpGet("GetSubjectByConfirmationCode")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<SubjectData>> GetSubjectByConfirmationCode([FromQuery] string confirmationCode, [FromServices] IRepositories repositories)
        {
            var dbSubject = new DBSubject(repositories.MainDB);

            return Ok(await dbSubject.GetSubjectByConfirmationCodeAsync(confirmationCode));
        }

        [HttpPut("PostPassword")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<PasswordData>> PostPassword([FromServices] IRepositories repositories)
        {
            var passwordData = await APIBrokerHelper.GetDataFromRequestBodyAsync<PasswordData>(Request);

            var dbLogin = new DBLogin(repositories.MainDB);
            await dbLogin.PostPasswordAsync(passwordData.ConfirmationCode, passwordData.Password, passwordData.Salt, passwordData.Initial);

            return Ok(passwordData);
        }



    }
}
