using DBHelper;
using FileBroker.Business;
using FileBroker.Common;
using FOAEA3.Resources.Helpers;

var consoleOut = Console.Out;
using (var textOut = new StreamWriter(new FileStream("log.txt", FileMode.Append)))
{
    args ??= Array.Empty<string>();

    var config = new FileBrokerConfigurationHelper(args);

    if (config.LogConsoleOutputToFile)
        Console.SetOut(textOut);

    Console.WriteLine($"*** Started {AppDomain.CurrentDomain.FriendlyName}.exe: {DateTime.Now}");
    string aspnetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

    ColourConsole.WriteEmbeddedColorLine($"Using Environment: [yellow]{aspnetCoreEnvironment}[/yellow]");
    ColourConsole.WriteEmbeddedColorLine($"FTProot: [yellow]{config.FTProot}[/yellow]");
    ColourConsole.WriteEmbeddedColorLine($"FTPbackupRoot: [yellow]{config.FTPbackupRoot}[/yellow]");
    ColourConsole.WriteEmbeddedColorLine($"Audit Root Path: [yellow]{config.AuditConfig.AuditRootPath}[/yellow]");

    var fileBrokerDB = new DBToolsAsync(config.FileBrokerConnection);
    var db = DataHelper.SetupFileBrokerRepositories(fileBrokerDB);

    DateTime start = DateTime.Now;
    ColourConsole.WriteEmbeddedColorLine($"Starting time [orange]{start}[/orange]");

    var apiBrokerManager = new ApiBrokerManager(db, config);
    apiBrokerManager.Run();

    DateTime end = DateTime.Now;
    var duration = end - start;

    ColourConsole.WriteEmbeddedColorLine($"Completion time [orange]{end}[/orange] (duration: [yellow]{duration.Minutes}[/yellow] minutes)");
    Console.WriteLine($"*** Ended: {DateTime.Now}\n");
}

Console.SetOut(consoleOut);

