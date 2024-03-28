using DBHelper;
using FileBroker.Business;
using FileBroker.Common;
using FileBroker.Data;
using FileBroker.Model.Interfaces;
using FOAEA3.Resources.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Outgoing.FileCreator.MEP;

public static class OutgoingFileCreatorMEP
{
    public static async Task Run(string[] args = null)
    {
        args ??= [];

        ColourConsole.WriteEmbeddedColorLine("Starting MEP Outgoing File Creator");

        var config = new FileBrokerConfigurationHelper(args);
        string aspnetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        ColourConsole.WriteEmbeddedColorLine($"Using Environment: [yellow]{aspnetCoreEnvironment}[/yellow]");
        ColourConsole.WriteEmbeddedColorLine($"FTProot: [yellow]{config.FTProot}[/yellow]");
        ColourConsole.WriteEmbeddedColorLine($"FTPbackupRoot: [yellow]{config.FTPbackupRoot}[/yellow]");
        ColourConsole.WriteEmbeddedColorLine($"Audit Root Path: [yellow]{config.AuditConfig.AuditRootPath}[/yellow]");

        var fileBrokerDB = new DBToolsAsync(config.FileBrokerConnection);

        bool generateTracingFiles = true;
        bool generateLicencingFiles = true; 
        bool generateStatsFiles = false; // TODO: fix when support for stats file is added

        if (args.Length > 0)
        {
            string option = args[0].ToUpper();
            switch (option)
            {
                case "TRACE_ONLY":
                    generateLicencingFiles = false;
                    generateStatsFiles = false;
                    break;
                case "LICENCE_ONLY":
                    generateTracingFiles = false;
                    generateStatsFiles = false;
                    break;
                case "STATS_ONLY":
                    generateLicencingFiles = false;
                    generateTracingFiles = false;
                    break;
                default:
                    break;
            }
        }

        var foaeaApis = FoaeaApiHelper.SetupFoaeaAPIs(config.ApiRootData);

        var db = DataHelper.SetupFileBrokerRepositories(fileBrokerDB);

        if (generateTracingFiles)
            await CreateOutgoingProvincialFiles(db, "TRCAPPOUT", new OutgoingProvincialTracingManager(foaeaApis, db, config));

        if (generateLicencingFiles)
            await CreateOutgoingProvincialFiles(db, "LICAPPOUT", new OutgoingProvincialLicenceDenialManager(foaeaApis, db, config));

        //if (generateStatsFiles)
        //    await CreateOutgoingProvincialFiles(db, "STATAPPOUT", new OutgoingProvincialStatusManager(foaeaApis, db, config));
    }

    private static async Task CreateOutgoingProvincialFiles(RepositoryList repositories, string category,
                                                           IOutgoingFileManager outgoingProvincialFileManager)
    {
        var provincialOutgoingSources = (await repositories.FileTable.GetFileTableDataForCategory(category))
                                                              .Where(s => s.Active == true);

        foreach (var provincialOutgoingSource in provincialOutgoingSources)
        {
            var errors = new List<string>();
            (string filePath, errors) = await outgoingProvincialFileManager.CreateOutputFile(provincialOutgoingSource.Name);
            if (errors.Count == 0)
                ColourConsole.WriteEmbeddedColorLine($"Successfully created [cyan]{filePath}[/cyan]");
            else
                foreach (var error in errors)
                {
                    ColourConsole.WriteEmbeddedColorLine($"Error creating [cyan]{provincialOutgoingSource.Name}[/cyan]: [red]{error}[/red]");
                    if (error != DataHelper.NO_DATA_FOUND)
                        await repositories.ErrorTrackingTable.MessageBrokerError(category, provincialOutgoingSource.Name,
                                                                    new Exception(error), displayExceptionError: true);
                }
        }
    }
}
