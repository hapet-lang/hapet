$buildVersion = (Get-Content "../build-resources/version.txt" -Raw).Trim()

if (Test-Path "./staging_folder") {
    Remove-Item -Recurse -Force "./staging_folder"
}

Write-Host "Publishing HapetCompiler..."
dotnet publish ../HapetCompiler/HapetCompiler.csproj --configuration Release --runtime win-x64 --self-contained -f net8.0
if ($LASTEXITCODE -ne 0) { throw "Failed to publish HapetCompiler" }

Write-Host "Publishing HashComputer..."
dotnet publish ../HashComputer/HashComputer.Cli/HashComputer.Cli.csproj --configuration Release --runtime win-x64 --self-contained -f net8.0
if ($LASTEXITCODE -ne 0) { throw "Failed to publish HashComputer" }

New-Item -ItemType Directory -Force -Path "./staging_folder"

Write-Host "Copying HapetCompiler files..."
Copy-Item -Path "../HapetCompiler/bin/Release/net8.0/win-x64/publish/*" -Destination "./staging_folder/" -Recurse -Force

Write-Host "Copying Stables..."
Copy-Item -Path "../build-resources/computed_stables.txt" -Destination "./staging_folder/computed_stables.txt" -Force

Write-Host "Copying Linker..."
Copy-Item -Path "../lld-link.exe" -Destination "./staging_folder/lld-link.exe" -Force

Write-Host "Copying STD..."
Copy-Item -Path "../std" -Destination "./staging_folder" -Recurse -Force

Write-Host "Copying .libs..."
$libsDest = "staging_folder/libs/win-x64"
New-Item -ItemType Directory -Force -Path $libsDest | Out-Null
Copy-Item -Path "../.xwin-cache/splat/crt/lib/x86_64/msvcrt.lib" -Destination "$libsDest/" -Force
Copy-Item -Path "../.xwin-cache/splat/sdk/lib/ucrt/x86_64/ucrt.lib" -Destination "$libsDest/" -Force

Write-Host "Computing hashes..."
$hashCompExe = "../HashComputer/HashComputer.Cli/bin/Release/net8.0/win-x64/publish/HashComputer.Cli.exe"
& $hashCompExe -v $buildVersion -d ./staging_folder -t 8 -m
if ($LASTEXITCODE -ne 0) { throw "Hash computation failed" }

Write-Host "Creating .exe installer..."
makensis /V1 /DVERSION=$buildVersion ./nsis-setup-x64.nsi
if ($LASTEXITCODE -ne 0) { throw "NSIS build failed" }

Write-Host "Done! Installer created."