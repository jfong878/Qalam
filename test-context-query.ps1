$ErrorActionPreference = 'Stop'
$assembly = [Reflection.Assembly]::LoadFrom((Join-Path $PSScriptRoot 'dist\Qalam.exe'))
$type = $assembly.GetTypes() | Where-Object { $_.Name -eq 'ContextService' } | Select-Object -First 1
if ($null -eq $type) { throw 'ContextService type not found' }
$flags = [Reflection.BindingFlags]'NonPublic,Static'
$buildTerms = $type.GetMethod('BuildTerms', $flags)
$buildQuery = $type.GetMethod('BuildQuery', $flags)
if ($null -eq $buildTerms -or $null -eq $buildQuery) { throw 'context query helpers not found' }

$sentence = 'Es un idioma critico en California.'
$terms = @($buildTerms.Invoke($null, @($sentence, 'es')))
$query = [string]$buildQuery.Invoke($null, @($sentence))
$terms | ForEach-Object { Write-Output ('term=' + $_) }
Write-Output ('query=' + $query)
$singleWord = @($terms | Where-Object { $_.Trim() -match '^[^\s]+$' -and $_.Trim() -ieq 'California' }).Count
if ($singleWord -ne 0) { throw 'single-word California fallback still present' }
if ($terms.Count -ne 1 -or $terms[0] -notmatch 'idioma critico en California') {
    throw 'full sentence was not preserved as the context term'
}
if ($query -notmatch 'idioma critico en California') {
    throw 'Tatoeba query was reduced away from the sentence'
}
Write-Output 'context_query_ok'
