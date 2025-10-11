using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Google_Drive_Cleaner.Src.Workers
{
    internal interface IGoogleDriveCleaner
    {
        Task<(bool Exist, string? FolderId)> DoesFolderExistsAsync(string folderName, CancellationToken cancellationToken);
        Task<int> DeleteContentFromFolderByNameAsync(string folderName, CancellationToken cancellationToken);
    }
}
