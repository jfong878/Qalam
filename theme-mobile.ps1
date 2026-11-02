$ErrorActionPreference = 'Stop'

function Patch-File([string]$relative, [scriptblock]$edit) {
    $path = Join-Path $PSScriptRoot $relative
    $text = [IO.File]::ReadAllText($path, [Text.Encoding]::UTF8)
    foreach ($pair in $edit.Invoke()) {
        $old = $pair[0]
        $new = $pair[1]
        $count = ([regex]::Matches($text, [regex]::Escape($old))).Count
        if ($count -ne 1) { throw "$relative expected one replacement, found $count for: $($old.Substring(0, [Math]::Min(60, $old.Length)))" }
        $text = $text.Replace($old, $new)
    }
    [IO.File]::WriteAllText($path, $text, (New-Object Text.UTF8Encoding($false)))
    Write-Host "patched $relative"
}

Patch-File 'mobile\index.html' {
    @(
        @('  <meta name="color-scheme" content="light">', '  <meta name="color-scheme" content="light dark">'),
        @('  <link rel="stylesheet" href="/app.css">', @'
  <script>
    (function () {
      try {
        var choice = localStorage.getItem("qalam.theme") || "system";
        var dark = choice === "dark" || (choice === "system" && window.matchMedia("(prefers-color-scheme: dark)").matches);
        document.documentElement.dataset.theme = dark ? "dark" : (choice === "sepia" ? "sepia" : "light");
      } catch (error) { }
    })();
  </script>
  <link rel="stylesheet" href="/app.css">
'@),
        @('      <div class="header-actions">
        <span class="connection" id="connection"><i></i><span>local</span></span>', @'
      <div class="header-actions">
        <select id="themeSelect" class="theme-select" aria-label="Theme" title="Theme: System follows your device">
          <option value="system">System</option>
          <option value="light">Light</option>
          <option value="sepia">Sepia</option>
          <option value="dark">Dark</option>
        </select>
        <span class="connection" id="connection"><i></i><span>local</span></span>
'@)
    )
}

Patch-File 'mobile\app.js' {
    @(
        @('  var installPrompt = null;', @'
  var installPrompt = null;
  var themeMedia = window.matchMedia ? window.matchMedia("(prefers-color-scheme: dark)") : null;
  var themeChoice = "system";
'@),
        @('  function $(id) { return document.getElementById(id); }', @'
  function $(id) { return document.getElementById(id); }

  function themeColor(theme) {
    if (theme === "dark") return "#0e1518";
    if (theme === "sepia") return "#efe2cb";
    return "#f4f1e8";
  }

  function applyTheme(choice, persist) {
    var allowed = ["system", "light", "sepia", "dark"];
    themeChoice = allowed.indexOf(choice) >= 0 ? choice : "system";
    var actual = themeChoice === "system"
      ? (themeMedia && themeMedia.matches ? "dark" : "light")
      : themeChoice;
    document.documentElement.setAttribute("data-theme", actual);
    document.documentElement.style.colorScheme = actual === "dark" ? "dark" : "light";
    var meta = document.querySelector('meta[name="theme-color"]');
    if (meta) meta.setAttribute("content", themeColor(actual));
    var select = $("themeSelect");
    if (select) select.value = themeChoice;
    if (persist) {
      try { localStorage.setItem("qalam.theme", themeChoice); } catch (error) { }
    }
  }

  function initTheme() {
    var saved = "system";
    try { saved = localStorage.getItem("qalam.theme") || "system"; } catch (error) { }
    applyTheme(saved, false);
    var select = $("themeSelect");
    if (select) {
      select.addEventListener("change", function () { applyTheme(select.value, true); });
    }
    if (themeMedia) {
      var onSystemTheme = function () {
        if (themeChoice === "system") applyTheme("system", false);
      };
      if (themeMedia.addEventListener) themeMedia.addEventListener("change", onSystemTheme);
      else if (themeMedia.addListener) themeMedia.addListener(onSystemTheme);
    }
  }
'@),
        @('  function init() {
    if (!token) {', @'
  function init() {
    initTheme();
    if (!token) {
'@)
    )
}

Patch-File 'mobile\app.css' {
    @(
        @('  --safe-bottom: env(safe-area-inset-bottom, 0px);
}', @'
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
'@),
        @('    radial-gradient(780px 380px at 90% -10%, rgba(185, 134, 50, 0.10), transparent 68%),
    radial-gradient(620px 340px at -8% 28%, rgba(15, 107, 92, 0.07), transparent 70%),', '    radial-gradient(780px 380px at 90% -10%, var(--gold-glow), transparent 68%),
    radial-gradient(620px 340px at -8% 28%, var(--teal-glow), transparent 70%),'),
        @('  outline: 3px solid rgba(15, 107, 92, 0.28);', '  outline: 3px solid var(--focus-ring);'),
        @('  border-bottom: 1px solid rgba(196, 206, 199, 0.72);', '  border-bottom: 1px solid var(--header-line);'),
        @('  background: rgba(255, 253, 248, 0.92);', '  background: var(--header-bg);'),
        @('  background: var(--ink);
  color: #f2d58f;', '  background: var(--mark-bg);
  color: var(--mark-ink);'),
        @('  border: 1px solid var(--teal-deep);
  border-radius: 9px;
  background: var(--teal-deep);
  color: #fffdf8;', '  border: 1px solid var(--primary-bg);
  border-radius: 9px;
  background: var(--primary-bg);
  color: var(--on-primary);'),
        @('.download-pill:hover { background: #062f2a; }', '.download-pill:hover { background: var(--primary-hover); }'),
        @('  background: #fffefa; transition: border-color 120ms, box-shadow 120ms; }', '  background: var(--input-bg); transition: border-color 120ms, box-shadow 120ms; }'),
        @('  border-color: rgba(15, 107, 92, 0.6); box-shadow: 0 0 0 3px rgba(15, 107, 92, 0.08); }', '  border-color: var(--focus-border); box-shadow: 0 0 0 3px var(--focus-glow); }'),
        @('  color: #9ba5a2; }', '  color: var(--placeholder); }'),
        @('button.primary { background: var(--teal-deep); color: #fffdf8; border-color: var(--teal-deep); box-shadow: 0 6px 18px rgba(9, 63, 56, 0.14); }\nbutton.primary:hover { background: #062f2a; }', 'button.primary { background: var(--primary-bg); color: var(--on-primary); border-color: var(--primary-bg); box-shadow: 0 6px 18px var(--window-shadow); }\nbutton.primary:hover { background: var(--primary-hover); }'),
        @('  background: rgba(255, 253, 248, 0.55); }', '  background: var(--empty-bg); }'),
        @('  background: #fffefa;\n  color: var(--ink);', '  background: var(--input-bg);\n  color: var(--ink);'),
        @('  border-color: rgba(15, 107, 92, 0.6); box-shadow: 0 0 0 3px rgba(15, 107, 92, 0.07); }', '  border-color: var(--focus-border); box-shadow: 0 0 0 3px var(--focus-glow); }'),
        @('  background: var(--teal-deep);
  color: #f7f3e8;
  box-shadow: 0 20px 40px rgba(9, 63, 56, 0.18);', '  background: var(--window-bg);
  color: var(--window-text);
  box-shadow: 0 20px 40px var(--window-shadow);'),
        @('  color: #f2d58f; font-size: 15px; }', '  color: var(--window-gold); font-size: 15px; }'),
        @('.windows-card .eyebrow { color: #f2d58f; }', '.windows-card .eyebrow { color: var(--window-gold); }'),
        @('  color: #c9d8d1; font-size: 12px;', '  color: var(--window-muted); font-size: 12px;'),
        @('  background: #f2d58f;\n  color: #17362f;', '  background: var(--window-button-bg);\n  color: var(--window-button-ink);'),
        @('.download-main:hover { background: #f7e4b2; }', '.download-main:hover { background: var(--window-button-hover); }'),
        @('.download-meta { margin-top: 9px !important; color: #a9c0b7 !important; font-size: 10px !important; }', '.download-meta { margin-top: 9px !important; color: var(--window-faint) !important; font-size: 10px !important; }'),
        @('  border: 1px solid rgba(242, 213, 143, 0.38);
  border-radius: 8px;
  background: transparent;
  color: #f2d58f;', '  border: 1px solid rgba(242, 213, 143, 0.38);
  border-radius: 8px;
  background: transparent;
  color: var(--window-gold);'),
        @('.install-web:hover { background: rgba(242, 213, 143, 0.08); }', '.install-web:hover { background: rgba(242, 213, 143, 0.08); }'),
        @('  border-top: 1px solid rgba(255, 255, 255, 0.12); list-style: none;', '  border-top: 1px solid var(--window-line); list-style: none;'),
        @('.win-list li { color: #dbe6df; font-size: 10.5px;', '.win-list li { color: var(--window-muted); font-size: 10.5px;'),
        @('.win-list li::before { content: "·"; color: #f2d58f;', '.win-list li::before { content: "·"; color: var(--window-gold);')
    )
}
