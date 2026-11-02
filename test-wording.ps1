$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$exe = Join-Path $root 'dist\Qalam-wording.exe'
$server = Start-Process -FilePath $exe -WorkingDirectory $root -ArgumentList @('--mobileserve','--port','8790','--token','wordingtest','--out','downloads\rewrite-url.txt') -RedirectStandardOutput (Join-Path $root 'downloads\rewrite-serve.log') -RedirectStandardError (Join-Path $root 'downloads\rewrite-serve.err') -PassThru
try {
    Start-Sleep -Seconds 3
    $body = @{
        text = "I has went to the shop yesterday and buyed two breads."
        language = "en"
        context = "A simple description of a shopping trip."
    } | ConvertTo-Json
    $headers = @{ 'X-Qalam-Token' = 'wordingtest' }
    $response = Invoke-RestMethod -Uri 'http://127.0.0.1:8790/api/rewrite' -Method Post -Headers $headers -ContentType 'application/json' -Body $body -TimeoutSec 120
    $response | ConvertTo-Json -Depth 5
}
catch {
    Write-Output ("wording_test_error=" + $_.Exception.Message)
    if (Test-Path (Join-Path $root 'downloads\rewrite-serve.err')) {
        Get-Content (Join-Path $root 'downloads\rewrite-serve.err') | Select-Object -Last 20
    }
}
finally {
    try { Stop-Process -Id $server.Id -Force } catch { }
    Write-Output ("server_pid=" + $server.Id)
}
