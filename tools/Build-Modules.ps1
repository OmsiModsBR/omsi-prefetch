<#
.SYNOPSIS
Builds the closed OmsiLaunch modules and assembles each one the way it is
installed: artifacts\modules\<id>\ with its module.json (every component pinned
by its SHA-256) and its files. Nothing is copied into an OMSI installation.

.PARAMETER OmsiLaunchRoot
The OmsiLaunch checkout the modules build against (default: the sibling
OmsiLaunch-spec-pack\OmsiLaunch).

.PARAMETER SkipTests
Builds and assembles without running the module tests.

.PARAMETER PlatformToolset
Overrides the C++ platform toolset the projects name (v145), for a Visual Studio
that does not provide it (for example v143). Empty keeps the projects' own.
#>
[CmdletBinding()]
param(
    [string]$OmsiLaunchRoot = '',
    [switch]$SkipTests,
    [string]$PlatformToolset = ''
)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$artifacts = Join-Path $root 'artifacts'
$extra = @()
if ($OmsiLaunchRoot) { $extra += "-p:OmsiLaunchRoot=$((Resolve-Path $OmsiLaunchRoot).Path.TrimEnd('\'))\" }
if ($PlatformToolset) { $extra += "-p:PlatformToolset=$PlatformToolset" }
$omsiLaunch = if ($OmsiLaunchRoot) { (Resolve-Path $OmsiLaunchRoot).Path } else { Join-Path $root '..\OmsiLaunch-spec-pack\OmsiLaunch' }

function Find-MSBuild {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (Test-Path $vswhere) {
        $found = & $vswhere -latest -products * -requires Microsoft.Component.MSBuild -find 'MSBuild\**\Bin\MSBuild.exe' | Select-Object -First 1
        if ($found) { return $found }
    }
    $fallback = 'C:\Program Files (x86)\Microsoft Visual Studio\18\BuildTools\MSBuild\Current\Bin\MSBuild.exe'
    if (Test-Path $fallback) { return $fallback }
    throw 'MSBuild (Visual Studio C++ build tools) was not found.'
}

function Invoke-Step([string]$What, [scriptblock]$Command) {
    Write-Host "== $What"
    & $Command
    if ($LASTEXITCODE -ne 0) { throw "$What failed (exit code $LASTEXITCODE)" }
}

function Write-Manifest([string]$Template, [string]$Directory) {
    $text = [IO.File]::ReadAllText($Template)
    $pinned = [regex]::Replace($text, '"path": "([^"]+)", "sha256": ""', {
        param($match)
        $file = Join-Path $Directory $match.Groups[1].Value
        if (-not (Test-Path -LiteralPath $file)) { throw "the manifest names $($match.Groups[1].Value), which is not in $Directory" }
        $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
        '"path": "' + $match.Groups[1].Value + '", "sha256": "' + $hash + '"'
    })
    [IO.File]::WriteAllText((Join-Path $Directory 'module.json'), $pinned, [Text.UTF8Encoding]::new($false))
}

$msbuild = Find-MSBuild

# ---- omsi-prefetch
Invoke-Step 'prefetch: native component (x86)' { & $msbuild (Join-Path $root 'prefetch\native\prefetch-native.vcxproj') -p:Configuration=Release -p:Platform=Win32 -nologo -v:minimal @extra }
Invoke-Step 'prefetch: managed component' { dotnet build (Join-Path $root 'prefetch\module\Prefetch.Module.csproj') -c Release -nologo -v:minimal @extra }
$preloader = Join-Path $artifacts 'publish\prefetch\preloader'
Invoke-Step 'prefetch: session program (x64, one file)' {
    dotnet publish (Join-Path $root 'prefetch\preloader\Prefetch.Preloader.csproj') -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -p:DebugType=none -o $preloader -nologo -v:minimal @extra
}
$module = Join-Path $artifacts 'modules\omsi-prefetch'
if (Test-Path $module) { Remove-Item -LiteralPath $module -Recurse -Force }
New-Item -ItemType Directory -Path $module | Out-Null
Copy-Item (Join-Path $artifacts 'bin\Prefetch.Module\Release\net6.0\Prefetch.Module.dll') $module
Copy-Item (Join-Path $artifacts 'bin\prefetch\native\prefetch-native.dll') $module
Copy-Item (Join-Path $preloader 'Prefetch.Preloader.exe') $module
# The module uses OmsiLaunch (LGPL-3.0-only): its notice and the license text go with it.
Copy-Item (Join-Path $root 'prefetch\NOTICE.txt') $module
Copy-Item (Join-Path $omsiLaunch 'LICENSE') (Join-Path $module 'LGPL-3.0.txt')
Copy-Item (Join-Path $omsiLaunch 'COPYING') (Join-Path $module 'GPL-3.0.txt')
Write-Manifest (Join-Path $root 'prefetch\module.template.json') $module
Write-Host "prefetch: assembled in $module"
Get-ChildItem $module | ForEach-Object { Write-Host ("  {0,-24} {1,12:N0} bytes" -f $_.Name, $_.Length) }

if (-not $SkipTests) {
    Invoke-Step 'prefetch: native component tests' {
        & $msbuild (Join-Path $root 'prefetch\tests\native\prefetch-native-tests.vcxproj') -p:Configuration=Release -p:Platform=Win32 -nologo -v:minimal @extra
        if ($LASTEXITCODE -eq 0) { & (Join-Path $artifacts 'bin\prefetch\tests\native\ShimTests.exe') }
    }
    Invoke-Step 'prefetch: preloader and package tests' {
        dotnet build (Join-Path $root 'prefetch\tests\Prefetch.Tests\Prefetch.Tests.csproj') -c Release -nologo -v:minimal @extra
        if ($LASTEXITCODE -eq 0) { & (Join-Path $artifacts 'bin\Prefetch.Tests\Release\net6.0-windows\win-x64\Prefetch.Tests.exe') --package $module }
    }
}
Write-Host 'done'
