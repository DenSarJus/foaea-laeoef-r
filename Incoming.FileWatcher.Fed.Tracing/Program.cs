using DBHelper;
using FileBroker.Business.Helpers;
using FileBroker.Common;
using FOAEA3.Resources.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Incoming.FileWatcher.Fed.LicenceDenial;

class Program
{
    static async Task Main(string[] args)
    {
        var consoleOut = Console.Out;
        using (var textOut = new StreamWriter(new FileStream("log.txt", FileMode.Append)))
        {
            args ??= Array.Empty<string>();

            var config = new FileBrokerConfigurationHelper(args);

            if (config.LogConsoleOutputToFile)
                Console.SetOut(textOut);

            Console.WriteLine($"*** Started {AppDomain.CurrentDomain.FriendlyName}.exe: {DateTime.Now}");
            ColourConsole.WriteEmbeddedColorLine("Starting Incoming Federal Tracing File Monitor");

            string aspnetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            ColourConsole.WriteEmbeddedColorLine($"Using Environment: [yellow]{aspnetCoreEnvironment}[/yellow]");
            ColourConsole.WriteEmbeddedColorLine($"FTProot: [yellow]{config.FTProot}[/yellow]");
            ColourConsole.WriteEmbeddedColorLine($"FTPbackupRoot: [yellow]{config.FTPbackupRoot}[/yellow]");
            ColourConsole.WriteEmbeddedColorLine($"Audit Root Path: [yellow]{config.AuditConfig.AuditRootPath}[/yellow]");

            var fileBrokerDB = new DBToolsAsync(config.FileBrokerConnection);
            var db = DataHelper.SetupFileBrokerRepositories(fileBrokerDB);

            var foaeaApis = FoaeaApiHelper.SetupFoaeaAPIs(config.ApiRootData);

            var federalFileManager = new IncomingFederalTracingFile(db, foaeaApis, config);

            var traceInFileTableData = await db.FileTable.GetFileTableDataForCategory("TRCIN");
            var folders = traceInFileTableData.Select(m => m.Path).Distinct().ToList();

            var processedFiles = new List<string>();
            bool finished = false;
            while (!finished)
            {
                var allNewFiles = new List<string>();
                foreach (var folder in folders)
                    await federalFileManager.GetNextExpectedIncomingFilesFoundInFolder(folder, allNewFiles);

                if (allNewFiles.Count > 0)
                {
                    ColourConsole.WriteEmbeddedColorLine($"Found [green]{allNewFiles.Count}[/green] file(s)");

                    foreach (var thisFile in allNewFiles)
                    {
                        if (processedFiles.Contains(thisFile))
                        {
                            finished = true;
                            break;
                        }

                        processedFiles.Add(thisFile);

                        ColourConsole.WriteEmbeddedColorLine($"Processing [green]{thisFile}[/green]...");

                        await federalFileManager.ProcessWaitingFile(thisFile);

                        if (federalFileManager.Errors.Any())
                        {
                            finished = true;
                            foreach (var error in federalFileManager.Errors)
                                await db.ErrorTrackingTable.MessageBrokerError("TRCIN", thisFile, new Exception(error), displayExceptionError: true);
                        }

                        federalFileManager.Errors.Clear();
                        FoaeaApiHelper.ClearErrors(foaeaApis);
                    }
                }
                else
                {
                    finished = true;
                    ColourConsole.WriteEmbeddedColorLine("[yellow]No new files found.[/yellow]");
                }
            }

            Console.WriteLine($"*** Ended: {DateTime.Now}\n");
        }
        Console.SetOut(consoleOut);
    }

}
