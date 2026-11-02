$ErrorActionPreference = 'Stop'
[Console]::OutputEncoding = [System.Text.Encoding]::UTF8

Add-Type -AssemblyName PresentationFramework
$exe = (Resolve-Path (Join-Path $PSScriptRoot '..\dist\Qalam.exe')).Path
$asm = [Reflection.Assembly]::LoadFrom($exe)
$type = $asm.GetType('Qalam.ContextService')
$service = [Activator]::CreateInstance($type)
$method = $type.GetMethod('GetExamplesAsync')

$studentArabic = -join @([char]0x0637, [char]0x0627, [char]0x0644, [char]0x0628)
$bookArabic = -join @([char]0x0643, [char]0x062a, [char]0x0627, [char]0x0628)

$queries = @(
    @('student', 'en', 'ar'),
    @('school', 'en', 'ar'),
    @('book', 'en', 'ar'),
    @('peace', 'en', 'ar'),
    @('knowledge', 'en', 'ar'),
    @('The student wrote the lesson at school this morning.', 'en', 'ar'),
    @($studentArabic, 'ar', 'en'),
    @($bookArabic, 'ar', 'en')
)

foreach ($query in $queries) {
    $task = $method.Invoke($service, [object[]]@($query[0], $query[1], $query[2]))
    $task.Wait()
    $pairs = $task.Result
    Write-Output ('QUERY {0} [{1}->{2}] count={3}' -f $query[0], $query[1], $query[2], $pairs.Count)
    foreach ($pair in $pairs) {
        Write-Output ('  S: ' + $pair.Source)
        Write-Output ('  T: ' + $pair.Target)
    }
}
