/* =====================================================================
   THEME TOGGLE · 13-tokens-light.css zusammen mit <html data-theme="light">
   Persistenz: localStorage "erc.theme.v1" ("light" | "dark").
   Failsafe: ohne Button oder in Private-Mode Noop -> dunkler Default.
   ===================================================================== */
(function () {
    'use strict';

    var STORAGE_KEY = 'erc.theme.v1';

    function writeTheme(theme) {
        try { localStorage.setItem(STORAGE_KEY, theme); }
        catch (e) { /* Private Mode: nur Session-seitig */ }
    }

    function isLight() {
        return document.documentElement.getAttribute('data-theme') === 'light';
    }

    function applyTheme(buttons, themeAttr) {
        for (var i = 0; i < buttons.length; i++) {
            var btn = buttons[i];
            if (!btn) continue;
            // Icon: Sonne im dunklen Design (= Wechsel zu hell anbieten), Mond im hellen.
            var icon = btn.querySelector('i.bi');
            if (icon) {
                icon.classList.toggle('bi-sun', themeAttr === 'dark');
                icon.classList.toggle('bi-moon', themeAttr === 'light');
            }
            // Button-Zustand für AT: aria-pressed + sprechender Titel
            btn.setAttribute('aria-pressed', themeAttr === 'light' ? 'true' : 'false');
            btn.title = themeAttr === 'light' ? 'Dunkles Design aktivieren' : 'Helles Design aktivieren';
        }
    }

    function syncIcons() {
        // alle Toggle-Buttons (Layout + Admin-Layout)
        var buttons = document.querySelectorAll('#ercThemeToggle');
        if (buttons.length) applyTheme(buttons, isLight() ? 'light' : 'dark');
    }

    function setTheme(light) {
        if (light) {
            document.documentElement.setAttribute('data-theme', 'light');
            document.documentElement.setAttribute('data-bs-theme', 'light');
        } else {
            document.documentElement.removeAttribute('data-theme');
            document.documentElement.setAttribute('data-bs-theme', 'dark');
        }
        writeTheme(light ? 'light' : 'dark');
        syncIcons();
        var meta = document.querySelector('meta[name="theme-color"]');
        if (meta) meta.setAttribute('content', light ? '#ffffff' : '#06070a');
        document.dispatchEvent(new CustomEvent('erc:themechange', { detail: { theme: light ? 'light' : 'dark' } }));
    }

    function init() {
        var buttons = document.querySelectorAll('#ercThemeToggle');
        if (!buttons.length) return;

        // initialer Icon-Zustand (Pre-Paint-Script hat evtl. schon "light" gesetzt)
        syncIcons();

        for (var i = 0; i < buttons.length; i++) {
            buttons[i].addEventListener('click', function () {
                setTheme(!isLight());
            });
        }
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }
})();