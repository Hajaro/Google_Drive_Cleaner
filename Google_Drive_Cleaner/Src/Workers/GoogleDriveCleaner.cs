using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using GoogleDriveFileRemover.Src.Logging;
using Microsoft.Extensions.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Google_Drive_Cleaner.Src.Workers
{
    internal sealed class GoogleDriveCleaner : IGoogleDriveCleaner
    {
        private readonly DriveService _drive;
        private readonly IConfiguration _configuration;

        private GoogleDriveCleaner(DriveService drive, IConfiguration configuration)
        {
            _drive = drive ?? throw new ArgumentNullException(nameof(drive));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public static async Task<IGoogleDriveCleaner> ConnectAsync(IConfiguration configuration, string appName = "GoogleDriveCleaner")
        {
            ArgumentNullException.ThrowIfNull(configuration, nameof(configuration));
            var serviceAccKeyPath = configuration["GoogleDriveSettings:ServiceAccountKeyPath"];
            if (string.IsNullOrEmpty(serviceAccKeyPath))
                throw new ArgumentException("ServiceAccountKey is not provided in configuration.", nameof(configuration));

            if (!File.Exists(serviceAccKeyPath))
                throw new FileNotFoundException("ServiceAccountKey file not found.", serviceAccKeyPath);

            GoogleCredential googleCredentials;
            using var stream = new FileStream(serviceAccKeyPath, FileMode.Open, FileAccess.Read);
            googleCredentials = GoogleCredential.FromStream(stream)
                .CreateScoped([DriveService.Scope.Drive]);

            var driveService = new DriveService(new Google.Apis.Services.BaseClientService.Initializer
            {
                HttpClientInitializer = googleCredentials,
                ApplicationName = appName
            });

            Logger.LogInformation("Connected to Google Drive API successfully.");
            await Task.CompletedTask;
            return new GoogleDriveCleaner(driveService, configuration);
        }

        public async Task<(bool Exist, string? FolderId)> DoesFolderExistsAsync(string folderName, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(folderName);

            var listRequest = _drive.Files.List();
            listRequest.Q = $"mimeType='application/vnd.google-apps.folder' and name='{folderName}' and trashed=false";
            string fields = string.Empty;
            listRequest.Fields = "files(id, name)";

            var pageSizeConfig = _configuration["GoogleDriveSearchResults:PageSize"];
            if (!string.IsNullOrEmpty(pageSizeConfig) && int.TryParse(pageSizeConfig, out _))
                listRequest.PageSize = Convert.ToInt32(pageSizeConfig);
            else
                listRequest.PageSize = 10;
            var supportsAllDrivesConfig = _configuration["GoogleDriveSearchResults:SupportsAllDrives"];
            if (!string.IsNullOrEmpty(supportsAllDrivesConfig) && bool.TryParse(supportsAllDrivesConfig, out var supportsAllDrives))
                listRequest.SupportsAllDrives = supportsAllDrives;
            else
                listRequest.SupportsAllDrives = true;
            var includeItemsFromAllDrivesConfig = _configuration["GoogleDriveSearchResults:IncludeItemsFromAllDrives"];
            if (!string.IsNullOrEmpty(includeItemsFromAllDrivesConfig) && bool.TryParse(includeItemsFromAllDrivesConfig, out var includeItemsFromAllDrives))
                listRequest.IncludeItemsFromAllDrives = includeItemsFromAllDrives;
            else
                listRequest.IncludeItemsFromAllDrives = true;

            var result = await listRequest.ExecuteAsync(cancellationToken);
            var folder = result.Files?.FirstOrDefault();
            if (folder == null)
            {
                Logger.LogWarning($"Folder '{folderName}' does not exist on Google Drive.");
                return (false, null);
            }

            if (result.Files!.Count > 1)
            {
                Logger.LogWarning($"Multiple folders '{folderName}' found. Using the one with ID: {folder.Id}");
                var firstFolder = result.Files.First();
                return (true, firstFolder.Id);
            }

            Logger.LogInformation($"Folder '{folderName}' exists on Google Drive with ID: {folder.Id}");
            return (true, folder.Id);
        }

        public async Task<int> DeleteContentFromFolderByNameAsync(string folderName, CancellationToken cancellationToken)
        {
            var (exist, folderId) = await DoesFolderExistsAsync(folderName, cancellationToken);
            if (!exist || string.IsNullOrEmpty(folderId))
            {
                Logger.LogWarning($"Folder '{folderName}' does not exist. No content to delete.");
                return 0;
            }

            var deleted = await DeleteContentFromFolderByIdAsync(folderId, cancellationToken);
            Logger.LogInformation($"Deleted {deleted} items from folder '{folderName}' (ID: {folderId}).");
            return deleted;


        }

        private async Task<int> DeleteContentFromFolderByIdAsync(string folderId, CancellationToken cancellationToken)
        {
            int deletedCount = 0;

            string? pageToken = null;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var listRequest = _drive.Files.List();
                listRequest.Q = $"'{folderId}' in parents and trashed=false";
                listRequest.Fields = "nextPageToken, files(id, name, mimeType)";
                listRequest.PageSize = 1000;
                listRequest.PageToken = pageToken;
                listRequest.SupportsAllDrives = true;
                listRequest.IncludeItemsFromAllDrives = true;

                var result = await listRequest.ExecuteAsync(cancellationToken);

                foreach (var file in result.Files ?? Enumerable.Empty<Google.Apis.Drive.v3.Data.File>())
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    bool isFolder = string.Equals(file.MimeType, "application/vnd.google-apps.folder", StringComparison.OrdinalIgnoreCase);
                    if (isFolder)
                    {
                        deletedCount += await DeleteContentFromFolderByIdAsync(file.Id, cancellationToken);
                        await DeleteItemAsync(file.Id, file.Name, isFolder: true, cancellationToken);
                        deletedCount++;
                    }
                    else
                    {
                        await DeleteItemAsync(file.Id, file.Name, isFolder: false, cancellationToken);
                        deletedCount++;
                    }
                }
                pageToken = result.NextPageToken;
            } while (!string.IsNullOrEmpty(pageToken));

            return deletedCount;
        }

        private async Task DeleteItemAsync(string id, string name, bool isFolder, CancellationToken cancellationToken)
        {
            try
            {
                var deleteRequest = _drive.Files.Delete(id);
                deleteRequest.SupportsAllDrives = true;
                await deleteRequest.ExecuteAsync(cancellationToken);
                Logger.LogInformation($"{(isFolder ? "Folder" : "File")} '{name}' (ID: {id}) deleted successfully.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error deleting {(isFolder ? "folder" : "file")} '{name}' (ID: {id}): {ex.Message}", ex);
            }
        }
    }
}