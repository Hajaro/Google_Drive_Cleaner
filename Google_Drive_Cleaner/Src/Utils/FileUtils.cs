using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GoogleDriveFileRemover.Src.Utils
{
    public static class FileUtils
    {
        public static void EnsurePathExists(string path)
        {
            if (string.IsNullOrEmpty(path) || Directory.Exists(path) || File.Exists(path))
                return;

            if (!Path.HasExtension(path))
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                var dir = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                File.Create(path).Close();
            }
        }
    }
}
