// Event-Galerie Lightbox (F1-Look). Initialisiert sich auf data-event-gallery-Container.
// Markup-Erwartung (in EventDetail.cshtml gerendert):
//   <div data-event-gallery data-images='["/uploads/events/a.jpg", ...]'>
//     <button data-gallery-index="0"> ... </button>
//     ...
//     <div data-lightbox hidden>
//       <img data-lightbox-img>
//       <button data-lightbox-prev>←</button>
//       <button data-lightbox-next>→</button>
//       <button data-lightbox-close>×</button>
//     </div>
//   </div>
// Features: ←/→/ESC/Backdrop/Zoom (wheel + pinch).

(function () {
  'use strict';

  function init(gallery) {
    var lightbox = gallery.querySelector('[data-lightbox]');
    if (!lightbox) return;

    var img = lightbox.querySelector('[data-lightbox-img]');
    var prevBtn = lightbox.querySelector('[data-lightbox-prev]');
    var nextBtn = lightbox.querySelector('[data-lightbox-next]');
    var closeBtn = lightbox.querySelector('[data-lightbox-close]');

    var images;
    try {
      images = JSON.parse(gallery.getAttribute('data-images') || '[]');
    } catch (e) {
      images = [];
    }
    if (!Array.isArray(images) || images.length === 0) return;

    var currentIndex = 0;
    var zoom = 1;
    var lastTouchDist = 0;

    function show(index) {
      if (index < 0) index = images.length - 1;
      if (index >= images.length) index = 0;
      currentIndex = index;
      img.src = images[index];
      img.alt = 'Bild ' + (index + 1) + ' von ' + images.length;
      zoom = 1;
      img.style.transform = 'scale(1)';
    }

    function open(index) {
      show(index);
      lightbox.hidden = false;
      document.body.style.overflow = 'hidden';
    }

    function close() {
      lightbox.hidden = true;
      document.body.style.overflow = '';
      img.src = '';
    }

    // Triggers: alle data-gallery-index-Elemente
    gallery.querySelectorAll('[data-gallery-index]').forEach(function (el) {
      el.addEventListener('click', function (ev) {
        ev.preventDefault();
        var idx = parseInt(el.getAttribute('data-gallery-index') || '0', 10);
        open(idx);
      });
    });

    if (prevBtn) prevBtn.addEventListener('click', function () { show(currentIndex - 1); });
    if (nextBtn) nextBtn.addEventListener('click', function () { show(currentIndex + 1); });
    if (closeBtn) closeBtn.addEventListener('click', close);

    // Backdrop-Klick schließt (aber nicht Klick auf das Bild/Buttons)
    lightbox.addEventListener('click', function (ev) {
      if (ev.target === lightbox) close();
    });

    // ESC + Pfeiltasten
    document.addEventListener('keydown', function (ev) {
      if (lightbox.hidden) return;
      if (ev.key === 'Escape') close();
      else if (ev.key === 'ArrowLeft') show(currentIndex - 1);
      else if (ev.key === 'ArrowRight') show(currentIndex + 1);
    });

    // Zoom: Wheel + Pinch
    img.addEventListener('wheel', function (ev) {
      ev.preventDefault();
      var delta = ev.deltaY > 0 ? -0.15 : 0.15;
      zoom = Math.max(1, Math.min(4, zoom + delta));
      img.style.transform = 'scale(' + zoom + ')';
    }, { passive: false });

    img.addEventListener('touchstart', function (ev) {
      if (ev.touches.length !== 2) return;
      var dx = ev.touches[0].clientX - ev.touches[1].clientX;
      var dy = ev.touches[0].clientY - ev.touches[1].clientY;
      lastTouchDist = Math.sqrt(dx * dx + dy * dy);
    });
    img.addEventListener('touchmove', function (ev) {
      if (ev.touches.length !== 2) return;
      var dx = ev.touches[0].clientX - ev.touches[1].clientX;
      var dy = ev.touches[0].clientY - ev.touches[1].clientY;
      var d = Math.sqrt(dx * dx + dy * dy);
      var factor = d / lastTouchDist;
      zoom = Math.max(1, Math.min(4, zoom * factor));
      lastTouchDist = d;
      img.style.transform = 'scale(' + zoom + ')';
    }, { passive: true });
  }

  function initAll() {
    document.querySelectorAll('[data-event-gallery]').forEach(init);
  }

  if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initAll);
  } else {
    initAll();
  }
})();
