using System.Threading;

using Google_Drive_Cleaner.Src.Workers;
using GoogleDriveFileRemover.Src.Helpers;
using GoogleDriveFileRemover.Src.Logging;

namespace Google_Drive_Cleaner.Src
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            var showExitText = true;

            using var cancelationToken = new CancellationTokenSource();
            Console.CancelKeyPress += (s, e) =>
            {
                e.Cancel = true;
                cancelationToken.Cancel();
                Logger.LogWarning("Ctrl+C pressed.");
            };

            var googleDriveWorker = null as IGoogleDriveCleaner;
            var configuration = ConfigurationHelper.InitializeConfig();
            Logger.InitializeSeriLog(configuration, "GoogleDriveCleaner");
            try
            {
                googleDriveWorker = await GoogleDriveCleaner.ConnectAsync(configuration);
                if (googleDriveWorker == null)
                {
                    Logger.LogFatal("Google Drive Worker failed to initialize.");
                    throw new Exception("Google Drive Worker failed to initialize.");
                }

                var googleDriveFolders = configuration.GetSection("GoogleDriveSettings:GoogleDriveFolders");
                if (googleDriveFolders == null || !googleDriveFolders.GetChildren().Any())
                {
                    Logger.LogFatal("No folders provided to check in configuration.");
                    throw new ArgumentException("No folders provided to check in configuration.");
                }

                foreach (var folder in googleDriveFolders.GetChildren())
                {
                    cancelationToken.Token.ThrowIfCancellationRequested();

                    var (exist, folderId) = await googleDriveWorker.DoesFolderExistsAsync(folder.Value!, cancelationToken.Token);
                    if (!exist)
                    {
                        Logger.LogWarning($"Folder: {folder.Value} does not exist in Google Drive.");
                    }
                    else
                    {
                        Logger.LogInformation($"Folder: {folder.Value} exists in Google Drive with ID: {folderId}");
                        var deletedCount = await googleDriveWorker.DeleteContentFromFolderByNameAsync(folder.Value!, cancelationToken.Token);
                        Logger.LogInformation($"Deleted {deletedCount} items from '{folder.Value}'.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Logger.LogWarning("Operation canceled by user.");
                showExitText = false;
            }
            catch (ArgumentNullException ex)
            {
                Logger.LogFatal($"Parameter: {ex.ParamName} passed to program was null.");
            }
            catch (ArgumentException ex)
            {
                Logger.LogFatal($"An argument was invalid: {ex.Message}, param: {ex.ParamName}");
            }
            catch (FileNotFoundException ex)
            {
                Logger.LogFatal($"A required file was not found: {ex.FileName}");
            }
            catch (Exception ex)
            {
                Logger.LogFatal($"Error occured exiting the program.\nCheck logs and restart the app.\n{ex.Message}");
            }
            finally
            {
                if (showExitText)
                {
                    Console.WriteLine("Press any key to exit...");
                    Console.ReadKey(intercept: true);
                }
                Logger.CloseAndFlush();
            }
        }
    }
}