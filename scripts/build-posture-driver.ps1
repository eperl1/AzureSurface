param(
    [string]$Configuration = "Release",
    [string]$Platform = "x64",
    [string]$OutputPath = (Join-Path $PSScriptRoot '..\artifacts\windows\SurfacePostureDriver'),
    [string]$CertificateSubject = 'CN=SurfacePostureDriver Test Certificate'
)

$ErrorActionPreference = 'Stop'
Import-Module PKI -ErrorAction Stop

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$project = Join-Path $repoRoot 'windows\SurfacePostureDriver\SurfacePostureDriver.vcxproj'
$driverSourceDir = Join-Path $repoRoot 'windows\SurfacePostureDriver'
$stageDir = Join-Path $OutputPath 'staging'
$packageDir = Join-Path $OutputPath 'package'
$driverBuildRoot = Join-Path $OutputPath 'build'
$driverBuildDir = Join-Path $driverBuildRoot "$Platform\$Configuration"
$artifactZip = Join-Path $OutputPath 'SurfacePostureDriver.zip'

New-Item -ItemType Directory -Force -Path $stageDir, $packageDir, $driverBuildDir | Out-Null

function Get-VsWherePath {
    $path = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (-not (Test-Path $path)) {
        throw 'vswhere.exe not found.'
    }

    return $path
}

function Get-MSBuildPath {
    $vswhere = Get-VsWherePath
    $msbuild = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find MSBuild\**\Bin\MSBuild.exe
    if (-not $msbuild) {
        throw 'MSBuild.exe not found via vswhere.'
    }

    return $msbuild.Trim()
}

function Get-WindowsKitToolPath {
    param([Parameter(Mandatory = $true)][string]$ToolName)

    $roots = @(
        (Join-Path ${env:ProgramFiles(x86)} 'Windows Kits\10\bin'),
        (Join-Path ${env:ProgramFiles} 'Windows Kits\10\bin')
    ) | Where-Object { Test-Path $_ }

    foreach ($arch in @('x64', 'x86')) {
        foreach ($root in $roots) {
            $match = Get-ChildItem -Path $root -Filter $ToolName -Recurse -ErrorAction SilentlyContinue |
                Where-Object { $_.FullName -match "\\$arch\\" } |
                Select-Object -First 1
            if ($match) {
                return $match.FullName
            }
        }
    }

    foreach ($root in $roots) {
        $match = Get-ChildItem -Path $root -Filter $ToolName -Recurse -ErrorAction SilentlyContinue |
            Select-Object -First 1
        if ($match) {
            return $match.FullName
        }
    }

    throw "$ToolName not found under Windows Kits."
}

function New-TestCertificate {
    param([string]$Subject)

    $existing = Get-ChildItem Cert:\CurrentUser\My | Where-Object { $_.Subject -eq $Subject } | Sort-Object NotAfter -Descending | Select-Object -First 1
    if ($existing) {
        return $existing
    }

    return New-SelfSignedCertificate -Type CodeSigningCert -Subject $Subject -CertStoreLocation Cert:\CurrentUser\My -HashAlgorithm SHA256 -KeyExportPolicy Exportable
}

function Copy-DriverPayload {
    param(
        [string]$Destination
    )

    Copy-Item -Path (Join-Path $driverSourceDir 'SurfacePostureDriver.inf') -Destination $Destination -Force
    Copy-Item -Path (Join-Path $repoRoot 'scripts\install-posture-driver.ps1') -Destination $Destination -Force
    Copy-Item -Path (Join-Path $repoRoot 'scripts\uninstall-posture-driver.ps1') -Destination $Destination -Force
    Copy-Item -Path (Join-Path $repoRoot 'scripts\verify-posture-driver.ps1') -Destination $Destination -Force
}

$msbuild = Get-MSBuildPath
& $msbuild $project /t:Build /p:Configuration=$Configuration /p:Platform=$Platform /p:OutDir="$driverBuildDir\" /p:SkipPackageVerification=true /p:ApiValidator_Enable=false /p:EnableInf2cat=false
if ($LASTEXITCODE -ne 0) {
    throw "MSBuild failed with exit code $LASTEXITCODE."
}

$sys = Get-ChildItem -Path $driverBuildDir -Filter 'SurfacePostureDriver.sys' -Recurse | Select-Object -First 1
if (-not $sys) {
    throw 'SurfacePostureDriver.sys was not produced by the build.'
}

$inf = Join-Path $driverSourceDir 'SurfacePostureDriver.inf'
Copy-Item -Path $sys.FullName -Destination $stageDir -Force
Copy-Item -Path $inf -Destination $stageDir -Force

$cert = New-TestCertificate -Subject $CertificateSubject
$cerPath = Join-Path $stageDir 'SurfacePostureDriverTest.cer'
Export-Certificate -Cert $cert -FilePath $cerPath | Out-Null

$inf2cat = Get-WindowsKitToolPath -ToolName 'inf2cat.exe'
& $inf2cat /driver:$stageDir /os:10_X64
if ($LASTEXITCODE -ne 0) {
    throw "Inf2Cat failed with exit code $LASTEXITCODE."
}

$signtool = Get-WindowsKitToolPath -ToolName 'signtool.exe'
$catalog = Join-Path $stageDir 'SurfacePostureDriver.cat'
& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $sys.FullName
if ($LASTEXITCODE -ne 0) {
    throw "Signtool failed while signing $($sys.FullName)."
}
& $signtool sign /fd SHA256 /sha1 $cert.Thumbprint $catalog
if ($LASTEXITCODE -ne 0) {
    throw "Signtool failed while signing $catalog."
}

Copy-DriverPayload -Destination $packageDir
Copy-Item -Path (Join-Path $stageDir '*') -Destination $packageDir -Force

if (Test-Path $artifactZip) {
    Remove-Item $artifactZip -Force
}

Compress-Archive -Path (Join-Path $packageDir '*') -DestinationPath $artifactZip

Write-Host "SurfacePostureDriver package created at $artifactZip"
Write-Host "Staging directory: $packageDir"
Write-Host "Certificate: $cerPath"
