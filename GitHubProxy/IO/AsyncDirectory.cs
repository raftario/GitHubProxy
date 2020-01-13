using System.IO;
using System.Threading.Tasks;

namespace GitHubProxy.IO
{
    internal static class AsyncDirectory
    {
        public static async Task Copy(string source, string dest)
        {
            var dir = new DirectoryInfo(source);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    $"Source directory does not exist or could not be found: {source}");
            }

            if (Directory.Exists(dest))
            {
                await Delete(dest);
            }
            Directory.CreateDirectory(dest);

            var files = dir.GetFiles();
            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    var path = Path.Combine(dest, file.Name);
                    file.CopyTo(path);
                }
            });

            var dirs = dir.GetDirectories();
            foreach (var subdir in dirs)
            {
                var path = Path.Combine(dest, subdir.Name);
                await Copy(subdir.FullName, path);
            }
        }

        public static async Task Delete(string path)
        {
            var dir = new DirectoryInfo(path);
            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    $"Source directory does not exist or could not be found: {path}");
            }

            var files = dir.GetFiles();
            await Task.Run(() =>
            {
                foreach (var file in files)
                {
                    file.Attributes = FileAttributes.Normal;
                    file.Delete();
                }
            });

            var dirs = dir.GetDirectories();
            foreach (var subdir in dirs)
            {
                var subpath = Path.Combine(path, subdir.Name);
                await Delete(subpath);
            }
        }
    }
}
