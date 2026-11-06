param([Parameter(Mandatory=$true)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
$result = [ordered]@{ package = ''; registryVisible = $false; comActivation = $false; cameras = @(); error = $null }
try {
    $result.windowsCameras = @(& "$env:ProgramFiles\Portrait Camera\PortraitCamera.MfProbe.exe" --list)
    $result.nativeCameraVisible = $LASTEXITCODE -eq 0
    $nativeKey = [Microsoft.Win32.Registry]::ClassesRoot.OpenSubKey('CLSID\{89084F72-48E3-4CC4-93B5-4F0E8155EA71}\InprocServer32')
    $nativeFile = [IO.Path]::GetFileName($nativeKey.GetValue('')); $nativeKey.Dispose()
    $code = @'
using System;
using System.Text;
using System.Runtime.InteropServices;
public static class PortraitPackageProbe {
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern bool SetDllDirectory(string path);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)]
    public static extern int GetCurrentPackageFullName(ref uint count, StringBuilder name);
    [DllImport("NATIVE_FILE", CallingConvention=CallingConvention.Cdecl, CharSet=CharSet.Unicode)]
    public static extern int pcListCameras(StringBuilder output, int capacity);
}
'@
    Add-Type -TypeDefinition ($code.Replace('NATIVE_FILE', $nativeFile))
    [void][PortraitPackageProbe]::SetDllDirectory((Join-Path $env:ProgramFiles 'Portrait Camera'))
    [uint32]$length = 4096
    $packageName = New-Object Text.StringBuilder 4096
    $packageStatus = [PortraitPackageProbe]::GetCurrentPackageFullName([ref]$length, $packageName)
    $result.package = $packageName.ToString()
    $result.packageStatus = $packageStatus
    $key = [Microsoft.Win32.Registry]::ClassesRoot.OpenSubKey('CLSID\{89084F72-48E3-4CC4-93B5-4F0E8155EA71}\InprocServer32')
    if ($key) { $result.registryVisible = $true; $result.library = $key.GetValue(''); $key.Dispose() }
    $buffer = New-Object Text.StringBuilder 65536
    [void][PortraitPackageProbe]::pcListCameras($buffer, $buffer.Capacity)
    $result.cameras = @($buffer.ToString().Split("`n") | Where-Object { $_ } | ForEach-Object { ($_ -split "`t",3)[1] })
    $cameraType = [Type]::GetTypeFromCLSID([Guid]'{89084F72-48E3-4CC4-93B5-4F0E8155EA71}', $true)
    $camera = [Activator]::CreateInstance($cameraType)
    $result.comActivation = $true
    [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($camera)
}
catch { $result.error = $_.Exception.ToString() }
$result | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath $OutputPath


