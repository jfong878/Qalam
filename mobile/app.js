(function () {
  "use strict";

  var token = window.QALAM_TOKEN || new URLSearchParams(location.search).get("token") || "";
  var languages = [];
  var lastTranslation = null;
  var currentCard = null;
  var revealed = false;
  var studyScope = "due";
  var activeTab = "translate";
  var installPrompt = null;
  var themeMedia = window.matchMedia ? window.matchMedia("(prefers-color-scheme: dark)") : null;
  var themeChoice = "system";

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

  function showStatus(element, message, kind) {
    if (!element) return;
    element.textContent = message || "";
    element.className = "status" + (kind ? " " + kind : "");
  }

  function api(path, options) {
    options = options || {};
    var headers = { "X-Qalam-Token": token };
    if (options.body) headers["Content-Type"] = "application/json";
    return fetch(path, {
      method: options.method || "GET",
      headers: headers,
      body: options.body
    }).then(function (response) {
      return response.text().then(function (text) {
        var data;
        try { data = JSON.parse(text); }
        catch (error) { data = { error: text || ("HTTP " + response.status) }; }
        if (!response.ok) throw new Error(data.error || ("HTTP " + response.status));
        return data;
      });
    });
  }

  function populate(select, includeAuto) {
    if (!select) return;
    select.innerHTML = "";
    if (includeAuto) {
      var auto = document.createElement("option");
      auto.value = "auto";
      auto.textContent = "Auto (Arabic ↔ English)";
      select.appendChild(auto);
    }
    languages.forEach(function (language) {
      var option = document.createElement("option");
      option.value = language.code;
      option.textContent = language.name + " (" + language.code + ")";
      select.appendChild(option);
    });
  }

  function choose(select, code) {
    if (!select) return;
    for (var i = 0; i < select.options.length; i++) {
      if (select.options[i].value === code) { select.value = code; return; }
    }
  }

  function statsHtml(stats) {
    var values = [
      ["due", "due"], ["total", "cards"], ["reviewedToday", "today"], ["newCards", "new"]
    ];
    return values.map(function (pair) {
      return '<span class="stat"><strong>' + (stats[pair[0]] || 0) + "</strong>" + pair[1] + "</span>";
    }).join("");
  }

  function setBusy(button, busy) {
    if (!button) return;
    button.disabled = busy;
    button.setAttribute("aria-busy", busy ? "true" : "false");
  }

  function tab(name) {
    activeTab = name;
    document.querySelectorAll(".tab").forEach(function (button) {
      var selected = button.getAttribute("data-tab") === name;
      button.classList.toggle("active", selected);
      button.setAttribute("aria-selected", selected ? "true" : "false");
    });
    document.querySelectorAll(".panel").forEach(function (panel) {
      panel.classList.toggle("active", panel.id === "panel-" + name);
    });
    if (name === "study") loadStudy(studyScope);
  }

  function translate() {
    var input = $("source");
    var button = $("translateBtn");
    var text = input.value.trim();
    if (!text) {
      showStatus($("translateStatus"), "Type something first.", "error");
      input.focus();
      return;
    }
    setBusy(button, true);
    showStatus($("translateStatus"), "Translating…");
    api("/api/translate", {
      method: "POST",
      body: JSON.stringify({ text: text, target: $("target").value })
    }).then(function (data) {
      lastTranslation = {
        source: text,
        translation: data.translation,
        detected: data.detected,
        target: data.target,
        engine: data.engine
      };
      $("result").classList.remove("hidden");
      $("translation").textContent = data.translation;
      $("translation").setAttribute("dir", /[\u0600-\u06ff]/.test(data.translation) ? "rtl" : "auto");
      $("pair").textContent = String(data.detected || "?").toUpperCase() + "  →  " + String(data.target || "").toUpperCase();
      showStatus($("translateStatus"), data.engine ? "via " + data.engine : "", "good");
      loadContext(text, data.detected, data.target);
    }).catch(function (error) {
      showStatus($("translateStatus"), error.message, "error");
    }).then(function () {
      setBusy(button, false);
    });
  }

  function suggestWording() {
    var input = $("source");
    var text = input.value.trim();
    if (!text) {
      showStatus($("translateStatus"), "Type something first.", "error");
      input.focus();
      return;
    }
    var button = $("wordingBtn");
    setBusy(button, true);
    showStatus($("translateStatus"), "Looking for more natural wording…");
    var context = lastTranslation && lastTranslation.source === text ? lastTranslation.translation : "";
    var language = lastTranslation && lastTranslation.source === text ? lastTranslation.detected : "";
    api("/api/rewrite", {
      method: "POST",
      body: JSON.stringify({ text: text, language: language, context: context })
    }).then(function (data) {
      $("wordingText").textContent = data.suggestion || "";
      $("wordingText").setAttribute("dir", /[\u0600-\u06ff]/.test(data.suggestion || "") ? "rtl" : "auto");
      $("wordingCard").classList.remove("hidden");
      showStatus($("translateStatus"), "Suggested more natural wording.", "good");
    }).catch(function (error) {
      showStatus($("translateStatus"), error.message, "error");
    }).then(function () {
      setBusy(button, false);
    });
  }

  function useWording() {
    var suggestion = $("wordingText").textContent || "";
    if (!suggestion) return;
    $("source").value = suggestion;
    $("wordingCard").classList.add("hidden");
    showStatus($("translateStatus"), "Wording applied.", "good");
    $("source").focus();
  }

  function openReport() {
    $("reportDetails").value = "";
    $("reportSteps").value = "";
    $("reportExpected").value = "";
    $("reportResult").value = "";
    $("reportResultWrap").classList.add("hidden");
    showStatus($("reportStatus"), "");
    $("reportModal").classList.remove("hidden");
    $("reportDetails").focus();
  }

  function closeReport() {
    $("reportModal").classList.add("hidden");
  }

  function submitReport() {
    var details = $("reportDetails").value.trim();
    if (!details) {
      showStatus($("reportStatus"), "Describe what happened first.", "error");
      $("reportDetails").focus();
      return;
    }
    var button = $("reportSubmitBtn");
    setBusy(button, true);
    showStatus($("reportStatus"), "Saving report locally…");
    api("/api/report", {
      method: "POST",
      body: JSON.stringify({
        details: details,
        steps: $("reportSteps").value,
        expected: $("reportExpected").value
      })
    }).then(function (data) {
      $("reportResult").value = data.report || "";
      $("reportResultWrap").classList.remove("hidden");
      showStatus($("reportStatus"), "Saved on the Qalam computer. Nothing was sent automatically.", "good");
    }).catch(function (error) {
      showStatus($("reportStatus"), error.message, "error");
    }).then(function () {
      setBusy(button, false);
    });
  }

  function copyReport() {
    var text = $("reportResult").value || "";
    if (!text) return;
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(function () {
        showStatus($("reportStatus"), "Copied. Nothing was sent automatically.", "good");
      }).catch(function () { fallbackCopy(text, $("reportStatus")); });
    } else fallbackCopy(text, $("reportStatus"));
  }

  function copyResult() {
    if (!lastTranslation) return;
    var text = lastTranslation.translation;
    if (navigator.clipboard && navigator.clipboard.writeText) {
      navigator.clipboard.writeText(text).then(function () {
        showStatus($("translateStatus"), "Copied.", "good");
      }).catch(function () { fallbackCopy(text); });
    } else fallbackCopy(text);
  }

  function fallbackCopy(text, target) {
    var area = document.createElement("textarea");
    area.value = text;
    area.style.position = "fixed";
    area.style.opacity = "0";
    document.body.appendChild(area);
    area.select();
    try { document.execCommand("copy"); showStatus(target || $("translateStatus"), "Copied.", "good"); }
    catch (error) { showStatus(target || $("translateStatus"), "Copy failed.", "error"); }
    document.body.removeChild(area);
  }

  function speak() {
    if (!lastTranslation || !("speechSynthesis" in window)) return;
    window.speechSynthesis.cancel();
    var utterance = new SpeechSynthesisUtterance(lastTranslation.translation);
    var code = lastTranslation.target || lastTranslation.detected || "en";
    utterance.lang = code === "zh-CN" ? "zh-CN" : code === "zh-TW" ? "zh-TW" : code;
    window.speechSynthesis.speak(utterance);
  }

  function addToStudy() {
    if (!lastTranslation) return;
    setBusy($("addStudyBtn"), true);
    api("/api/study/add", {
      method: "POST",
      body: JSON.stringify({
        source: lastTranslation.source,
        translation: lastTranslation.translation,
        detected: lastTranslation.detected,
        target: lastTranslation.target
      })
    }).then(function (data) {
      showStatus($("translateStatus"), data.added ? "Added to study." : "Already in study.", "good");
    }).catch(function (error) {
      showStatus($("translateStatus"), error.message, "error");
    }).then(function () { setBusy($("addStudyBtn"), false); });
  }

  function exampleElement(example) {
    var card = document.createElement("article");
    card.className = "example";
    var source = document.createElement("p");
    source.className = "source";
    source.dir = /[\u0600-\u06ff]/.test(example.source || "") ? "rtl" : "auto";
    source.textContent = example.source || "";
    var target = document.createElement("p");
    target.className = "target";
    target.dir = /[\u0600-\u06ff]/.test(example.target || "") ? "rtl" : "auto";
    target.textContent = example.target || "";
    var origin = document.createElement("p");
    origin.className = "origin";
    origin.textContent = [example.origin, example.license].filter(Boolean).join("  ·  ") || "open corpus";
    card.appendChild(source);
    card.appendChild(target);
    card.appendChild(origin);
    return card;
  }

  function renderExamples(container, examples) {
    if (!container) return;
    container.innerHTML = "";
    (examples || []).forEach(function (example) {
      container.appendChild(exampleElement(example));
    });
  }

  function loadContext(query, from, to) {
    var panel = $("contextPanel");
    var results = $("contextResults");
    if (!panel || !results) return;
    results.innerHTML = "";
    if (!query || !from || !to || from === to) {
      panel.classList.add("hidden");
      return;
    }
    panel.classList.remove("hidden");
    showStatus($("contextStatus"), "Finding examples in the local corpus…");
    var url = "/api/corpus?query=" + encodeURIComponent(query) +
              "&from=" + encodeURIComponent(from) + "&to=" + encodeURIComponent(to);
    api(url).then(function (data) {
      var examples = data.examples || [];
      renderExamples(results, examples);
      showStatus($("contextStatus"), examples.length
        ? examples.length + " aligned example" + (examples.length === 1 ? "" : "s") + "."
        : "No local example for this phrase yet.",
        examples.length ? "good" : "");
    }).catch(function (error) {
      showStatus($("contextStatus"), error.message, "error");
    });
  }

  function renderStats(stats) {
    $("studyStats").innerHTML = statsHtml(stats || {});
  }

  function renderCard(card, index, total) {
    currentCard = card;
    revealed = false;
    $("studyCard").classList.remove("hidden");
    $("studyEmpty").classList.add("hidden");
    $("studyFront").textContent = card.source || "";
    $("studyFront").setAttribute("dir", /[\u0600-\u06ff]/.test(card.source || "") ? "rtl" : "auto");
    $("studyBack").textContent = card.translation || "";
    $("studyBack").setAttribute("dir", /[\u0600-\u06ff]/.test(card.translation || "") ? "rtl" : "auto");
    $("studyPair").textContent = String(card.detected || "?").toUpperCase() + "  →  " + String(card.target || "").toUpperCase();
    $("studyCounter").textContent = (index + 1) + " / " + total;
    $("studyBackWrap").classList.add("hidden");
    $("gradeRow").classList.add("hidden");
    $("revealBtn").classList.remove("hidden");
    showStatus($("studyStatus"), "Recall the meaning, then reveal.");
  }

  function renderStudyEmpty(stats, scope, reviewed) {
    currentCard = null;
    revealed = false;
    $("studyCard").classList.add("hidden");
    $("studyEmpty").classList.remove("hidden");
    $("reviewAhead").classList.toggle("hidden", !(stats.total > 0));
    if (scope === "all" && reviewed > 0) {
      $("studyEmptyTitle").textContent = "Session complete";
      $("studyEmptyBody").textContent = "You reviewed " + reviewed + " card" + (reviewed === 1 ? "" : "s") + ".";
    } else if (stats.total === 0) {
      $("studyEmptyTitle").textContent = "Your study deck is empty";
      $("studyEmptyBody").textContent = "Save a translation or add one from the Translate tab, then import saved items.";
    } else if (stats.due === 0) {
      $("studyEmptyTitle").textContent = "No cards due";
      $("studyEmptyBody").textContent = stats.total + " card" + (stats.total === 1 ? "" : "s") + " in the deck. Review ahead or come back later.";
    } else {
      $("studyEmptyTitle").textContent = "No cards due";
      $("studyEmptyBody").textContent = "Nothing is due right now.";
    }
  }

  function loadStudy(scope, reviewed) {
    studyScope = scope || "due";
    showStatus($("studyStatus"), "Loading deck…");
    api("/api/study?scope=" + encodeURIComponent(studyScope)).then(function (data) {
      renderStats(data.stats);
      if (!data.cards || data.cards.length === 0) {
        renderStudyEmpty(data.stats || {}, studyScope, reviewed || 0);
        return;
      }
      renderCard(data.cards[0], 0, data.cards.length);
    }).catch(function (error) {
      $("studyEmpty").classList.remove("hidden");
      $("studyCard").classList.add("hidden");
      $("studyEmptyTitle").textContent = "Study unavailable";
      $("studyEmptyBody").textContent = error.message;
      showStatus($("studyStatus"), error.message, "error");
    });
  }

  function reveal() {
    if (!currentCard) return;
    revealed = true;
    $("studyBackWrap").classList.remove("hidden");
    $("gradeRow").classList.remove("hidden");
    $("revealBtn").classList.add("hidden");
    showStatus($("studyStatus"), "How well did you recall it?");
  }

  function grade(value) {
    if (!currentCard || !revealed) return;
    var reviewed = currentCard;
    var buttons = $("gradeRow").querySelectorAll("button");
    buttons.forEach(function (button) { button.disabled = true; });
    api("/api/study/grade", {
      method: "POST",
      body: JSON.stringify({
        source: reviewed.source,
        target: reviewed.target,
        grade: value
      })
    }).then(function (data) {
      renderStats(data.stats);
      loadStudy(studyScope, 1);
    }).catch(function (error) {
      showStatus($("studyStatus"), error.message, "error");
      buttons.forEach(function (button) { button.disabled = false; });
    });
  }

  function importSaved() {
    setBusy($("importBtn"), true);
    api("/api/study/import", { method: "POST" }).then(function (data) {
      showStatus($("studyStatus"), data.added ? "Imported " + data.added + " card" + (data.added === 1 ? "" : "s") + "." : "Everything saved is already in the deck.", "good");
      loadStudy("due");
    }).catch(function (error) {
      showStatus($("studyStatus"), error.message, "error");
    }).then(function () { setBusy($("importBtn"), false); });
  }

  function searchCorpus() {
    var query = $("corpusQuery").value.trim();
    var from = $("corpusFrom").value;
    var to = $("corpusTo").value;
    if (!query) { showStatus($("corpusStatus"), "Type a word or phrase.", "error"); return; }
    if (from === to) { showStatus($("corpusStatus"), "Choose two different languages.", "error"); return; }
    setBusy($("corpusBtn"), true);
    showStatus($("corpusStatus"), "Searching the local corpus…");
    var url = "/api/corpus?query=" + encodeURIComponent(query) + "&from=" + encodeURIComponent(from) + "&to=" + encodeURIComponent(to);
    api(url).then(function (data) {
      var examples = data.examples || [];
      renderExamples($("corpusResults"), examples);
      showStatus($("corpusStatus"), examples.length
        ? examples.length + " example" + (examples.length === 1 ? "" : "s") + "."
        : "No local example found.",
        examples.length ? "good" : "");
    }).catch(function (error) {
      showStatus($("corpusStatus"), error.message, "error");
    }).then(function () { setBusy($("corpusBtn"), false); });
  }

  function useSample(button) {
    $("source").value = button.getAttribute("data-text") || button.textContent;
    $("source").focus();
    showStatus($("translateStatus"), "");
  }

  function swapTarget() {
    var select = $("target");
    if (!select) return;
    var from = lastTranslation && lastTranslation.detected ? lastTranslation.detected : "";
    var next = from && from !== select.value ? from : (select.value === "ar" ? "en" : "ar");
    if (!choose(select, next)) choose(select, "en");
    localStorage.setItem("qalam.target", select.value);
    showStatus($("translateStatus"), "Target set to " + (select.options[select.selectedIndex] || {}).textContent + ".");
  }

  function openAllContext() {
    var query = $("source").value.trim();
    if (!query) return;
    $("corpusQuery").value = query;
    if (lastTranslation) {
      choose($("corpusFrom"), lastTranslation.detected || "ar");
      choose($("corpusTo"), lastTranslation.target || "en");
    }
    tab("corpus");
    searchCorpus();
  }

  function init() {
    initTheme();
    if (!token) {
      showStatus($("translateStatus"), "This page needs its Qalam token. Open the exact link shown by the app.", "error");
      return;
    }

    document.querySelectorAll(".tab").forEach(function (button) {
      button.addEventListener("click", function () { tab(button.getAttribute("data-tab")); });
    });
    $("translateBtn").addEventListener("click", translate);
    $("wordingBtn").addEventListener("click", suggestWording);
    $("useWordingBtn").addEventListener("click", useWording);
    $("reportBugBtn").addEventListener("click", openReport);
    $("reportCloseBtn").addEventListener("click", closeReport);
    $("reportCancelBtn").addEventListener("click", closeReport);
    $("reportSubmitBtn").addEventListener("click", submitReport);
    $("reportCopyBtn").addEventListener("click", copyReport);
    $("reportModal").addEventListener("click", function (event) {
      if (event.target === $("reportModal")) closeReport();
    });
    document.addEventListener("keydown", function (event) {
      if (event.key === "Escape" && !$("reportModal").classList.contains("hidden")) closeReport();
    });
    $("swapBtn").addEventListener("click", swapTarget);
    $("source").addEventListener("keydown", function (event) {
      if ((event.ctrlKey || event.metaKey) && event.key === "Enter") { event.preventDefault(); translate(); }
    });
    document.querySelectorAll(".sample").forEach(function (button) {
      button.addEventListener("click", function () { useSample(button); });
    });
    $("copyBtn").addEventListener("click", copyResult);
    $("speakBtn").addEventListener("click", speak);
    $("addStudyBtn").addEventListener("click", addToStudy);
    $("moreContext").addEventListener("click", openAllContext);
    $("revealBtn").addEventListener("click", reveal);
    $("gradeRow").querySelectorAll("button").forEach(function (button) {
      button.addEventListener("click", function () { grade(button.getAttribute("data-grade")); });
    });
    $("importBtn").addEventListener("click", importSaved);
    $("reviewAhead").addEventListener("click", function () { loadStudy("all"); });
    $("corpusBtn").addEventListener("click", searchCorpus);
    $("corpusQuery").addEventListener("keydown", function (event) {
      if (event.key === "Enter") { event.preventDefault(); searchCorpus(); }
    });

    function connection() {
      var online = navigator.onLine;
      $("connection").classList.toggle("offline", !online);
      $("connection").querySelector("span").textContent = online ? "local" : "offline";
    }
    window.addEventListener("online", connection);
    window.addEventListener("offline", connection);
    connection();

    if ("serviceWorker" in navigator &&
        (location.protocol === "https:" || location.hostname === "localhost" || location.hostname === "127.0.0.1")) {
      navigator.serviceWorker.register("/sw.js").catch(function () { });
    }

    var installButton = $("installAppBtn");
    if (installButton && !window.matchMedia("(display-mode: standalone)").matches) {
      window.addEventListener("beforeinstallprompt", function (event) {
        event.preventDefault();
        installPrompt = event;
        installButton.classList.remove("hidden");
      });
      installButton.addEventListener("click", function () {
        if (!installPrompt) {
          showStatus($("translateStatus"), "Use your browser menu to install or add Qalam to the home screen.", "");
          return;
        }
        installPrompt.prompt();
        installPrompt = null;
        installButton.classList.add("hidden");
      });
    }

    api("/api/languages").then(function (data) {
      languages = JSON.parse(data.languages);
      $("languageCount").textContent = languages.length;
      populate($("target"), false);
      populate($("corpusFrom"), false);
      populate($("corpusTo"), false);
      choose($("target"), localStorage.getItem("qalam.target") || "en");
      choose($("corpusFrom"), "en");
      choose($("corpusTo"), "es");
      $("target").addEventListener("change", function () {
        localStorage.setItem("qalam.target", $("target").value);
      });
      loadStudy("due");
    }).catch(function (error) {
      showStatus($("translateStatus"), error.message, "error");
    });

    $("version").textContent = "Qalam Context · local";
  }

  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
  else init();
})();
