[CmdletBinding()]
param(
    [string]$Version = "1.0.0",
    [string]$Notes = "",
    [string]$NotesFile = "",
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
. (Join-Path $root 'release-text.ps1')
$dist = Join-Path $root 'dist'
$stage = Join-Path $dist 'package-stage'
$package = Join-Path $dist 'Qalam-Windows.zip'
$nsis = $null

if (-not [string]::IsNullOrWhiteSpace($NotesFile)) {
    if (-not [string]::IsNullOrWhiteSpace($Notes)) {
        throw 'Pass either -Notes or -NotesFile, not both.'
    }
    $Notes = Read-QalamUtf8File -Path $NotesFile -Label 'Release notes'
}
Assert-QalamReleaseText -Text $Notes -Label 'Release notes' -AllowEmpty

if (-not $SkipBuild) {
    & (Join-Path $root 'build.ps1')
    if ($LASTEXITCODE -ne 0) { throw "Build failed with exit code $LASTEXITCODE" }
}

if (-not (Test-Path -LiteralPath (Join-Path $dist 'Qalam.exe'))) {
    throw "Build output missing: $(Join-Path $dist 'Qalam.exe')"
}

if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
New-Item -ItemType Directory -Force -Path $stage | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'dist') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'tashkeel') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'documents') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'mobile') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'data') | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $stage 'concordance') | Out-Null

Copy-Item -LiteralPath (Join-Path $dist 'Qalam.exe') -Destination (Join-Path $stage 'dist\Qalam.exe') -Force
Copy-Item -LiteralPath (Join-Path $root 'tashkeel\tashkeel_server.py') -Destination (Join-Path $stage 'tashkeel\tashkeel_server.py') -Force
Copy-Item -LiteralPath (Join-Path $root 'tashkeel\requirements.txt') -Destination (Join-Path $stage 'tashkeel\requirements.txt') -Force
Copy-Item -LiteralPath (Join-Path $root 'documents\pdf_translate.py') -Destination (Join-Path $stage 'documents\pdf_translate.py') -Force
Copy-Item -LiteralPath (Join-Path $root 'documents\requirements.txt') -Destination (Join-Path $stage 'documents\requirements.txt') -Force
Copy-Item -LiteralPath (Join-Path $root 'documents\ocr_windows.ps1') -Destination (Join-Path $stage 'documents\ocr_windows.ps1') -Force
Copy-Item -Path (Join-Path $root 'mobile\*') -Destination (Join-Path $stage 'mobile') -Recurse -Force
Copy-Item -LiteralPath (Join-Path $root 'install.ps1') -Destination (Join-Path $stage 'install.ps1') -Force
Copy-Item -LiteralPath (Join-Path $root 'uninstall.ps1') -Destination (Join-Path $stage 'uninstall.ps1') -Force
Copy-Item -LiteralPath (Join-Path $root 'update.ps1') -Destination (Join-Path $stage 'update.ps1') -Force
Copy-Item -LiteralPath (Join-Path $root 'README.md') -Destination (Join-Path $stage 'README.md') -Force

Get-ChildItem -LiteralPath (Join-Path $root 'data') -File -Filter '*.tsv' | ForEach-Object {
    Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $stage ('data\' + $_.Name)) -Force
}
# Ship the review-sized seed packs. The 100k/depth corpora are installed
# separately; embedding them would make every update hundreds of megabytes.
$seedPacks = @(
    'ar-en-dialect.tsv',
    'ar-en.tsv',
    'en-ar-dialect.tsv',
    'en-ar.tsv',
    'en-es-slang.tsv',
    'en-es.tsv',
    'es-en-slang.tsv',
    'es-en.tsv'
)
foreach ($name in $seedPacks) {
    $source = Join-Path (Join-Path $root 'concordance') $name
    if (Test-Path -LiteralPath $source) {
        $destination = Join-Path (Join-Path $stage 'concordance') $name
        Copy-Item -LiteralPath $source -Destination $destination -Force
    }
}

$cmd = "@echo off`r`npowershell -NoProfile -ExecutionPolicy Bypass -File `"%~dp0install.ps1`"`r`n"
[IO.File]::WriteAllText((Join-Path $stage 'Install Qalam.cmd'), $cmd, (New-Object System.Text.ASCIIEncoding))
[IO.File]::WriteAllText((Join-Path $stage 'PACKAGE.txt'), "Qalam $Version`r`n", (New-Object System.Text.UTF8Encoding($false)))

if (Test-Path -LiteralPath $package) { Remove-Item -LiteralPath $package -Force }
Compress-Archive -Path (Join-Path $stage '*') -DestinationPath $package -Force
if (-not (Test-Path -LiteralPath $package)) { throw 'Package ZIP was not created.' }

$nsisRoot = Join-Path $root '.tools'
$candidate = Get-ChildItem -Path $nsisRoot -Recurse -Filter 'makensis.exe' -ErrorAction SilentlyContinue | Select-Object -First 1
if ($candidate) {
    $nsis = $candidate.FullName
} else {
    New-Item -ItemType Directory -Force -Path $nsisRoot | Out-Null
    $nsisZip = Join-Path $nsisRoot 'nsis-3.11.zip'
    if (-not (Test-Path -LiteralPath $nsisZip)) {
        Invoke-WebRequest -Uri 'https://downloads.sourceforge.net/project/nsis/NSIS%203/3.11/nsis-3.11.zip' -OutFile $nsisZip
    }
    $expected = 'c7d27f780ddb6cffb4730138cd1591e841f4b7edb155856901cdf5f214394fa1'
    $actual = (Get-FileHash -LiteralPath $nsisZip -Algorithm SHA256).Hash.ToLowerInvariant()
    if ($actual -ne $expected) { throw 'NSIS download checksum mismatch.' }
    Expand-Archive -LiteralPath $nsisZip -DestinationPath (Join-Path $nsisRoot 'nsis') -Force
    $candidate = Get-ChildItem -Path (Join-Path $nsisRoot 'nsis') -Recurse -Filter 'makensis.exe' | Select-Object -First 1
    if (-not $candidate) { throw 'NSIS compiler was not found after extraction.' }
    $nsis = $candidate.FullName
}

$out = Join-Path $dist ("Qalam-Setup-$Version.exe")
$bootstrap = Join-Path $root 'installer\setup-bootstrap.ps1'
$nsi = Join-Path $root 'installer\Qalam.nsi'
$icon = Join-Path $root 'assets\qalam.ico'
if (Test-Path -LiteralPath $out) { Remove-Item -LiteralPath $out -Force }

& $nsis /V2 "/DVERSION=$Version" "/DPACKAGE_ZIP=$package" "/DBOOTSTRAP=$bootstrap" "/DOUTPUT=$out" "/DICON=$icon" $nsi
if ($LASTEXITCODE -ne 0) { throw "NSIS failed with exit code $LASTEXITCODE" }
if (-not (Test-Path -LiteralPath $out)) { throw 'Setup executable was not created.' }

$file = Get-Item -LiteralPath $out
$bytes = [IO.File]::ReadAllBytes($out)
$sha256 = (Get-FileHash -LiteralPath $out -Algorithm SHA256).Hash.ToLowerInvariant()
$sha512 = [Convert]::ToBase64String([Security.Cryptography.SHA512]::Create().ComputeHash($bytes))
$releaseDate = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ss.fffZ')
$leaf = $file.Name
$manifest = [ordered]@{
    version = $Version
    url = "https://github.com/jfong878/Qalam/releases/download/v$Version/$leaf"
    sha256 = $sha256
    size = $file.Length
    notes = $Notes
    mandatory = $false
}
$utf8 = New-Object System.Text.UTF8Encoding($false)
[IO.File]::WriteAllText((Join-Path $dist 'latest.json'), ($manifest | ConvertTo-Json), $utf8)
$null = Assert-QalamManifestNotes -Path (Join-Path $dist 'latest.json') -Expected $Notes -Label 'latest.json'
$yml = "version: $Version`r`nfiles:`r`n  - url: $leaf`r`n    sha512: $sha512`r`n    size: $($file.Length)`r`npath: $leaf`r`nsha512: $sha512`r`nreleaseDate: '$releaseDate'`r`n"
[IO.File]::WriteAllText((Join-Path $dist 'latest.yml'), $yml, $utf8)
$null = Read-QalamUtf8File -Path (Join-Path $dist 'latest.yml') -Label 'latest.yml'

Write-Host "Built $out"
Write-Host "SHA-256 $sha256"
Write-Host "Manifest $(Join-Path $dist 'latest.json')"
