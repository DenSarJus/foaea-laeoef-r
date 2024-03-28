using DBHelper;
using FileBroker.Business.Helpers;
using FileBroker.Common;
using FOAEA3.Resources.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Incoming.FileWatcher.Fed.LicenceDenial;

class Program
{
    // private static IncomingFederalLicenceDenialFile FederalFileManager;

    static async Task Main(string[] args)
    {
        ColourConsole.WriteEmbeddedColorLine("Starting Federal Licence Denial File Monitor");

        var config = new FileBrokerConfigurationHelper(args);

        var fileBrokerDB = new DBToolsAsync(config.FileBrokerConnection);
        var db = DataHelper.SetupFileBrokerRepositories(fileBrokerDB);

        var foaeaApis = FoaeaApiHelper.SetupFoaeaAPIs(config.ApiRootData);

        var federalFileManager = new IncomingFederalLicenceDenialFile(db, foaeaApis, config);

        var allNewFiles = new List<string>();
        await federalFileManager.AddNewFiles(config.FTProot + @"\Tc3sls", allNewFiles); // Transport Canada Licence Denial
        await federalFileManager.AddNewFiles(config.FTProot + @"\Pa3sls", allNewFiles); // Passport Canada Licence Denial

        if (allNewFiles.Count > 0)
        {
            ColourConsole.WriteEmbeddedColorLine($"Found [green]{allNewFiles.Count}[/green] file(s)");
            foreach (var newFile in allNewFiles)
            {
                var errors = new List<string>();
                ColourConsole.WriteEmbeddedColorLine($"Processing [green]{newFile}[/green]...");
                await federalFileManager.ProcessNewFile(newFile);
                if (federalFileManager.Errors.Any())
                    foreach (var error in errors)
                        await db.ErrorTrackingTable.MessageBrokerError("LICIN", newFile, new Exception(error), displayExceptionError: true);
            }
        }
        else
            ColourConsole.WriteEmbeddedColorLine("[yellow]No new files found.[/yellow]");
    }

}
