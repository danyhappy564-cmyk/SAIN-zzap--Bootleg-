// SAIN zzap site: language switch, tactic tabs, utility-AI demo, scroll reveal.
(function () {
  "use strict";
  var KEY = "sainzzap.lang";
  var root = document.documentElement;
  var META = {
    ko: {
      title: "SAIN zzap — 문 앞에서 멈추지 않고 고인물처럼 싸우는 봇",
      desc: "SPT 4.1용 SAIN 포크. 문 끼임 수정, 실제 고인물 플레이를 흉내 낸 전술 행동, 바로 쓰는 zzap 프리셋."
    },
    en: {
      title: "SAIN zzap — bots that don't freeze at doors and fight like veterans",
      desc: "A SAIN fork for SPT 4.1: door-stuck fixes, tactics modelled on veteran play, and ready-made zzap presets."
    }
  };

  function store(l) { try { localStorage.setItem(KEY, l); } catch (e) { /* storage blocked */ } }

  function setLang(l, save) {
    if (l !== "ko" && l !== "en") l = "ko";
    root.setAttribute("data-lang", l);
    root.lang = l;
    document.title = META[l].title;
    var d = document.querySelector('meta[name="description"]');
    if (d) d.setAttribute("content", META[l].desc);
    document.querySelectorAll(".lang button").forEach(function (b) {
      b.setAttribute("aria-pressed", String(b.dataset.set === l));
    });
    // images that exist in both languages
    document.querySelectorAll("img[data-ko]").forEach(function (img) {
      if (img.dataset.deferred === "1") return; // loaded on demand
      var want = img.dataset[l];
      if (img.getAttribute("src") !== want) img.setAttribute("src", want);
      if (img.dataset["alt" + l]) img.alt = img.dataset["alt" + l];
    });
    document.querySelectorAll("details.gif[open] img[data-deferred]").forEach(function (img) {
      img.setAttribute("src", img.dataset[l]);
    });
    if (save) {
      store(l);
      try {
        var u = new URL(location.href);
        u.searchParams.set("lang", l);
        history.replaceState(null, "", u);
      } catch (e) { /* old browser */ }
    }
  }

  document.querySelectorAll(".lang button").forEach(function (b) {
    b.addEventListener("click", function () { setLang(b.dataset.set, true); });
  });
  setLang(root.getAttribute("data-lang") || "ko", false);

  // big GIFs inside <details> load only when opened (saves phone data)
  document.querySelectorAll("details.gif").forEach(function (det) {
    det.addEventListener("toggle", function () {
      if (!det.open) return;
      det.querySelectorAll("img[data-deferred]").forEach(function (img) {
        img.setAttribute("src", img.dataset[root.getAttribute("data-lang")]);
      });
    });
  });

  // tactic tabs
  var tabs = document.querySelectorAll("[role=tab]");
  function pick(tab, focus) {
    tabs.forEach(function (t) {
      var on = t === tab;
      t.setAttribute("aria-selected", String(on));
      t.tabIndex = on ? 0 : -1;
      document.getElementById(t.getAttribute("aria-controls")).hidden = !on;
    });
    if (focus) tab.focus();
  }
  tabs.forEach(function (t, i) {
    t.addEventListener("click", function () { pick(t, false); });
    t.addEventListener("keydown", function (e) {
      var k = e.key, n = null;
      if (k === "ArrowRight") n = tabs[(i + 1) % tabs.length];
      if (k === "ArrowLeft") n = tabs[(i - 1 + tabs.length) % tabs.length];
      if (n) { e.preventDefault(); pick(n, true); }
    });
  });

  // utility-AI demo: scores per scenario, weighted roll like the mod does
  var SCEN = {
    camp: { push: 34, nade: 71, hold: 22, flank: 48, search: 18, retreat: 6,
      ko: "적이 6초 넘게 한자리에 있고 실내 → 수류탄 점수가 오릅니다.",
      en: "The enemy has sat still for 6+ s indoors, so the grenade scores high." },
    reload: { push: 82, nade: 30, hold: 12, flank: 26, search: 20, retreat: 5,
      ko: "장전 소리를 들었고 그 시간 안에 닿을 수 있음 → 밀기 점수가 오릅니다.",
      en: "It heard a reload and can reach the enemy in time, so pushing scores high." },
    hurt: { push: 8, nade: 24, hold: 15, flank: 18, search: 6, retreat: 74,
      ko: "중상이고 상대 수가 많음 → 후퇴. 맞는 중이면 제자리 지키기는 건너뜁니다.",
      en: "Badly hurt and outnumbered, so retreat. While being hit it never just stands still." }
  };
  var ORDER = ["push", "nade", "hold", "flank", "search", "retreat"];
  var demo = document.getElementById("udemo");
  if (demo) {
    var cur = "camp";
    var rows = {};
    ORDER.forEach(function (k) { rows[k] = demo.querySelector('[data-k="' + k + '"]'); });
    var why = demo.querySelector(".why");
    function render(roll) {
      var s = SCEN[cur], total = 0;
      ORDER.forEach(function (k) { total += Math.pow(s[k], 2); });
      var r = Math.random() * total, chosen = ORDER[0];
      for (var i = 0; i < ORDER.length; i++) {
        r -= Math.pow(s[ORDER[i]], 2);
        if (r <= 0) { chosen = ORDER[i]; break; }
      }
      if (!roll) {
        chosen = ORDER.reduce(function (a, b) { return s[a] >= s[b] ? a : b; });
      }
      ORDER.forEach(function (k) {
        rows[k].querySelector(".u-fill").style.width = s[k] + "%";
        rows[k].querySelector(".u-val").textContent = s[k];
        rows[k].classList.toggle("pick", k === chosen);
      });
      why.querySelector('[lang="ko"]').textContent = s.ko;
      why.querySelector('[lang="en"]').textContent = s.en;
    }
    demo.querySelectorAll(".scen button").forEach(function (b) {
      b.addEventListener("click", function () {
        cur = b.dataset.s;
        demo.querySelectorAll(".scen button").forEach(function (x) { x.setAttribute("aria-pressed", String(x === b)); });
        render(false);
      });
    });
    demo.querySelector(".u-btn").addEventListener("click", function () { render(true); });
    render(false);
  }

  // reveal + active nav link
  root.classList.add("js");
  var navLinks = {};
  document.querySelectorAll(".nav a").forEach(function (a) { navLinks[a.getAttribute("href").slice(1)] = a; });
  if ("IntersectionObserver" in window) {
    var io = new IntersectionObserver(function (es) {
      es.forEach(function (e) { if (e.isIntersecting) { e.target.classList.add("in"); io.unobserve(e.target); } });
    }, { rootMargin: "0px 0px -8% 0px" });
    document.querySelectorAll(".rv").forEach(function (el) { io.observe(el); });
    var so = new IntersectionObserver(function (es) {
      es.forEach(function (e) {
        if (!e.isIntersecting) return;
        Object.keys(navLinks).forEach(function (id) { navLinks[id].classList.toggle("on", id === e.target.id); });
      });
    }, { rootMargin: "-45% 0px -50% 0px" });
    document.querySelectorAll("section[id]").forEach(function (s) { so.observe(s); });
  } else {
    document.querySelectorAll(".rv").forEach(function (el) { el.classList.add("in"); });
  }
})();
