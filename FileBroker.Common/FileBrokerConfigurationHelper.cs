﻿using FileBroker.Model;
using FOAEA3.Model;
using FOAEA3.Resources.Helpers;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.IO;

namespace FileBroker.Common
{
    public class FileBrokerConfigurationHelper
    {
        public string FileBrokerConnection { get; }
        public string TermsAcceptedTextEnglish { get; }
        public string TermsAcceptedTextFrench { get; }
        public string EmailRecipient { get; }
        public string FTProot { get; }

        public ApiConfig ApiRootData { get; }
        public FoaeaLoginData FoaeaLogin { get; }
        public FileBrokerLoginData FileBrokerLogin { get; }
        public ProvincialAuditFileConfig AuditConfig { get; }
        public TokenConfig Tokens { get; }

        public List<string> ProductionServers { get; }

        public FileBrokerConfigurationHelper(string[] args = null)
        {
            string aspnetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("FileBrokerConfiguration.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"FileBrokerConfiguration.{aspnetCoreEnvironment}.json", optional: true, reloadOnChange: true);

            if (args is not null)
                builder = builder.AddCommandLine(args);

            IConfiguration configuration = builder.Build();

            FileBrokerConnection = configuration.GetConnectionString("FileBrokerDB").ReplaceVariablesWithEnvironmentValues();

            FoaeaLogin = new FoaeaLoginData
            {
                UserName = configuration["FOAEA:userName"].ReplaceVariablesWithEnvironmentValues(),
                Password = configuration["FOAEA:userPassword"].ReplaceVariablesWithEnvironmentValues(),
                Submitter = configuration["FOAEA:submitter"].ReplaceVariablesWithEnvironmentValues()
            };

            FileBrokerLogin = new FileBrokerLoginData
            {
                UserName = configuration["FILE_BROKER:userName"].ReplaceVariablesWithEnvironmentValues(),
                Password = configuration["FILE_BROKER:userPassword"].ReplaceVariablesWithEnvironmentValues()
            };

            TermsAcceptedTextEnglish = configuration["Declaration:TermsAccepted:English"];
            TermsAcceptedTextFrench = configuration["Declaration:TermsAccepted:French"];

            EmailRecipient = configuration["emailRecipients"];
            FTProot = configuration["FTProot"];

            ApiRootData = configuration.GetSection("APIroot").Get<ApiConfig>();
            AuditConfig = configuration.GetSection("AuditConfig").Get<ProvincialAuditFileConfig>();
            Tokens = configuration.GetSection("Tokens").Get<TokenConfig>();
            ProductionServers = configuration.GetSection("ProductionServers").Get<List<string>>();
        }
    }
}
