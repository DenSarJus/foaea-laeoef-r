using FileBroker.Common.Helpers;
using FileBroker.Model;
using FOAEA3.Model.Structs;

namespace FileBroker.Business.Helpers
{
    public class IncomingProvincialFile
    {
        private RepositoryList DB { get; }
        private APIBrokerList FoaeaApis { get; }
        private IFileBrokerConfigurationHelper Config { get; }

        public IncomingProvincialFile(RepositoryList db,
                                      APIBrokerList foaeaApis,
                                      IFileBrokerConfigurationHelper config)
        {
            DB = db;
            FoaeaApis = foaeaApis;
            Config = config;
        }

        public async Task<List<string>> ProcessWaitingFile(string fullPath)
        {
            var errors = new List<string>();

            if (Path.GetExtension(fullPath)?.ToUpper() == ".XML")
                errors = await ProcessIncomingXmlFile(fullPath, errors);

            //else if (Path.GetExtension(fullPath)?.ToUpper() == ".ZIP")
            //    await ProcessIncomingESDfile(fullPath, errors);

            else
                errors.Add($"Unknown file type: {Path.GetExtension(fullPath)?.ToUpper()} for file {fullPath}");

            return errors;
        }

        public async Task<List<string>> ProcessIncomingXmlFile(string fullPath, List<string> errors)
        {
            string fileNameNoXmlExtension = Path.GetFileNameWithoutExtension(fullPath);
            string fileNameNoCycle = FileHelper.TrimCycleAndXmlExtension(fileNameNoXmlExtension);

            if (await FileHelper.CheckForDuplicateFile(fullPath, DB.MailService, Config))
            {
                errors.Add("Duplicate file found!");
                return errors;
            }

            string xmlData = File.ReadAllText(fullPath, System.Text.Encoding.UTF8);
            string jsonText = FileHelper.ConvertXmlToJson(xmlData, errors);

            if (errors.Any())
                return errors;

            char fileType = fileNameNoCycle.ToUpper().Last();
            switch (fileType)
            {
                case 'T':
                    errors = await ProcessIncomingTracing(jsonText, fileNameNoXmlExtension, errors);
                    break;

                case 'W':
                    errors = await ProcessIncomingSwearing(jsonText, fileNameNoXmlExtension, errors);
                    break;

                //case 'I':
                //    errors = await ProcessIncomingInterception(jsonText, fileNameNoXmlExtension, errors);
                //    break;

                case 'L':
                    errors = await ProcessIncomingLicencing(jsonText, fileNameNoXmlExtension, errors);
                    break;

                default:
                    errors.Add($"Unknown file type: {fileType}");
                    break;
            }

            if (!errors.Any())
            {
                string errorDoingBackup = await FileHelper.BackupFile(fullPath, DB, Config);

                if (!string.IsNullOrEmpty(errorDoingBackup))
                    await DB.ErrorTrackingTable.MessageBrokerError($"File Error: {fullPath}",
                                                                        "Error creating backup of outbound file: " + errorDoingBackup);
            }

            return errors;
        }

        private async Task<List<string>> ProcessIncomingTracing(string sourceTracingJsonData, string fileName, List<string> errors)
        {
            errors = JsonHelper.Validate<MEPTracingFileData>(sourceTracingJsonData, out List<UnknownTag> unknownTags);

            if (errors.Count != 0)
                return errors;

            var tracingManager = new IncomingProvincialTracingManager(DB, FoaeaApis, fileName, Config);

            var fileNameNoCycle = Path.GetFileNameWithoutExtension(fileName);
            var fileTableData = await DB.FileTable.GetFileTableDataForFileName(fileNameNoCycle);
            if (!fileTableData.IsLoading)
            {
                await DB.FileTable.SetIsFileLoadingValue(fileTableData.PrcId, true);

                var info = await tracingManager.ExtractAndProcessRequestsInFile(sourceTracingJsonData, unknownTags);

                await DB.FileTable.SetIsFileLoadingValue(fileTableData.PrcId, false);

                if ((info is not null) && (info.ContainsMessagesOfType(MessageType.Error)))
                    foreach (var error in info.GetMessagesForType(MessageType.Error))
                        errors.Add(error.Description);

            }
            else
                errors.Add("File was already loading?");

            return errors;
        }

        private async Task<List<string>> ProcessIncomingSwearing(string sourceSwearingJsonData, string fileName, List<string> errors)
        {
            errors = JsonHelper.Validate<MEPSwearingFileData>(sourceSwearingJsonData, out List<UnknownTag> unknownTags);

            if (errors.Any())
                return errors;

            var tracingManager = new IncomingProvincialSwearingManager(DB, FoaeaApis, fileName, Config);

            var fileNameNoCycle = Path.GetFileNameWithoutExtension(fileName);
            var fileTableData = await DB.FileTable.GetFileTableDataForFileName(fileNameNoCycle);
            if (!fileTableData.IsLoading)
            {
                await DB.FileTable.SetIsFileLoadingValue(fileTableData.PrcId, true);

                var info = await tracingManager.ExtractAndProcessRequestsInFile(sourceSwearingJsonData, unknownTags);

                await DB.FileTable.SetIsFileLoadingValue(fileTableData.PrcId, false);

                if ((info is not null) && (info.ContainsMessagesOfType(MessageType.Error)))
                    foreach (var error in info.GetMessagesForType(MessageType.Error))
                        errors.Add(error.Description);

            }
            else
                errors.Add("File was already loading?");

            return errors;
        }
        public async Task<List<string>> ProcessIncomingInterception(string sourceInterceptionJsonData, string fileName, List<string> errors)
        {
            errors = JsonHelper.Validate<MEPInterceptionFileData>(sourceInterceptionJsonData, out List<UnknownTag> unknownTags);

            if (errors.Any())
            {
                errors = JsonHelper.Validate<MEPInterceptionFileDataSingleSource>(sourceInterceptionJsonData, out unknownTags);

                if (errors.Any())
                    return errors;
            }

            var interceptionManager = new IncomingProvincialInterceptionManager(DB, FoaeaApis, fileName, Config);

            var fileNameNoCycle = Path.GetFileNameWithoutExtension(fileName);
            var fileTableData = await DB.FileTable.GetFileTableDataForFileName(fileNameNoCycle);
            if (!fileTableData.IsLoading)
            {
                var info = await interceptionManager.ExtractAndProcessRequestsInFile(sourceInterceptionJsonData, unknownTags,
                                                                                          includeInfoInMessages: true);
                if ((info is not null) && (info.ContainsMessagesOfType(MessageType.Error)))
                    foreach (var error in info.GetMessagesForType(MessageType.Error))
                        errors.Add(error.Description);
            }
            else
                errors.Add("File was already loading?");

            return errors;
        }

        private async Task<List<string>> ProcessIncomingLicencing(string sourceLicenceDenialJsonData, string fileName, List<string> errors)
        {
            errors = JsonHelper.Validate<MEPLicenceDenialFileData>(sourceLicenceDenialJsonData, out List<UnknownTag> unknownTags);
            if (errors.Any())
            {
                errors = JsonHelper.Validate<MEPLicenceDenialFileDataSingle>(sourceLicenceDenialJsonData, out unknownTags);

                if (errors.Count != 0)
                    return errors;
            }

            var licenceDenialManager = new IncomingProvincialLicenceDenialManager(DB, FoaeaApis, fileName, Config);

            var fileNameNoCycle = Path.GetFileNameWithoutExtension(fileName);
            var fileTableData = await DB.FileTable.GetFileTableDataForFileName(fileNameNoCycle);
            if (!fileTableData.IsLoading)
            {
                var info = await licenceDenialManager.ExtractAndProcessRequestsInFile(sourceLicenceDenialJsonData, unknownTags);

                if ((info is not null) && (info.ContainsMessagesOfType(MessageType.Error)))
                    foreach (var error in info.GetMessagesForType(MessageType.Error))
                        errors.Add(error.Description);
            }
            else
                errors.Add("File was already loading?");

            return errors;
        }

        private async Task ProcessIncomingESDfile(string fullPath, List<string> errors)
        {
            var electronicSummonsManager = new IncomingProvincialElectronicSummonsManager(DB, FoaeaApis, fullPath, Config);

            var info = await electronicSummonsManager.ExtractAndProcessRequestsInFile(fullPath);

            if ((info is not null) && (info.ContainsMessagesOfType(MessageType.Error)))
                foreach (var error in info.GetMessagesForType(MessageType.Error))
                    errors.Add(error.Description);
        }

        public async Task AddNewIncomingXMLfilesIfExpected(string rootPath, List<string> newFiles, string option)
        {
            var directory = new DirectoryInfo(rootPath);
            var allFiles = await Task.Run(() => { return directory.GetFiles("*.xml"); });
            var last31days = DateTime.Now.AddDays(-31);
            var files = allFiles.Where(f => f.LastWriteTime > last31days).OrderByDescending(f => f.LastWriteTime);

            foreach (var thisFile in files)
            {
                if (await IsNextExpectedIncomingCycle(thisFile, option))
                    newFiles.Add(thisFile.FullName);
            }
        }

        private async Task<bool> IsNextExpectedIncomingCycle(FileInfo thisFile, string option)
        {
            int cycle = FileHelper.ExtractCycleFromFilename(thisFile.Name);
            if (cycle != FileHelper.INVALID_CYCLE)
            {
                string baseFileName = FileHelper.TrimCycleAndXmlExtension(thisFile.Name);
                var fileTableData = await DB.FileTable.GetFileTableDataForFileName(baseFileName);

                if ((option == "TRACE_ONLY") && (
                    (fileTableData.Category == "TRCAPPIN") || (fileTableData.Category == "LICAFFDVTIN")))
                {
                    if ((cycle == fileTableData.Cycle) && (fileTableData.Type.ToLower() == "in") &&
                        (fileTableData.Active.HasValue) && (fileTableData.Active.Value))
                        return true;
                    else
                        return false;
                }
            }
            return false;
        }

        public async Task AddNewESDfiles(string rootPath, List<string> newFiles)
        {
            var directory = new DirectoryInfo(rootPath);

            if (directory.Exists)
            {
                var allFiles = await Task.Run(() => { return directory.GetFiles("*.zip"); });
                var last31days = DateTime.Now.AddDays(-31);
                var files = allFiles.Where(f => f.LastWriteTime > last31days).OrderByDescending(f => f.LastWriteTime);

                if (files.Any())
                {
                    var foaeaAccess = new FoaeaSystemAccess(FoaeaApis, Config.FoaeaLogin);

                    await foaeaAccess.SystemLogin();
                    try
                    {
                        foreach (var fileInfo in files)
                        {
                            var fileName = Path.GetFileName(fileInfo.FullName);

                            bool wasAlreadyLoaded = await FoaeaApis.InterceptionApplications.ESD_CheckIfAlreadyLoaded(fileName);

                            if (!wasAlreadyLoaded)
                                newFiles.Add(fileInfo.FullName);
                        }
                    }
                    finally
                    {
                        await foaeaAccess.SystemLogout();
                    }
                }
            }
        }

        public async Task<List<string>> GetNextExpectedIncomingFilesFoundInFolder(List<string> searchPaths, string option)
        {
            var allNewFiles = new List<string>();

            foreach (string searchPath in searchPaths)
            {
                if (!searchPath.Contains("ESD"))
                    await AddNewIncomingXMLfilesIfExpected(searchPath, allNewFiles, option);
                //else
                //    await AddNewESDfiles(searchPath, allNewFiles);
            }

            return allNewFiles.OrderBy(q => q[^12]).ToList(); // sort by last character so hopefully, swearing files will come last
        }

    }
}
