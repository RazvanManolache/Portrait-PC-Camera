$ErrorActionPreference = 'Stop'
if ([IntPtr]::Size -ne 4) { throw 'Run with the 32-bit Windows PowerShell executable in SysWOW64.' }
$cameraType = [Type]::GetTypeFromCLSID([Guid]'{89084F72-48E3-4CC4-93B5-4F0E8155EA71}', $true)
$camera = [Activator]::CreateInstance($cameraType)
try {
    [pscustomobject]@{ test = '32-bit COM camera activation'; passed = $true; pointerSize = [IntPtr]::Size } | ConvertTo-Json
}
finally { [void][Runtime.InteropServices.Marshal]::FinalReleaseComObject($camera) }
