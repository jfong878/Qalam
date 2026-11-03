# Builds Qalam with the .NET Framework compiler that ships with Windows.
# No SDK, NuGet, or network access required for the build itself.

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$wpf = Join-Path $framework 'WPF'
$csc = Join-Path $framework 'csc.exe'

if (-not (Test-Path -LiteralPath $csc)) {
    throw "In-box .NET Framework compiler not found: $csc"
}

$outDir = Join-Path $root 'dist'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$exe = Join-Path $outDir 'Qalam.exe'
$icon = Join-Path $root 'assets\qalam.ico'
if (-not (Test-Path -LiteralPath $icon)) { throw "Icon missing: $icon" }
if (Test-Path -LiteralPath $exe) { Remove-Item -LiteralPath $exe -Force }

$cscArgs = @(
    '/nologo'
    '/target:winexe'
    '/platform:anycpu'
    '/optimize+'
    '/codepage:65001'
    "/out:`"$exe`""
    "/win32icon:`"$icon`""
    "/r:`"$framework\System.dll`""
    "/r:`"$framework\System.Core.dll`""
    "/r:`"$framework\System.Drawing.dll`""
    "/r:`"$framework\System.Windows.Forms.dll`""
    "/r:`"$framework\System.Web.Extensions.dll`""
    "/r:`"$framework\System.Net.Http.dll`""
    "/r:`"$framework\System.Runtime.Serialization.dll`""
    "/r:`"$framework\System.Xaml.dll`""
    "/r:`"$framework\System.IO.Compression.dll`""
    "/r:`"$framework\System.IO.Compression.FileSystem.dll`""
    "/r:`"$framework\System.Xml.dll`""
    "/r:`"$framework\System.Xml.Linq.dll`""
    "/r:`"$wpf\PresentationFramework.dll`""
    "/r:`"$wpf\PresentationCore.dll`""
    "/r:`"$wpf\WindowsBase.dll`""
    "/r:`"$wpf\UIAutomationClient.dll`""
    "/r:`"$wpf\UIAutomationTypes.dll`""
    (Join-Path $root 'src\Qalam.cs')
    (Join-Path $root 'src\BugReport.cs')
    (Join-Path $root 'src\BugReportWindow.cs')
    (Join-Path $root 'src\Theme.cs')
    (Join-Path $root 'src\LanguageCatalog.cs')
    (Join-Path $root 'src\Colloquial.cs')
    (Join-Path $root 'src\LocalModel.cs')
    (Join-Path $root 'src\Grammar.cs')
    (Join-Path $root 'src\Concordance.cs')
    (Join-Path $root 'src\DiskConcordance.cs')
    (Join-Path $root 'src\Register.cs')
    (Join-Path $root 'src\TranslationCompare.cs')
    (Join-Path $root 'src\SingleInstance.cs')
    (Join-Path $root 'src\MainWindow.cs')
    (Join-Path $root 'src\Documents.cs')
    (Join-Path $root 'src\PdfDocuments.cs')
    (Join-Path $root 'src\DocumentTranslationService.cs')
    (Join-Path $root 'src\DocumentWindow.cs')
    (Join-Path $root 'src\DocumentSelfTest.cs')
    (Join-Path $root 'src\Characters.cs')
    (Join-Path $root 'src\CharacterWindow.cs')
    (Join-Path $root 'src\Study.cs')
    (Join-Path $root 'src\StudyWindow.cs')
    (Join-Path $root 'src\Mobile.cs')
    (Join-Path $root 'src\Updates.cs')
    (Join-Path $root 'src\AssemblyInfo.cs')
)

& $csc @cscArgs
if ($LASTEXITCODE -ne 0) {
    throw "csc failed with exit code $LASTEXITCODE"
}

Write-Host "Built $exe"
