param([string]$InnoCompiler)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
Set-Location $projectRoot
$version = ([xml](Get-Content src/PortraitCamera/PortraitCamera.csproj -Raw)).Project.PropertyGroup.Version
if ((Get-Content installer/PortraitCamera.iss -Raw) -notmatch ('#define AppVersion "' + [regex]::Escape($version) + '"')) {
    throw 'The app and installer versions must match.'
}
function Invoke-Checked([string]$File, [string[]]$Arguments) {
    & $File @Arguments
    if ($LASTEXITCODE -ne 0) { throw "$File failed with exit code $LASTEXITCODE" }
}
if (-not (Test-Path third_party/softcam/src/softcam/softcam.cpp)) { throw 'Missing vendored Softcam source.' }
foreach ($arch in @(@('x64','x64'), @('x86','Win32'))) {
    Invoke-Checked cmake @('-S','native','-B',"build/native-$($arch[0])",'-G','Visual Studio 17 2022','-A',$arch[1])
    Invoke-Checked cmake @('--build',"build/native-$($arch[0])",'--config','Release','--parallel','8')
}
Invoke-Checked dotnet @('publish','src/PortraitCamera','-c','Release','-r','win-x64','--self-contained','true','-o','dist/app')
$filterData = & .\build\native-x64\Release\PortraitCamera.Registration.exe --metadata
if ($LASTEXITCODE -ne 0 -or $filterData -notmatch '^[0-9a-f]+$') { throw 'Invalid camera registration metadata.' }
$filterData32 = & .\build\native-x86\Release\PortraitCamera.Registration.exe --metadata
if ($LASTEXITCODE -ne 0 -or $filterData32 -ne $filterData) { throw 'Camera metadata differs between architectures.' }
$filterDataForInstaller = ([regex]::Matches($filterData, '..') | ForEach-Object Value) -join ' '
('#define CameraFilterData "' + $filterDataForInstaller + '"') | Set-Content build/filter-metadata.iss
$sourceHash = (Get-FileHash build/native-x64/Release/PortraitCamera.MediaSource.dll).Hash.Substring(0,16).ToLower()
('#define CameraSourceFile "PortraitCamera.MediaSource-' + $sourceHash + '.dll"') | Add-Content build/filter-metadata.iss
Copy-Item build/native-x64/Release/PortraitCamera.Registration.exe dist/app/
Copy-Item build/native-x64/Release/PortraitCamera.MediaSource.dll dist/app/
Copy-Item build/native-x64/Release/PortraitCamera.MfProbe.exe dist/app/
New-Item -ItemType Directory -Force dist/app/camera-x86,dist/app/licenses | Out-Null
Copy-Item build/native-x86/Release/PortraitCamera.Native.dll "dist/app/camera-x86/PortraitCamera.Native-$version.dll"
Copy-Item third_party/softcam/LICENSE dist/app/licenses/Softcam.txt
Copy-Item native/mf/LICENSE dist/app/licenses/VCamSample.txt
Copy-Item third_party/wil/LICENSE dist/app/licenses/WIL.txt
Copy-Item assets/models/LICENSE-YuNet.txt dist/app/licenses/
Copy-Item assets/licenses/* dist/app/licenses/
Copy-Item THIRD-PARTY-NOTICES.txt dist/app/
Copy-Item README.md dist/app/
# Both the native camera and this pinned OpenCV runtime link the C++ runtime statically.
# .NET's required libraries are included by self-contained publish.
if (-not $InnoCompiler) {
    $found = Get-Command ISCC.exe -ErrorAction SilentlyContinue
    if ($found) { $InnoCompiler = $found.Source }
    else {
        $candidates = @("${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe", "$env:ProgramFiles\Inno Setup 6\ISCC.exe")
        $InnoCompiler = $candidates | Where-Object { Test-Path $_ } | Select-Object -First 1
    }
}
if (-not $InnoCompiler) { throw 'Install Inno Setup 6 on the build machine, or pass -InnoCompiler.' }
Invoke-Checked $InnoCompiler @('installer/PortraitCamera.iss')
Get-FileHash "dist/PortraitCamera-Setup-$version-win-x64.exe" -Algorithm SHA256 | ForEach-Object { "$($_.Hash.ToLower())  $([IO.Path]::GetFileName($_.Path))" } | Set-Content dist/SHA256SUMS.txt
