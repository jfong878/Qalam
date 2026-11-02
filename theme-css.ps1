$ErrorActionPreference = 'Stop'
$path = Join-Path $PSScriptRoot 'mobile\app.css'
$css = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)

function Replace-Css([string]$old, [string]$new, [string]$label) {
    $count = ([regex]::Matches($script:css, [regex]::Escape($old))).Count
    if ($count -ne 1) { throw "Expected one '$label', found $count" }
    $script:css = $script:css.Replace($old, $new)
}

Replace-Css @'
  --safe-bottom: env(safe-area-inset-bottom, 0px);
}
'@ @'
  --safe-bottom: env(safe-area-inset-bottom, 0px);
  --input-bg: #fffefa;
  --placeholder: #9ba5a2;
  --on-primary: #fffdf8;
  --primary-bg: #093f38;
  --primary-hover: #062f2a;
  --mark-bg: #102b32;
  --mark-ink: #f2d58f;
  --header-bg: rgba(255, 253, 248, 0.92);
  --header-line: rgba(196, 206, 199, 0.72);
  --focus-ring: rgba(15, 107, 92, 0.28);
  --focus-border: rgba(15, 107, 92, 0.6);
  --focus-glow: rgba(15, 107, 92, 0.08);
  --empty-bg: rgba(255, 253, 248, 0.55);
  --gold-glow: rgba(185, 134, 50, 0.10);
  --teal-glow: rgba(15, 107, 92, 0.07);
  --window-bg: #093f38;
  --window-text: #f7f3e8;
  --window-muted: #c9d8d1;
  --window-faint: #a9c0b7;
  --window-gold: #f2d58f;
  --window-button-bg: #f2d58f;
  --window-button-hover: #f7e4b2;
  --window-button-ink: #17362f;
  --window-line: rgba(255, 255, 255, 0.12);
  --window-shadow: rgba(9, 63, 56, 0.18);
  --card-shadow: rgba(19, 45, 48, 0.08);
  --card-shadow-soft: rgba(19, 45, 48, 0.05);
}
'@ 'tokens'

Replace-Css @'
    radial-gradient(780px 380px at 90% -10%, rgba(185, 134, 50, 0.10), transparent 68%),
    radial-gradient(620px 340px at -8% 28%, rgba(15, 107, 92, 0.07), transparent 70%),
'@ @'
    radial-gradient(780px 380px at 90% -10%, var(--gold-glow), transparent 68%),
    radial-gradient(620px 340px at -8% 28%, var(--teal-glow), transparent 70%),
'@ 'body glows'

Replace-Css '  outline: 3px solid rgba(15, 107, 92, 0.28);' '  outline: 3px solid var(--focus-ring);' 'focus outline'
Replace-Css '  border-bottom: 1px solid rgba(196, 206, 199, 0.72);' '  border-bottom: 1px solid var(--header-line);' 'header line'
Replace-Css '  background: rgba(255, 253, 248, 0.92);' '  background: var(--header-bg);' 'header background'
Replace-Css @'
  background: var(--ink);
  color: #f2d58f;
'@ @'
  background: var(--mark-bg);
  color: var(--mark-ink);
'@ 'mark'

Replace-Css @'
  border: 1px solid var(--teal-deep);
  border-radius: 9px;
  background: var(--teal-deep);
  color: #fffdf8;
'@ @'
  border: 1px solid var(--primary-bg);
  border-radius: 9px;
  background: var(--primary-bg);
  color: var(--on-primary);
'@ 'download pill'
Replace-Css '.download-pill:hover { background: #062f2a; }' '.download-pill:hover { background: var(--primary-hover); }' 'download hover'

Replace-Css '.source-box { margin-top: 14px; border: 1px solid var(--line); border-radius: 12px; background: #fffefa; transition: border-color 120ms, box-shadow 120ms; }' '.source-box { margin-top: 14px; border: 1px solid var(--line); border-radius: 12px; background: var(--input-bg); transition: border-color 120ms, box-shadow 120ms; }' 'source box'
Replace-Css '.source-box:focus-within { border-color: rgba(15, 107, 92, 0.6); box-shadow: 0 0 0 3px rgba(15, 107, 92, 0.08); }' '.source-box:focus-within { border-color: var(--focus-border); box-shadow: 0 0 0 3px var(--focus-glow); }' 'source focus'
Replace-Css '.source-box textarea::placeholder { color: #9ba5a2; }' '.source-box textarea::placeholder { color: var(--placeholder); }' 'placeholder'

Replace-Css 'button.primary { background: var(--teal-deep); color: #fffdf8; border-color: var(--teal-deep); box-shadow: 0 6px 18px rgba(9, 63, 56, 0.14); }' 'button.primary { background: var(--primary-bg); color: var(--on-primary); border-color: var(--primary-bg); box-shadow: 0 6px 18px var(--window-shadow); }' 'primary button'
Replace-Css 'button.primary:hover { background: #062f2a; }' 'button.primary:hover { background: var(--primary-hover); }' 'primary hover'
Replace-Css '.empty { margin-top: 16px; border: 1px dashed var(--line-strong); border-radius: 14px; padding: 26px; text-align: center; color: var(--muted); background: rgba(255, 253, 248, 0.55); }' '.empty { margin-top: 16px; border: 1px dashed var(--line-strong); border-radius: 14px; padding: 26px; text-align: center; color: var(--muted); background: var(--empty-bg); }' 'empty'

Replace-Css @'
  background: #fffefa;
  color: var(--ink);
'@ @'
  background: var(--input-bg);
  color: var(--ink);
'@ 'input background'
Replace-Css 'input:focus, select:focus { border-color: rgba(15, 107, 92, 0.6); box-shadow: 0 0 0 3px rgba(15, 107, 92, 0.07); }' 'input:focus, select:focus { border-color: var(--focus-border); box-shadow: 0 0 0 3px var(--focus-glow); }' 'input focus'

Replace-Css @'
  background: var(--teal-deep);
  color: #f7f3e8;
  box-shadow: 0 20px 40px rgba(9, 63, 56, 0.18);
'@ @'
  background: var(--window-bg);
  color: var(--window-text);
  box-shadow: 0 20px 40px var(--window-shadow);
'@ 'windows card'
Replace-Css '.win-badge { width: 27px; height: 27px; display: grid; place-items: center; border: 1px solid rgba(242, 213, 143, 0.42); border-radius: 7px; color: #f2d58f; font-family: Tahoma, sans-serif; font-size: 15px; }' '.win-badge { width: 27px; height: 27px; display: grid; place-items: center; border: 1px solid rgba(242, 213, 143, 0.42); border-radius: 7px; color: var(--window-gold); font-family: Tahoma, sans-serif; font-size: 15px; }' 'win badge'
Replace-Css '.windows-card .eyebrow { color: #f2d58f; }' '.windows-card .eyebrow { color: var(--window-gold); }' 'windows eyebrow'
Replace-Css '  color: #c9d8d1; font-size: 12px;' '  color: var(--window-muted); font-size: 12px;' 'windows paragraph'
Replace-Css @'
  background: #f2d58f;
  color: #17362f;
'@ @'
  background: var(--window-button-bg);
  color: var(--window-button-ink);
'@ 'download main'
Replace-Css '.download-main:hover { background: #f7e4b2; }' '.download-main:hover { background: var(--window-button-hover); }' 'download main hover'
Replace-Css '.download-meta { margin-top: 9px !important; color: #a9c0b7 !important; font-size: 10px !important; }' '.download-meta { margin-top: 9px !important; color: var(--window-faint) !important; font-size: 10px !important; }' 'download meta'
Replace-Css @'
  border: 1px solid rgba(242, 213, 143, 0.38);
  border-radius: 8px;
  background: transparent;
  color: #f2d58f;
'@ @'
  border: 1px solid rgba(242, 213, 143, 0.38);
  border-radius: 8px;
  background: transparent;
  color: var(--window-gold);
'@ 'install button'
Replace-Css '  border-top: 1px solid rgba(255, 255, 255, 0.12); list-style: none;' '  border-top: 1px solid var(--window-line); list-style: none;' 'win list line'
Replace-Css '.win-list li { color: #dbe6df; font-size: 10.5px;' '.win-list li { color: var(--window-muted); font-size: 10.5px;' 'win list text'
Replace-Css '.win-list li::before { content: "·"; color: #f2d58f;' '.win-list li::before { content: "·"; color: var(--window-gold);' 'win list bullet'

$extra = @'

select.theme-select {
  width: auto;
  min-height: 36px;
  max-width: 112px;
  border: 1px solid var(--line-strong);
  border-radius: 8px;
  background-color: var(--paper);
  background-image: linear-gradient(45deg, transparent 50%, var(--muted) 50%), linear-gradient(135deg, var(--muted) 50%, transparent 50%);
  background-position: calc(100% - 14px) 50%, calc(100% - 9px) 50%;
  background-size: 5px 5px, 5px 5px;
  background-repeat: no-repeat;
  color: var(--ink-soft);
  padding: 0 26px 0 9px;
  font-size: 11px;
  font-weight: 650;
  cursor: pointer;
}

:root[data-theme="sepia"] {
  color-scheme: light;
  --bg: #efe2cb;
  --paper: #fbf2df;
  --paper-2: #f4e8d2;
  --ink: #3b2b1d;
  --ink-soft: #5b4632;
  --muted: #7a634b;
  --faint: #927a5e;
  --line: #d8c4a4;
  --line-strong: #c2a982;
  --teal: #2f6b57;
  --teal-deep: #3c5a4a;
  --teal-wash: #eadcc4;
  --gold: #956322;
  --blue: #315f8a;
  --danger: #a64a36;
  --ok: #3f7456;
  --shadow: 0 18px 50px rgba(76, 52, 24, 0.10);
  --input-bg: #fff8e8;
  --placeholder: #9b856c;
  --on-primary: #fffaf0;
  --primary-bg: #2f4b40;
  --primary-hover: #243d34;
  --mark-bg: #28473d;
  --mark-ink: #f2d58f;
  --header-bg: rgba(251, 242, 223, 0.94);
  --header-line: rgba(194, 169, 130, 0.72);
  --focus-ring: rgba(47, 107, 87, 0.30);
  --focus-border: rgba(47, 107, 87, 0.62);
  --focus-glow: rgba(47, 107, 87, 0.08);
  --empty-bg: rgba(251, 242, 223, 0.55);
  --gold-glow: rgba(149, 99, 34, 0.11);
  --teal-glow: rgba(47, 107, 87, 0.07);
  --window-bg: #2f4b40;
  --window-text: #f7f3e8;
  --window-muted: #d2dfd6;
  --window-faint: #b8c9bf;
  --window-gold: #f2d58f;
  --window-button-bg: #f2d58f;
  --window-button-hover: #f7e4b2;
  --window-button-ink: #17362f;
  --window-line: rgba(255, 255, 255, 0.14);
  --window-shadow: rgba(47, 75, 64, 0.22);
  --card-shadow: rgba(76, 52, 24, 0.09);
  --card-shadow-soft: rgba(76, 52, 24, 0.05);
}

:root[data-theme="dark"] {
  color-scheme: dark;
  --bg: #0e1518;
  --paper: #141d20;
  --paper-2: #1a2427;
  --ink: #edf3ef;
  --ink-soft: #cfdbd5;
  --muted: #9caca6;
  --faint: #7d8d88;
  --line: #29383a;
  --line-strong: #3a4a4b;
  --teal: #67cdb6;
  --teal-deep: #a6e3d2;
  --teal-wash: #172a27;
  --gold: #d9b36a;
  --blue: #83b6e8;
  --danger: #ef8f81;
  --ok: #68c79f;
  --shadow: 0 18px 50px rgba(0, 0, 0, 0.32);
  --input-bg: #10191c;
  --placeholder: #6f807b;
  --on-primary: #f8fffb;
  --primary-bg: #146459;
  --primary-hover: #0f5148;
  --mark-bg: #0b3f3a;
  --mark-ink: #f2d58f;
  --header-bg: rgba(14, 21, 24, 0.92);
  --header-line: rgba(58, 74, 75, 0.72);
  --focus-ring: rgba(103, 205, 182, 0.35);
  --focus-border: rgba(103, 205, 182, 0.65);
  --focus-glow: rgba(103, 205, 182, 0.10);
  --empty-bg: rgba(20, 29, 32, 0.55);
  --gold-glow: rgba(217, 179, 106, 0.08);
  --teal-glow: rgba(103, 205, 182, 0.06);
  --window-bg: #103e36;
  --window-text: #f4f8f6;
  --window-muted: #c7d8d0;
  --window-faint: #a9c0b7;
  --window-gold: #f2d58f;
  --window-button-bg: #f2d58f;
  --window-button-hover: #f7e4b2;
  --window-button-ink: #17362f;
  --window-line: rgba(255, 255, 255, 0.13);
  --window-shadow: rgba(0, 0, 0, 0.35);
  --card-shadow: rgba(0, 0, 0, 0.30);
  --card-shadow-soft: rgba(0, 0, 0, 0.18);
}

@media (max-width: 720px) {
  select.theme-select {
    min-height: 34px;
    max-width: 84px;
    padding: 0 23px 0 8px;
    font-size: 10px;
  }
}
'@
$css = $css.TrimEnd() + "`r`n" + $extra
[IO.File]::WriteAllText($path, $css, (New-Object Text.UTF8Encoding($false)))
Write-Host 'Mobile CSS theme patch applied'
