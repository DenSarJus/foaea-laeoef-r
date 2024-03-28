using DBHelper;
using FileBroker.Business;
using FileBroker.Common;
using FileBroker.Model.Interfaces;
using FOAEA3.Model;
using FOAEA3.Resources.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Outgoing.FileCreator.Fed.LicenceDenial;

public static class OutgoingFileCreatorFedLicenceDenial
{
    public static async Task Run(string[] args = null)
    {
        args ??= Array.Empty<string>();

        ColourConsole.WriteEmbeddedColorLine("Starting Federal Outgoing Licence Denial Files Creator");

        var config = new FileBrokerConfigurationHelper(args);

        var fileBrokerDB = new DBToolsAsync(config.FileBrokerConnection);

        await CreateOutgoingFederalLicenceDenialFiles(fileBrokerDB, config.ApiRootData, config);

        ColourConsole.Write("Completed.");
    }

    private static async Task CreateOutgoingFederalLicenceDenialFiles(DBToolsAsync fileBrokerDB, ApiConfig apiRootData,
                                                                          IFileBrokerConfigurationHelper config)
    {
        var foaeaApis = FoaeaApiHelper.SetupFoaeaAPIs(apiRootData);
        var db = DataHelper.SetupFileBrokerRepositories(fileBrokerDB);

        var federalFileManager = new OutgoingFederalLicenceDenialManager(foaeaApis, db, config);

        var federalLicenceDenialOutgoingSources = (await db.FileTable.GetFileTableDataForCategory("LICOUT"))
                                                              .Where(s => s.Active == true);

        foreach (var federalLicenceDenialOutgoingSource in federalLicenceDenialOutgoingSources)
        {
            var errors = new List<string>();
            (string filePath, errors) = await federalFileManager.CreateOutputFile(federalLicenceDenialOutgoingSource.Name);
            if (errors.Count == 0)
                ColourConsole.WriteEmbeddedColorLine($"Successfully created [cyan]{filePath}[/cyan]");
            else
                foreach (var error in errors)
                {
                    ColourConsole.WriteEmbeddedColorLine($"Error creating [cyan]{federalLicenceDenialOutgoingSource.Name}[/cyan]: [red]{error}[/red]");
                    await db.ErrorTrackingTable.MessageBrokerError("LICOUT", federalLicenceDenialOutgoingSource.Name,
                                                                               new Exception(error), displayExceptionError: true);
                }
        }

    }
}
