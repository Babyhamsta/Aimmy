# Call this script with "powershell -ExecutionPolicy Bypass -File .\build.ps1"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Definition
$outputDir = Join-Path $scriptDir "Aimmy2/bin/Release"

# Check if the output directory exists and delete it
if (Test-Path $outputDir) {
    Remove-Item -Recurse -Force $outputDir
    Write-Host "Output directory $outputDir deleted."
} else {
    Write-Host "Output directory $outputDir does not exist, no need to delete."
}

# Clean the project
dotnet clean --configuration Release

# Build the project
dotnet build --configuration Release --no-incremental

# Get the Assembly name and version from the .csproj file
$csprojPath = Join-Path $scriptDir "Aimmy2/Aimmy2.csproj"
[xml]$csproj = Get-Content $csprojPath


$assemblyName = $csproj.Project.PropertyGroup.AssemblyName
$version = $csproj.Project.PropertyGroup.Version


# Define the zip file name and path
$zipFileName = "$assemblyName`_$version.zip"
$zipFileName  = $zipFileName -replace '(^\s+|\s+$)','' -replace '\s+',' '

$zipFilePath = Join-Path $outputDir $zipFileName

# Compress the output directory into a zip file
Compress-Archive -Path $outputDir\* -DestinationPath $zipFilePath

Write-Host "Output directory compressed into $zipFileName in the Release folder."
