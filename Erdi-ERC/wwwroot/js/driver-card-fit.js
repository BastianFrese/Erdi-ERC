/* ──────────────────────────────────────────────────────────────
   Driver-Card Footer-Autofit
   Verkleinert lange Team-/Strecken-/Hardware-Namen schrittweise,
   bis sie EINZEILIG in ihre Zelle passen – kein Abschneiden, kein
   Umbruch mitten im Wort. Logos bleiben unberührt (feste Höhe).
   ────────────────────────────────────────────────────────────── */
(function () {
  'use strict';

  var BASE_PX = 11;   // CSS-Ausgangsgröße von .erc-meta__val
  var MIN_PX  = 7.5;  // so klein darf es maximal werden, bevor wir aufgeben
  var STEP    = 0.5;
  var GUARD   = 40;   // Sicherheits-Stopp gegen Endlosschleifen

  function fit(el) {
    // erst auf CSS-Basis zurücksetzen (wichtig fürs Re-Fit bei resize)
    el.style.fontSize = '';
    var size = BASE_PX;
    var guard = GUARD;
    while (el.scrollWidth > el.clientWidth + 0.5 && size > MIN_PX && guard-- > 0) {
      size -= STEP;
      el.style.fontSize = size + 'px';
    }
  }

  function fitAll() {
    document.querySelectorAll('.erc-meta__val').forEach(fit);
  }

  function boot() {
    fitAll();
    // Webfont (Inter) ändert Textbreiten – nach dem Laden erneut messen.
    if (document.fonts && document.fonts.ready) {
      document.fonts.ready.then(fitAll).catch(function () {});
    }
  }

  if (document.readyState !== 'loading') {
    boot();
  } else {
    document.addEventListener('DOMContentLoaded', boot);
  }

  // Karten sind fix breit, aber Zoom/Layout-Wechsel sicherheitshalber abfangen.
  var t;
  window.addEventListener('resize', function () {
    clearTimeout(t);
    t = setTimeout(fitAll, 150);
  });
}());
