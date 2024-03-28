using FileBroker.Data;
using FileBroker.Model;
using FileBroker.Model.Interfaces;
using FOAEA3.Resources.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace FileBroker.Common
{
    public static class FileHelper
    {
        public const int INVALID_CYCLE = -1;

        public static string TrimCycleAndXmlExtension(string fileName)
        {
            if (fileName.ToUpper().EndsWith(".XML"))
                fileName = Path.GetFileNameWithoutExtension(fileName);

            int lastPeriod = fileName.LastIndexOf('.');
            if (lastPeriod > 0)
                return fileName[..lastPeriod];
            else
                return fileName;
        }

        public static int ExtractCycleFromFilename(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return -1;

            if (fileName.ToUpper().EndsWith(".XML"))
                fileName = Path.GetFileNameWithoutExtension(fileName);

            int lastPeriod = fileName.LastIndexOf('.');
            if (lastPeriod > 0)
                return int.TryParse(fileName[(lastPeriod + 1)..], out int cycle) ? cycle : -1;
            else
                return INVALID_CYCLE;
        }

        public static string ExtractCycleAsStringFromFilename(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
                return string.Empty;

            if (fileName.ToUpper().EndsWith(".XML"))
                fileName = Path.GetFileNameWithoutExtension(fileName);

            int lastPeriod = fileName.LastIndexOf('.');
            if (lastPeriod > 0)
                return fileName[(lastPeriod + 1)..];
            else
                return string.Empty;
        }

        public static bool IsExpectedCycle(FileTableData fileTableData, string fileName, out int expectedCycle, 
                                                                                         out int actualCycle)
        {
            expectedCycle = fileTableData.Cycle;
            actualCycle = ExtractCycleFromFilename(fileName);

            return (actualCycle == expectedCycle);
        }

        public static string ConvertXmlToJson(string xmlData, List<string> errors)
        {
            try
            {
                xmlData = RemoveXMLartifacts(xmlData);
                var doc = new XmlDocument();
                doc.LoadXml(xmlData);
                return JsonConvert.SerializeXmlNode(doc);
            }
            catch (Exception ex)
            {
                errors.Add(ex.Message);
                return "";
            }
        }

        public static async Task<string> BackupFile(string fileName, RepositoryList db, IFileBrokerConfigurationHelper config)
        {
            try
            {
                string sourceFileName = fileName;
                string sourceFileNoPath = Path.GetFileName(fileName);

                if (fileName.EndsWith(".XML", StringComparison.InvariantCultureIgnoreCase))
                    fileName = Path.GetFileNameWithoutExtension(fileName);

                string fileNameNoCycle = Path.GetFileNameWithoutExtension(fileName);

                string backupRoot = config.FTPbackupRoot;
                string ftpFolder = (await db.Settings.GetSettingsDataForFileName(fileNameNoCycle)).Paths;

                string destinationFileName = backupRoot + ftpFolder + sourceFileNoPath;

                if (!File.Exists(destinationFileName))
                    File.Copy(sourceFileName, destinationFileName);
                else
                    return "Error: could not copy file since it already exists: " + destinationFileName;

                return string.Empty;
            }
            catch (Exception e)
            {
                return "Error: " + e.Message;
            }
        }

        public static async Task<bool> CheckForDuplicateFile(string fullPath, IMailServiceRepository mailService, 
                                                             IFileBrokerConfigurationHelper config)
        {
            var fileInfo = new FileInfo(fullPath);

            string fileName = fileInfo.Name.ToUpper();
            string baseName = TrimCycleAndXmlExtension(fileName);
            string folderName = fileInfo.DirectoryName.ExtractSubfolder();

            var dInfo = new DirectoryInfo(config.FTPbackupRoot.AppendToPath(folderName));
            FileInfo[] dupFileInfos;

            if (fileName.Contains("02010131"))
                dupFileInfos = dInfo.GetFiles(fileName, SearchOption.TopDirectoryOnly).Where(m => m.LastWriteTime > DateTime.Now.AddMonths(-16)).ToArray();
            else if (fileName.Contains("RC3STSIT") || fileName.Contains("HR3SVSIS") ||
                     fileName.Contains("OA3SIS") || fileName.Contains("DOJEEINB"))
                dupFileInfos = dInfo.GetFiles(fileName, SearchOption.TopDirectoryOnly).Where(m => m.LastWriteTime > DateTime.Now.AddMonths(-2)).ToArray();
            else
                dupFileInfos = dInfo.GetFiles(fileName, SearchOption.TopDirectoryOnly).ToArray();

            if (dupFileInfos.Any())
            {
                foreach (var dupFileInfo in dupFileInfos)
                {
                    if (FileEquals(fileInfo.FullName, dupFileInfo.FullName))
                    {
                        string msgBody = $"Inbound file {fileInfo.Name} is identical to file {dupFileInfo.Name} that was received on {dupFileInfo.LastWriteTime:yyyy-MM-dd}";
                        await mailService.SendEmail($"Duplicate file not loaded: {fileInfo.Name}", config.OpsRecipient, msgBody);
                    }
                    else
                    {
                        string msgBody = $"Inbound file {fileInfo.Name} is with the same cycle but different content from file {dupFileInfo.Name} that was received on {dupFileInfo.LastWriteTime:yyyy-MM-dd}";
                        await mailService.SendEmail($"Different file with the same cycle not loaded: {fileInfo.Name}", config.OpsRecipient, msgBody);
                    }
                }
                return true;
            }

            return false;
        }

        private static bool FileEquals(string path1, string path2)
        {
            byte[] file1 = File.ReadAllBytes(path1);
            byte[] file2 = File.ReadAllBytes(path2);

            if (file1.Length != file2.Length)
                return false;

            for (int i = 0; i < file1.Length; i++)
                if (file1[i] != file2[i])
                    return false;

            return true;
        }

        private static string RemoveXMLartifacts(string xmlData)
        {
            string result = xmlData;

            string replacement = string.Empty;
            string pattern = @"<\?xml.*\?>";
            result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase);

            replacement = "";
            pattern = @"[\t\s]+xmlns[^>]+";
            result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase);

            replacement = "";
            pattern = @"[\t\s]+xsi[^>]+";
            result = Regex.Replace(result, pattern, replacement, RegexOptions.IgnoreCase);

            return result;
        }

        public static string FormatDBDateString(string dateString)
        {
            if (!string.IsNullOrEmpty(dateString.Trim()) && (dateString.Length == 8))
            {
                try
                {
                    int day = int.Parse(dateString[0..2]);
                    int month = int.Parse(dateString[2..4]);
                    int year = int.Parse(dateString[4..8]);
                    var dateTime = new DateTime(year, month, day);
                    return dateTime.ToString("yyyy-MM-ddTHH:mm:sszzz");
                }
                catch
                {
                    return string.Empty;
                }
            }
            return string.Empty;
        }

        public static async Task<IActionResult> ExtractAndSaveRequestBodyToFile(string fileName, IFileTableRepository fileTable, 
                                                                                HttpRequest Request)
        {
            string bodyContent;
            string fileNameNoExtension;
            string fileNameNoCycle;
            FileTableData fileData;

            string outputPath;
            string outputFile;

            switch (Request.ContentType?.ToLower())
            {
                case "text/plain":
                    using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                    {
                        bodyContent = await reader.ReadToEndAsync();
                    }

                    fileNameNoCycle = Path.GetFileNameWithoutExtension(fileName);
                    fileData = await fileTable.GetFileTableDataForFileName(fileNameNoCycle);

                    if ((fileData == null) || (string.IsNullOrEmpty(fileData.Path)))
                        return new BadRequestResult();

                    outputPath = fileData.Path;
                    outputFile = outputPath.AppendToPath(fileName, isFileName: true);

                    await File.WriteAllTextAsync(outputFile, bodyContent);

                    return new OkResult();

                case "application/xml":
                case "text/xml":
                    using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                    {
                        bodyContent = await reader.ReadToEndAsync();
                    }

                    fileNameNoExtension = Path.GetFileNameWithoutExtension(fileName);
                    fileNameNoCycle = Path.GetFileNameWithoutExtension(fileNameNoExtension);
                    fileData = await fileTable.GetFileTableDataForFileName(fileNameNoCycle);

                    if ((fileData == null) || (string.IsNullOrEmpty(fileData.Path)))
                        return new BadRequestResult();

                    outputPath = fileData.Path;
                    outputFile = outputPath.AppendToPath(fileName, isFileName: true);

                    await File.WriteAllTextAsync(outputFile, bodyContent, Encoding.UTF8);

                    return new OkResult();

                case "application/json":
                    using (var reader = new StreamReader(Request.Body, Encoding.UTF8))
                    {
                        bodyContent = await reader.ReadToEndAsync();
                    }

                    fileNameNoExtension = Path.GetFileNameWithoutExtension(fileName);
                    fileNameNoCycle = Path.GetFileNameWithoutExtension(fileNameNoExtension);
                    fileData = await fileTable.GetFileTableDataForFileName(fileNameNoCycle);

                    if ((fileData == null) || (string.IsNullOrEmpty(fileData.Path)))
                        return new BadRequestResult();

                    outputPath = fileData.Path;
                    outputFile = outputPath.AppendToPath(fileName, isFileName: true);
                    outputFile = Path.ChangeExtension(outputFile, ".XML");

                    var doc = JsonConvert.DeserializeXNode(bodyContent)!;

                    var declaration = doc.Declaration ?? new XDeclaration("1.0", null, null);

                    await File.WriteAllTextAsync(outputFile, $"{declaration}{Environment.NewLine}{doc}");

                    return new OkResult();

                default:
                    return new BadRequestResult();
            }
        }

    }
}
