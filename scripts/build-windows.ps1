param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\windows\SurfaceModeReceiver\bin\Release\publish')
)

$solution = Join-Path $PSScriptRoot '..\windows\SurfaceModeReceiver\SurfaceModeReceiver.sln'
$project = Join-Path $PSScriptRoot '..\windows\SurfaceModeReceiver\src\SurfaceModeReceiver\SurfaceModeReceiver.csproj'

dotnet restore $solution
dotnet test $solution --configuration $Configuration --no-restore
dotnet publish $project --configuration $Configuration --runtime $Runtime --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o $OutputPath
