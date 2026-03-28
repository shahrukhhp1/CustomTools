// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Shared helpers used across widgets.
(function () {
    const root = (window.DailyTools = window.DailyTools || {});

    root.copyToClipboard = async function (text) {
        if (!text) return false;
        if (navigator.clipboard?.writeText) {
            await navigator.clipboard.writeText(text);
            return true;
        }
        return false;
    };

    root.flashButtonText = function (buttonEl, nextText, durationMs) {
        if (!buttonEl) return;
        const prev = buttonEl.textContent;
        buttonEl.textContent = nextText;
        window.setTimeout(function () {
            buttonEl.textContent = prev;
        }, durationMs ?? 900);
    };

    root.copyCurrentUrl = async function (buttonEl) {
        const ok = await root.copyToClipboard(window.location.href);
        if (ok) root.flashButtonText(buttonEl, "Copied link", 900);
    };

    root.getCurrentTheme = function () {
        const t = document.documentElement.getAttribute("data-theme");
        return (t === "dark" || t === "light") ? t : "light";
    };

    root.applyTheme = function (theme) {
        const t = (theme === "dark" || theme === "light") ? theme : "light";
        document.documentElement.setAttribute("data-theme", t);
        try { localStorage.setItem("dtools-theme", t); } catch { }
        return t;
    };

    root.updateThemeToggle = function (buttonEl) {
        const btn = buttonEl || document.getElementById("themeToggle");
        if (!btn) return;
        const t = root.getCurrentTheme();
        btn.textContent = (t === "dark") ? "Dark" : "Light";
        btn.setAttribute("aria-label", "Toggle theme");
        btn.setAttribute("title", "Toggle theme");
    };

    root.toggleTheme = function (buttonEl) {
        const next = root.getCurrentTheme() === "dark" ? "light" : "dark";
        root.applyTheme(next);
        root.updateThemeToggle(buttonEl);
    };

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () {
            root.updateThemeToggle();
        });
    } else {
        root.updateThemeToggle();
    }
})();
