using DBHelper;
using FileBroker.Business.Helpers;
using FileBroker.Common;
using FileBroker.Data.DB;
using FileBroker.Model;
using FileBroker.Model.Interfaces;
using FOAEA3.Resources.Helpers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using static FOAEA3.Resources.Helpers.ColourConsole;

namespace Incoming.FileWatcher.MEP;

internal class Program
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
            string aspnetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            WriteEmbeddedColorLine($"Using Environment: [yellow]{aspnetCoreEnvironment}[/yellow]");
            WriteEmbeddedColorLine($"FTProot: [yellow]{config.FTProot}[/yellow]");
            WriteEmbeddedColorLine($"FTPbackupRoot: [yellow]{config.FTPbackupRoot}[/yellow]");
            WriteEmbeddedColorLine($"Audit Root Path: [yellow]{config.AuditConfig.AuditRootPath}[/yellow]");

            var fileBrokerDB = new DBToolsAsync(config.FileBrokerConnection);
            var db = DataHelper.SetupFileBrokerRepositories(fileBrokerDB);

            await db.RequestLogTable.DeleteAll();

            string provinceCode = args.Any() ? args.First()?.ToUpper() : "ALL";
            string option = args.Length > 1 ? args[1].ToUpper() : string.Empty;
            var filesToProcess = await GetFileTableDataForIncomingMEPfiles(args, new DBFileTable(fileBrokerDB), option);

            if (!filesToProcess.Any())
            {
                await GenerateError(db.ErrorTrackingTable, "No items found in FileTable?");
                return;
            }

            var provinces = GetProvinceListForIncomingMEPfiles(filesToProcess);

            if ((string.IsNullOrEmpty(provinceCode) || !provinces.Contains(provinceCode)) && (provinceCode != "ALL"))
            {
                await GenerateError(db.ErrorTrackingTable, $"Invalid province argument on command line: [{provinceCode}]\nMust be one of: " +
                                                           string.Join(", ", provinces + " or ALL"));
                return;
            }

            DateTime start = DateTime.Now;
            WriteEmbeddedColorLine($"Starting [cyan]{provinceCode}[/cyan] MEP File Monitor");
            WriteEmbeddedColorLine($"Starting time [orange]{start}[/orange]");

            var foaeaApis = FoaeaApiHelper.SetupFoaeaAPIs(config.ApiRootData);

            int totalFilesFound = 0;
            foreach (var itemProvince in provinces)
            {
                string provCode = itemProvince.ToUpper();
                var searchFolders = GetFoldersToProcess(filesToProcess, provCode);

                var fileTable = new DBFileTable(fileBrokerDB);
                var provincialFileManager = new IncomingProvincialFile(db, foaeaApis, config);

                bool foundZero = true;

                var processedFiles = new List<string>();
                bool AlreadyProcessed(string f) => processedFiles.Contains(f);

                bool finishedForProvince = false;
                while (!finishedForProvince)
                {
                    var allNewExpectedFiles = await provincialFileManager.GetNextExpectedIncomingFilesFoundInFolder(searchFolders, option);

                    if (allNewExpectedFiles.Any())
                    {
                        foundZero = false;
                        string moreThanOne = allNewExpectedFiles.Count > 1 ? "s" : "";

                        if (!AlreadyProcessed(allNewExpectedFiles.First()))
                        {
                            WriteEmbeddedColorLine($"Found [green]{allNewExpectedFiles.Count}[/green] file{moreThanOne} in [green]{itemProvince}[/green]");
                            totalFilesFound += allNewExpectedFiles.Count;
                        }

                        foreach (var thisfile in allNewExpectedFiles)
                        {
                            if (AlreadyProcessed(thisfile))
                            {
                                finishedForProvince = true;
                                break;
                            }

                            processedFiles.Add(thisfile);

                            WriteEmbeddedColorLine($"Processing [green]{thisfile}[/green]...");

                            var errors = await provincialFileManager.ProcessWaitingFile(thisfile);

                            if (errors.Any())
                            {
                                foreach (var error in errors)
                                {
                                    await db.ErrorTrackingTable.MessageBrokerError($"{provinceCode} incoming file processing error", thisfile, new Exception(error), displayExceptionError: true);
                                    WriteEmbeddedColorLine($"[red]Error[/red]: [yellow]{error}[/yellow]");
                                }
                                finishedForProvince = true;
                            }
                        }
                    }
                    else
                    {
                        finishedForProvince = true;
                        if (foundZero)
                            WriteEmbeddedColorLine($"Found [green]0[/green] file in [green]{itemProvince}[/green]");
                    }
                }
            }

            WriteEmbeddedColorLine($"Completed. [yellow]{totalFilesFound}[/yellow] processed.");

            DateTime end = DateTime.Now;
            var duration = end - start;

            WriteEmbeddedColorLine($"Completion time [orange]{end}[/orange] (duration: [yellow]{duration.Minutes}[/yellow] minutes)");
            Console.WriteLine($"*** Ended: {DateTime.Now}\n");
        }
        Console.SetOut(consoleOut);
    }

    private static async Task GenerateError(IErrorTrackingRepository errorTrackingDB, string error)
    {
        WriteEmbeddedColorLine(error);
        await errorTrackingDB.MessageBrokerError($"Incoming MEP File Processing", "Error starting MEP File Monitor",
                                                      new Exception(error), displayExceptionError: true);
    }

    private static List<string> GetFoldersToProcess(List<FileTableData> filesToProcess, string provinceCode)
    {
        var searchPaths = new List<string>();

        var thisProvinceFilesData = filesToProcess.Where(f => f.Name[..2].ToUpper() == provinceCode);
        string interceptionPath = string.Empty;
        foreach (var provinceFileData in thisProvinceFilesData)
        {
            string category = provinceFileData.Category.ToUpper().Trim();

            string thisPath = provinceFileData.Path.ToUpper();
            if ((!searchPaths.Contains(thisPath)) && (category != "ESD"))
                searchPaths.Add(thisPath);
        }

        if (!string.IsNullOrEmpty(interceptionPath))
            searchPaths.Add(interceptionPath.AppendToPath("ESD"));

        return searchPaths;
    }

    private static List<string> GetProvinceListForIncomingMEPfiles(List<FileTableData> fileTableData)
    {
        return fileTableData.Where(f => f.Active.HasValue && f.Active.Value)
                            .GroupBy(f => f.Name[..2].ToUpper())
                            .Select(group => group.First().Name[..2].ToUpper())
                            .ToList();
    }

    private static async Task<List<FileTableData>> GetFileTableDataForIncomingMEPfiles(string[] args, 
                                                                                       DBFileTable fileTable, string option)
    {
        var fileTableData = new List<FileTableData>();

        bool loadAllCategories = true;
        if (args.Length > 1)
        {
            switch (option)
            {
                case "TRACE_ONLY":
                    fileTableData.AddRange(await fileTable.GetFileTableDataForCategory("TRCAPPIN"));
                    fileTableData.AddRange(await fileTable.GetFileTableDataForCategory("LICAFFDVTIN"));
                    loadAllCategories = false;
                    break;
                case "INTERCEPTION_ONLY":
                    fileTableData.AddRange(await fileTable.GetFileTableDataForCategory("INTAPPIN"));
                    fileTableData.AddRange(await fileTable.GetFileTableDataForCategory("ESD"));
                    loadAllCategories = false;
                    break;
                case "LICENCE_ONLY":
                    fileTableData.AddRange(await fileTable.GetFileTableDataForCategory("LICAPPIN"));
                    loadAllCategories = false;
                    break;
                default:
                    break;
            }
        }
        if (loadAllCategories)
        {
            fileTableData.AddRange(await fileTable.GetFileTableDataForCategory("TRCAPPIN"));
            //fileTableData.AddRange(await fileTable.GetFileTableDataForCategory("INTAPPIN"));
            //fileTableData.AddRange(await fileTable.GetFileTableDataForCategory("ESD"));
            fileTableData.AddRange(await fileTable.GetFileTableDataForCategory("LICAPPIN"));
        }

        fileTableData = fileTableData.Where(f => f.Active.HasValue && f.Active.Value).ToList();

        return fileTableData;
    }
}