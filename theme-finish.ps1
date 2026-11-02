$ErrorActionPreference = 'Stop'

$cssPath = Join-Path $PSScriptRoot 'mobile\app.css'
$css = [IO.File]::ReadAllText($cssPath, [Text.Encoding]::UTF8)
if ($css.IndexOf(':root[data-theme="dark"]') -lt 0) {
    $block = @'

/* Theme palette. The top-of-file values remain the light defaults. */
:root {
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
}

body {
  background:
    radial-gradient(780px 380px at 90% -10%, var(--gold-glow), transparent 68%),
    radial-gradient(620px 340px at -8% 28%, var(--teal-glow), transparent 70%),
    var(--bg);
}
.site-header { border-bottom-color: var(--header-line); background: var(--header-bg); }
.mark { background: var(--mark-bg); color: var(--mark-ink); }
.download-pill { border-color: var(--primary-bg); background: var(--primary-bg); color: var(--on-primary); }
.download-pill:hover { background: var(--primary-hover); }
.source-box { background: var(--input-bg); }
.source-box:focus-within, input:focus, select:focus {
  border-color: var(--focus-border);
  box-shadow: 0 0 0 3px var(--focus-glow);
}
.source-box textarea::placeholder { color: var(--placeholder); }
button:focus-visible, a:focus-visible, input:focus-visible, textarea:focus-visible, select:focus-visible {
  outline-color: var(--focus-ring);
}
button.primary {
  background: var(--primary-bg);
  border-color: var(--primary-bg);
  color: var(--on-primary);
  box-shadow: 0 6px 18px var(--window-shadow);
}
button.primary:hover { background: var(--primary-hover); }
.empty { background: var(--empty-bg); }
input, select { background: var(--input-bg); color: var(--ink); }
.windows-card {
  background: var(--window-bg);
  color: var(--window-text);
  box-shadow: 0 20px 40px var(--window-shadow);
}
.win-badge, .windows-card .eyebrow, .win-list li::before { color: var(--window-gold); }
.windows-card p { color: var(--window-muted); }
.download-main { background: var(--window-button-bg); color: var(--window-button-ink); }
.download-main:hover { background: var(--window-button-hover); }
.download-meta { color: var(--window-faint) !important; }
.install-web { color: var(--window-gold); }
.win-list { border-top-color: var(--window-line); }
.win-list li { color: var(--window-muted); }

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
    [IO.File]::WriteAllText($cssPath, $css.TrimEnd() + "`r`n" + $block, (New-Object Text.UTF8Encoding($false)))
    Write-Host 'appended mobile theme palette'
}

$swPath = Join-Path $PSScriptRoot 'mobile\sw.js'
$sw = [IO.File]::ReadAllText($swPath, [Text.Encoding]::UTF8)
if ($sw.IndexOf('qalam-shell-v3') -ge 0) {
    $sw = $sw.Replace('qalam-shell-v3', 'qalam-shell-v4')
    [IO.File]::WriteAllText($swPath, $sw, (New-Object Text.UTF8Encoding($false)))
    Write-Host 'bumped service-worker cache'
}

$mobilePath = Join-Path $PSScriptRoot 'src\Mobile.cs'
$mobile = [IO.File]::ReadAllText($mobilePath, [Text.Encoding]::UTF8)
$old = @'
                string html = Get(client, root + "/?token=" + server.Token);
                Check(log, "html", html.IndexOf("Qalam", StringComparison.Ordinal) >= 0 && html.IndexOf("app.js", StringComparison.Ordinal) >= 0);
'@
$new = @'
                string html = Get(client, root + "/?token=" + server.Token);
                Check(log, "html", html.IndexOf("Qalam", StringComparison.Ordinal) >= 0 && html.IndexOf("app.js", StringComparison.Ordinal) >= 0);
                string css = Get(client, root + "/app.css");
                Check(log, "theme_controls", html.IndexOf("themeSelect", StringComparison.Ordinal) >= 0 &&
                      html.IndexOf(">Sepia<", StringComparison.Ordinal) >= 0 &&
                      css.IndexOf("[data-theme=\"sepia\"]", StringComparison.Ordinal) >= 0 &&
                      css.IndexOf("[data-theme=\"dark\"]", StringComparison.Ordinal) >= 0);
'@
if ($mobile.IndexOf($old) -lt 0) { throw 'Mobile.cs test checkpoint not found' }
$mobile = $mobile.Replace($old, $new)
[IO.File]::WriteAllText($mobilePath, $mobile, (New-Object Text.UTF8Encoding($false)))
Write-Host 'added mobile theme test'
