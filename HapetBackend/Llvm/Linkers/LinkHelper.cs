using HapetFrontend;
using System.Reflection;

namespace HapetBackend.Llvm.Linkers
{
    public static class LinkHelper
    {
        public static bool GetLibraryPaths(string name, string prjOutFolder, out (string, string) data)
        {
            // TODO: is there .lib file when we are on linux?
            string fileName = $"{name}.mpt";
            string theAssemblyName;
            string pathToLink;

            // relative to compiler
            if (File.Exists(fileName) && !Path.IsPathRooted(fileName))
            {
                pathToLink = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location);
                var dirName = Path.GetDirectoryName(fileName);
                pathToLink = $"{pathToLink.Replace('\\', '/').TrimEnd('/')}/{dirName.Replace('\\', '/')}";
                theAssemblyName = Path.GetFileNameWithoutExtension(fileName);
            }
            // absolute path check 
            else if (File.Exists(fileName) && Path.IsPathRooted(fileName))
            {
                pathToLink = Path.GetDirectoryName(fileName).Replace('\\', '/');
                theAssemblyName = Path.GetFileNameWithoutExtension(fileName);
            }
            // relative to out folder
            else if (File.Exists($"{prjOutFolder}/{fileName}"))
            {
                ArgumentNullException.ThrowIfNull(prjOutFolder);

                pathToLink = prjOutFolder;
                var dirName = Path.GetDirectoryName(fileName);
                pathToLink = $"{pathToLink.Replace('\\', '/').TrimEnd('/')}/{dirName.Replace('\\', '/')}";
                theAssemblyName = Path.GetFileNameWithoutExtension(fileName);
            }
            else
            {
                data = ("", "");
                return false;
            }
            data = (pathToLink, theAssemblyName);
            return true;
        }
    }
}
