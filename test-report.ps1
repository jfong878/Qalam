$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$server = Start-Process -FilePath (Join-Path $root 'dist\Qalam.exe') -WorkingDirectory $root -ArgumentList @('--mobileserve','--port','8791','--token','reporttest','--out','downloads\report-url.txt') -RedirectStandardOutput (Join-Path $root 'downloads\report-serve.log') -RedirectStandardError (Join-Path $root 'downloads\report-serve.err') -PassThru
try {
    Start-Sleep -Seconds 3
    $body = @{
        details = "Automated check of the local bug-report flow."
        steps = "1. Open Report a bug.`n2. Enter details.`n3. Save the report."
        expected = "A local Markdown report is created."
    } | ConvertTo-Json
    $response = Invoke-RestMethod -Uri 'http://127.0.0.1:8791/api/report' -Method Post -Headers @{ 'X-Qalam-Token' = 'reporttest' } -ContentType 'application/json' -Body $body -TimeoutSec 30
    if (-not $response.path -or -not (Test-Path -LiteralPath $response.path)) {
        throw 'report file was not created'
    }
    if ($response.report -notmatch 'Automated check of the local bug-report flow' -or
        $response.report -notmatch 'Version:') {
        throw 'report content is incomplete'
    }
    Write-Output ('report_path=' + $response.path)
    Write-Output ('report_bytes=' + (Get-Item -LiteralPath $response.path).Length)
    Write-Output 'report_api_ok'
    Remove-Item -LiteralPath $response.path -Force
}
catch {
    Write-Output ('report_test_error=' + $_.Exception.Message)
    if (Test-Path (Join-Path $root 'downloads\report-serve.err')) {
        Get-Content (Join-Path $root 'downloads\report-serve.err') | Select-Object -Last 20
    }
    exit 1
}
finally {
    try { Stop-Process -Id $server.Id -Force } catch { }
}
