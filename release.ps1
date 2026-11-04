[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$Version,
    [Parameter(Mandatory = $true)][string]$Notes,
    [string]$Repo = 'jfong878/Qalam',
    [switch]$BuildOnly,
    [switch]$PublishOnly,
    [switch]$Resume
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dist = Join-Path $root 'dist'
$repo = $Repo
$assemblyPath = Join-Path $root 'src\AssemblyInfo.cs'
$buildScript = Join-Path $root 'build-installer.ps1'
$utf8 = New-Object System.Text.UTF8Encoding($false)

if ($BuildOnly -and $PublishOnly) {
    throw 'Choose either -BuildOnly or -PublishOnly, not both.'
}
if ([string]::IsNullOrWhiteSpace($Notes)) {
    throw 'Release notes are required; pass a non-empty -Notes value.'
}

$Version = $Version.Trim().TrimStart('v')
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw 'Version must be a three-part version such as 1.0.1.'
}
$assemblyVersion = $Version + '.0'
[Version]$targetVersion = [Version]$assemblyVersion
$tag = 'v' + $Version
$setupPath = Join-Path $dist ('Qalam-Setup-' + $Version + '.exe')
$manifestPath = Join-Path $dist 'latest.json'
$ymlPath = Join-Path $dist 'latest.yml'
$exePath = Join-Path $dist 'Qalam.exe'

function Write-Step([string]$Message) {
    Write-Host ('==> ' + $Message)
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function Get-GhPath {
    $local = Join-Path $root '.tools\gh\bin\gh.exe'
    if (Test-Path -LiteralPath $local) {
        if ([string]::IsNullOrWhiteSpace($env:GH_CONFIG_DIR)) {
            $env:GH_CONFIG_DIR = Join-Path $root '.tools\gh-config'
        }
        New-Item -ItemType Directory -Force -Path $env:GH_CONFIG_DIR | Out-Null
        return $local
    }
    $command = Get-Command gh.exe -ErrorAction SilentlyContinue
    if (-not $command) { throw 'GitHub CLI (gh.exe) was not found.' }
    return $command.Source
}

function Get-Manifest {
    if (-not (Test-Path -LiteralPath $manifestPath)) {
        throw ('Update manifest is missing: ' + $manifestPath)
    }
    return [IO.File]::ReadAllText($manifestPath, [Text.Encoding]::UTF8) | ConvertFrom-Json
}

function Invoke-FormatSuite {
    $python = Get-Command python.exe -ErrorAction SilentlyContinue
    if (-not $python) { throw 'Python is required for the PDF formatting release gate.' }
    $suite = Join-Path $root 'selftest\format_suite.py'
    $fixture = Join-Path $root 'selftest\format30\source.pdf'
    if (-not (Test-Path -LiteralPath $suite)) { throw ('Formatting suite missing: ' + $suite) }
    if (-not (Test-Path -LiteralPath $fixture)) { throw ('Formatting fixture missing: ' + $fixture) }
    $suiteOut = Join-Path $root 'selftest\format30\release-run'
    Write-Step 'Running the 15-language PDF formatting suite'
    & $python.Source $suite --exe $exePath --fixture $fixture --out $suiteOut
    if ($LASTEXITCODE -ne 0) {
        throw 'PDF formatting suite failed; refusing to publish the release.'
    }
}

function Assert-BuildArtifacts {
    foreach ($path in @($exePath, $setupPath, $manifestPath, $ymlPath)) {
        if (-not (Test-Path -LiteralPath $path)) {
            throw ('Release artifact is missing: ' + $path)
        }
    }

    $built = [Reflection.AssemblyName]::GetAssemblyName($exePath).Version
    if ($built -ne $targetVersion) {
        throw ('Built executable is ' + $built + ', expected ' + $targetVersion + '.')
    }

    $manifest = Get-Manifest
    if ([string]$manifest.version -ne $Version) {
        throw ('latest.json version is ' + $manifest.version + ', expected ' + $Version + '.')
    }
    # The manifest carries the absolute versioned URL, so a lagging
    # /releases/latest alias cannot turn an update check into a 404.
    $leaf = Split-Path -Leaf $setupPath
    $expectedUrl = 'https://github.com/jfong878/Qalam/releases/download/v' + $Version + '/' + $leaf
    if ([string]$manifest.url -ne $expectedUrl -and [string]$manifest.url -ne $leaf) {
        throw ('latest.json url is ' + $manifest.url + ', expected ' + $expectedUrl + '.')
    }

    $setup = Get-Item -LiteralPath $setupPath
    $sha256 = Get-Sha256 $setupPath
    if ([string]$manifest.sha256 -ne $sha256) {
        throw 'latest.json SHA-256 does not match the setup.'
    }
    if ([long]$manifest.size -ne $setup.Length) {
        throw 'latest.json size does not match the setup.'
    }
    if ([long]$manifest.size -ne $setup.Length -or $setup.Length -le 0) {
        throw 'The setup has an invalid size.'
    }

    $yml = [IO.File]::ReadAllText($ymlPath, [Text.Encoding]::UTF8)
    if ($yml.IndexOf('version: ' + $Version, [StringComparison]::Ordinal) -lt 0) {
        throw 'latest.yml does not contain the release version.'
    }
    if ($yml.IndexOf('path: ' + (Split-Path -Leaf $setupPath), [StringComparison]::Ordinal) -lt 0) {
        throw 'latest.yml does not contain the setup filename.'
    }
    $sha512 = [Convert]::ToBase64String([Security.Cryptography.SHA512]::Create().ComputeHash([IO.File]::ReadAllBytes($setupPath)))
    if ($yml.IndexOf($sha512, [StringComparison]::Ordinal) -lt 0) {
        throw 'latest.yml does not contain the setup SHA-512.'
    }
    if ($yml.IndexOf('size: ' + $setup.Length, [StringComparison]::Ordinal) -lt 0) {
        throw 'latest.yml does not contain the setup size.'
    }

    return [pscustomobject]@{
        Manifest = $manifest
        Sha256 = $sha256
        Size = $setup.Length
    }
}

if (-not $PublishOnly) {
    Write-Step ('Preparing Qalam ' + $Version)
    $assembly = [IO.File]::ReadAllText($assemblyPath, [Text.Encoding]::UTF8)
    $match = [regex]::Match($assembly, '\[assembly:\s*AssemblyVersion\("([^"]+)"\)\]')
    if (-not $match.Success) { throw 'AssemblyVersion was not found in src\AssemblyInfo.cs.' }
    [Version]$currentVersion = $match.Groups[1].Value
    if ($targetVersion -le $currentVersion) {
        throw ('Version must increase: current is ' + $currentVersion + ', requested is ' + $targetVersion + '.')
    }

    $assembly = [regex]::Replace($assembly,
        '(\[assembly:\s*AssemblyVersion\(")[^"]+("\))',
        ('${1}' + $assemblyVersion + '${2}'))
    $assembly = [regex]::Replace($assembly,
        '(\[assembly:\s*AssemblyFileVersion\(")[^"]+("\))',
        ('${1}' + $assemblyVersion + '${2}'))
    $assembly = [regex]::Replace($assembly,
        '(\[assembly:\s*AssemblyInformationalVersion\(")[^"]+("\))',
        ('${1}' + $Version + '${2}'))
    [IO.File]::WriteAllText($assemblyPath, $assembly, $utf8)
    Write-Step ('Set assembly version to ' + $assemblyVersion)

    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File $buildScript -Version $Version -Notes $Notes
    if ($LASTEXITCODE -ne 0) {
        throw ('Installer build failed with exit code ' + $LASTEXITCODE + '.')
    }
}
else {
    Write-Step ('Using existing Qalam ' + $Version + ' artifacts')
    $assembly = [IO.File]::ReadAllText($assemblyPath, [Text.Encoding]::UTF8)
    $match = [regex]::Match($assembly, '\[assembly:\s*AssemblyVersion\("([^"]+)"\)\]')
    if (-not $match.Success) { throw 'AssemblyVersion was not found in src\AssemblyInfo.cs.' }
    [Version]$currentVersion = $match.Groups[1].Value
    if ($currentVersion -ne $targetVersion) {
        throw ('Assembly version is ' + $currentVersion + ', expected ' + $targetVersion + '. Rebuild first.')
    }
}

$verified = Assert-BuildArtifacts
Write-Step ('Verified setup: ' + $verified.Size + ' bytes, SHA-256 ' + $verified.Sha256)

# This is a release gate, not a post-release check. It runs against the exact
# binary and payload that were just built, before any draft is uploaded.
Invoke-FormatSuite

if ($BuildOnly) {
    Write-Host ''
    Write-Host ('Build only. Publish with: .\release.ps1 -Version ' + $Version + ' -Notes "..." -PublishOnly')
    return
}

$gh = Get-GhPath
& $gh auth status --hostname github.com 2>&1 | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'GitHub CLI is not signed in.' }

$repoRaw = & $gh repo view $repo --json visibility,isPrivate 2>&1
if ($LASTEXITCODE -ne 0) { throw ('Cannot read release repository: ' + ($repoRaw -join ' ')) }
$repoInfo = ($repoRaw -join "`n") | ConvertFrom-Json
if ([string]$repoInfo.visibility -ne 'PUBLIC' -or [bool]$repoInfo.isPrivate) {
    throw 'The release repository must be public.'
}

$feedUrl = 'https://github.com/' + $repo + '/releases/latest/download/latest.json'
Write-Step ('Checking current public feed: ' + $feedUrl)
# A repository's first release has no feed yet. That is the only case where a
# 404 is expected; every other failure still refuses to publish blind.
[Version]$remoteVersion = [Version]'0.0.0'
try {
    $remote = Invoke-RestMethod -Uri $feedUrl -UseBasicParsing -Headers @{
        'Cache-Control' = 'no-cache'
        'Pragma' = 'no-cache'
    }
    $remoteVersion = [Version]([string]$remote.version)
}
catch {
    $status = 0
    try { $status = [int]$_.Exception.Response.StatusCode } catch { }
    if ($status -ne 404) {
        throw ('Could not read the current public update feed. Refusing to publish: ' + $_.Exception.Message)
    }
    Write-Step 'No feed yet; treating this as the first release'
}
if ($remoteVersion -ge $targetVersion) {
    throw ('Public feed is already at ' + $remoteVersion + '; refusing to publish ' + $targetVersion + '.')
}
Write-Step ('Public feed is at ' + $remoteVersion)

$temp = Join-Path ([IO.Path]::GetTempPath()) ('qalam-release-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Force -Path $temp | Out-Null
$notesPath = Join-Path $temp 'notes.txt'
[IO.File]::WriteAllText($notesPath, $Notes, $utf8)

$releasesRaw = & $gh api ('repos/' + $repo + '/releases?per_page=100') 2>$null
if ($LASTEXITCODE -ne 0) { throw 'Cannot list releases; refusing to publish.' }
$allReleases = @()
if ($releasesRaw) { $allReleases = @(($releasesRaw -join "`n") | ConvertFrom-Json) }
$existing = @($allReleases | Where-Object { $_.tag_name -eq $tag }) | Select-Object -First 1
if ($null -ne $existing) {
    if (-not $Resume) {
        throw ('Release ' + $tag + ' already exists. Use -Resume only for a draft.')
    }
    if (-not [bool]$existing.draft) {
        throw ('Release ' + $tag + ' is already published and cannot be replaced.')
    }
    Write-Step ('Replacing interrupted draft ' + $tag)
    & $gh api --method DELETE ('repos/' + $repo + '/releases/' + $existing.id) 2>$null
    if ($LASTEXITCODE -ne 0) { throw 'Could not remove the interrupted draft.' }
}

Write-Step ('Creating draft release ' + $tag)
& $gh release create $tag --repo $repo --draft --title ('Qalam ' + $Version) --notes-file $notesPath $setupPath $manifestPath $ymlPath
if ($LASTEXITCODE -ne 0) { throw 'Creating the draft release failed.' }

$draftRaw = & $gh api ('repos/' + $repo + '/releases?per_page=100') 2>$null
if ($LASTEXITCODE -ne 0) { throw 'Cannot verify the draft release after upload.' }
$draft = @((($draftRaw -join "`n") | ConvertFrom-Json) | Where-Object { $_.tag_name -eq $tag }) | Select-Object -First 1
if ($null -eq $draft) { throw 'Draft release was not found after creation.' }
if (-not [bool]$draft.draft) { throw 'Expected a draft release before publishing.' }

$expected = @{
    (Split-Path -Leaf $setupPath) = $verified.Sha256
    'latest.json' = (Get-Sha256 $manifestPath)
    'latest.yml' = (Get-Sha256 $ymlPath)
}
foreach ($name in $expected.Keys) {
    $asset = @($draft.assets | Where-Object { $_.name -eq $name }) | Select-Object -First 1
    if (-not $asset) { throw ('Draft release is missing asset: ' + $name) }
    if ([long]$asset.size -le 0) { throw ('Draft asset has an invalid size: ' + $name) }

    $digest = [string]$asset.digest
    if ($digest) {
        if ($digest.ToLowerInvariant() -ne ('sha256:' + $expected[$name])) {
            throw ('Draft asset digest does not match: ' + $name)
        }
    }
    else {
        $downloadDir = Join-Path $temp 'draft'
        New-Item -ItemType Directory -Force -Path $downloadDir | Out-Null
        & $gh release download $tag --repo $repo --pattern $name --dir $downloadDir --clobber
        if ($LASTEXITCODE -ne 0) { throw ('Could not download draft asset for verification: ' + $name) }
        $downloaded = Join-Path $downloadDir $name
        if ((Get-Sha256 $downloaded) -ne $expected[$name]) {
            throw ('Draft asset hash does not match: ' + $name)
        }
    }
}
Write-Step 'Verified every draft asset'

Write-Step ('Publishing ' + $tag + ' as latest')
$patch = [ordered]@{
    draft = $false
    prerelease = $false
    make_latest = 'true'
    name = 'Qalam ' + $Version
    body = $Notes
}
$patchPath = Join-Path $temp 'release-patch.json'
[IO.File]::WriteAllText($patchPath, ($patch | ConvertTo-Json -Compress), $utf8)
$publishedRaw = & $gh api --method PATCH ('repos/' + $repo + '/releases/' + $draft.id) --input $patchPath 2>$null
if ($LASTEXITCODE -ne 0) { throw 'Publishing the release failed.' }
$published = ($publishedRaw -join "`n") | ConvertFrom-Json
if ([bool]$published.draft -or [bool]$published.prerelease) {
    throw 'The release is still a draft or prerelease.'
}

Write-Step 'Waiting for the public feed to serve the new version'
$deadline = (Get-Date).AddSeconds(90)
$publicManifest = $null
while ((Get-Date) -lt $deadline) {
    try {
        $publicManifest = Invoke-RestMethod -Uri ($feedUrl + '?cb=' + [DateTime]::UtcNow.Ticks) -UseBasicParsing -Headers @{
            'Cache-Control' = 'no-cache'
            'Pragma' = 'no-cache'
        }
        if ([string]$publicManifest.version -eq $Version) { break }
    }
    catch { }
    Start-Sleep -Seconds 5
}
if ($null -eq $publicManifest -or [string]$publicManifest.version -ne $Version) {
    throw ('The release is published, but the public feed did not reach ' + $Version + ' within 90 seconds.')
}
$feedLeaf = Split-Path -Leaf ([string]$publicManifest.url)
if ($feedLeaf -ne (Split-Path -Leaf $setupPath)) {
    throw ('The public feed points to the wrong installer filename: ' + $feedLeaf + '.')
}
if ([string]$publicManifest.sha256 -ne $verified.Sha256) {
    throw 'The public feed SHA-256 does not match the built installer.'
}

$installerUri = New-Object System.Uri([Uri]$feedUrl, [string]$publicManifest.url)
$publicSetup = Join-Path $temp 'public-setup.exe'
Write-Step ('Downloading public installer: ' + $installerUri.AbsoluteUri)
Invoke-WebRequest -Uri $installerUri.AbsoluteUri -OutFile $publicSetup -UseBasicParsing -Headers @{
    'Cache-Control' = 'no-cache'
    'Pragma' = 'no-cache'
}
$publicHash = Get-Sha256 $publicSetup
if ($publicHash -ne $verified.Sha256) {
    throw 'The public installer hash does not match the built installer.'
}
$publicSize = (Get-Item -LiteralPath $publicSetup).Length
if ($publicSize -ne $verified.Size) {
    throw 'The public installer size does not match the built installer.'
}

$pageUrl = 'https://github.com/' + $repo + '/releases/latest'
$page = Invoke-WebRequest -Uri $pageUrl -UseBasicParsing -MaximumRedirection 5 -Headers @{
    'Cache-Control' = 'no-cache'
}
if ($page.StatusCode -ne 200) { throw 'The public latest-release page did not return HTTP 200.' }

Remove-Item -LiteralPath $temp -Recurse -Force -ErrorAction SilentlyContinue
Write-Host ''
Write-Host ('Released Qalam ' + $Version)
Write-Host ('Page:   ' + $pageUrl)
Write-Host ('Feed:   ' + $feedUrl)
Write-Host ('Setup:  ' + $installerUri.AbsoluteUri)
Write-Host ('SHA256: ' + $verified.Sha256)
Write-Host 'The public feed and installer were downloaded and verified.'
