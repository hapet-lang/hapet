#!/bin/bash

# Version
buildVersion=$(<../build-resources/version.txt)

# Clean-up
rm -rf ./staging_folder/

# .NET publish
# self-contained is recommended, so final users won't need to install .NET
dotnet publish ../HapetCompiler/HapetCompiler.csproj --configuration Release --runtime win-x64 --self-contained -f net8.0
# dotnet publish ../SoftHub.Updater/SoftHub.Updater.csproj --configuration Release --runtime win-x64 --self-contained -f net8.0
echo "Published"

dotnet publish ../HashComputer/HashComputer.Cli/HashComputer.Cli.csproj --configuration Release --runtime linux-x64 --self-contained -f net8.0
echo "Hash computer published"

# Staging directory
mkdir staging_folder

# Other files
cp -f -a ../HapetCompiler/bin/Release/net8.0/win-x64/publish/. ./staging_folder/ # copies all files from publish dir
echo "SoftHub copied"

# Updater
# mkdir ./staging_folder/updater__
# cp -f -a ../SoftHub.Updater/bin/Release/net8.0/win-x64/publish/. ./staging_folder/updater__/ # copies all files from publish dir
echo "Updater copied"

# Stables
cp ../build-resources/computed_stables.txt ./staging_folder/computed_stables.txt
echo "Stables copied"

# Hash computer
chmod +x ../HashComputer/HashComputer.Cli/bin/Release/net8.0/linux-x64/publish/HashComputer.Cli
../HashComputer/HashComputer.Cli/bin/Release/net8.0/linux-x64/publish/HashComputer.Cli -v $buildVersion -d ./staging_folder -t 8 -m
echo "Hashes computed"

# Make .exe file
makensis -V1 -DVERSION=$buildVersion ./nsis-setup.nsi
echo ".exe created"
