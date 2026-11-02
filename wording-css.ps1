$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot 'mobile\app.css'
$css = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
if ($css.IndexOf('.search-actions') -lt 0) {
    $css = $css.TrimEnd() + @'

.search-actions {
  display: flex;
  align-items: center;
  gap: 8px;
}
#wordingText {
  font-size: clamp(20px, 3vw, 28px);
  line-height: 1.5;
}
@media (max-width: 720px) {
  .search-footer { gap: 8px; }
  .search-actions { gap: 6px; }
  .search-actions .ghost { padding: 0 9px; font-size: 10.5px; }
  .search-actions .primary.translate { min-width: 98px; }
}
'@
    [IO.File]::WriteAllText($path, $css, (New-Object Text.UTF8Encoding($false)))
    Write-Host 'added wording styles'
}
