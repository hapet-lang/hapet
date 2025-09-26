using HapetFrontend;
using System.Reflection;

namespace HapetCompiler.ProjectConf
{
    public partial class ProjectXmlParser
    {
        private void SetDefaultDefines()
        {
            // version as string
            Assembly assem = Assembly.GetEntryAssembly();
            AssemblyName assemName = assem.GetName();
            string ver = assemName.Version.ToString(3);
            _projectData.Defines.Add("HAPET_VERSION", ver);

            // target and current
            _projectData.Defines.Add("TARGET_PLATFORM", _projectSettings.TargetPlatformData.Name);
            _projectData.Defines.Add("CURRENT_PLATFORM", CompilerSettings.CurrentPlatformData.Name);

            // debug or release
            if (_projectSettings.IsDebug)
                _projectData.Defines.Add("DEBUG", null);
            else
                _projectData.Defines.Add("RELEASE", null);

            // setting target and current arch
            SetArch(_projectSettings.TargetPlatformData, "TARGET_");
            SetArch(CompilerSettings.CurrentPlatformData, "CURRENT_");

            void SetArch(PlatformData data, string additionalString)
            {
                // arch
                switch (data.TargetPlatform)
                {
                    case TargetPlatform.Win86:
                    case TargetPlatform.Linux86:
                        {
                            _projectData.Defines.Add($"{additionalString}ARCH", "x86");
                            break;
                        }
                    case TargetPlatform.Win64:
                    case TargetPlatform.Linux64:
                        {
                            _projectData.Defines.Add($"{additionalString}ARCH", "x64");
                            break;
                        }
                }
            }
        }
    }
}
