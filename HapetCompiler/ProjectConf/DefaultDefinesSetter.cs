using HapetFrontend;
using System.Reflection;

namespace HapetCompiler.ProjectConf
{
    internal partial class ProjectXmlParser
    {
        private void SetDefaultDefines()
        {
            Assembly assem = Assembly.GetEntryAssembly();
            AssemblyName assemName = assem.GetName();
            string ver = assemName.Version.ToString(3);
            _projectData.Defines.Add("HAPET_VERSION", ver);

            _projectData.Defines.Add("TARGET_PLATFORM", _projectSettings.TargetPlatformData.Name);
            _projectData.Defines.Add("CURRENT_PLATFORM", CompilerSettings.CurrentPlatformData.Name);
        }
    }
}
