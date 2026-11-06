param([Parameter(Mandatory)][string]$Tag)
$ErrorActionPreference = 'Stop'
Set-Location (Split-Path $PSScriptRoot -Parent)
if ($Tag -notmatch '^v\d+\.\d+\.\d+$' -or $env:GITHUB_REF -ne "refs/tags/$Tag") {
    throw 'Publishing requires a stable version tag.'
}
$version = ([xml](Get-Content src/PortraitCamera/PortraitCamera.csproj -Raw)).Project.PropertyGroup.Version
if ($Tag -ne "v$version") { throw 'Tag does not match the app version.' }
if ((Get-Content dist/BUILD-COMMIT.txt -Raw).Trim() -ne $env:GITHUB_SHA) {
    throw 'Build artifact does not match this source commit.'
}
$report = Get-Content dist/self-test/results.json -Raw | ConvertFrom-Json
if (!$report.passed) { throw 'Build self-tests did not pass.' }
$installer = "PortraitCamera-Setup-$version-win-x64.exe"
$hash = (Get-FileHash "dist/$installer" -Algorithm SHA256).Hash.ToLowerInvariant()
if ((Get-Content dist/SHA256SUMS.txt -Raw).Trim() -ne "$hash  $installer") {
    throw 'Installer checksum does not match.'
}
$notes = "docs/releases/$Tag.md"
if (!(Test-Path $notes)) { throw 'Release notes are missing.' }
$repo = $env:GITHUB_REPOSITORY
if (!$repo) { throw 'Repository is missing.' }
function Invoke-Gh([string[]]$Arguments) {
    $result = & gh @Arguments
    if ($LASTEXITCODE -ne 0) { throw "GitHub command failed: $($Arguments[0])" }
    return $result
}
# Resume an interrupted draft, but never replace a published release.
$releases = (Invoke-Gh @('api', "repos/$repo/releases?per_page=100")) | ConvertFrom-Json
$existing = $releases | Where-Object tag_name -eq $Tag | Select-Object -First 1
if ($existing -and !$existing.draft) { throw 'This version is already published; use a new version.' }
if (!$existing) {
    Invoke-Gh @('release', 'create', $Tag, '--repo', $repo, '--verify-tag', '--draft', '--title', "Portrait Camera $Tag", '--notes-file', $notes)
}
$files = @($installer, 'SHA256SUMS.txt', 'BUILD-COMMIT.txt')
$upload = @('release', 'upload', $Tag, '--repo', $repo, '--clobber') + @($files | ForEach-Object { "dist/$_" })
Invoke-Gh $upload
$releases = (Invoke-Gh @('api', "repos/$repo/releases?per_page=100")) | ConvertFrom-Json
$draft = $releases | Where-Object tag_name -eq $Tag | Select-Object -First 1
if (!$draft -or !$draft.draft -or @($draft.assets).Count -ne $files.Count) { throw 'Unexpected release asset list.' }
foreach ($file in $files) {
    $asset = $draft.assets | Where-Object name -eq $file
    $expected = 'sha256:' + (Get-FileHash "dist/$file" -Algorithm SHA256).Hash.ToLowerInvariant()
    if (!$asset -or $asset.digest -ne $expected) { throw "Uploaded digest mismatch: $file" }
}
# Publish only after all uploaded assets have passed the server-side digest check.
Invoke-Gh @('release', 'edit', $Tag, '--repo', $repo, '--draft=false', '--latest', '--notes-file', $notes)
"Published $Tag from $env:GITHUB_SHA" | Add-Content $env:GITHUB_STEP_SUMMARY
