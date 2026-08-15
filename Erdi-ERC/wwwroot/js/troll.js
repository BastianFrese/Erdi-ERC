/* Erdi-Troll-System – clientseitige Gag-Logik. Nur auf der Gate-Seite geladen.
   Jeder Gag wird über ein data-Attribut auf seinem Wrapper initialisiert; fehlt der
   Wrapper, passiert nichts (robust gegen weggelassene Gags). */
(function () {
    'use strict';

    var card = document.querySelector('.troll-card');
    if (!card) return;

    var reduce = window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    var rand = function (n) { return Math.floor(Math.random() * n); };
    var show = function (el) { if (el) el.classList.remove('troll-hidden'); };
    var hide = function (el) { if (el) el.classList.add('troll-hidden'); };

    // ---- Fake-Ladebalken: zählt auf 99 % und findet „kein Talent" ----
    (function () {
        var root = card.querySelector('[data-troll-loading]');
        if (!root) return;
        var bar = root.querySelector('[data-troll-bar]');
        var pct = root.querySelector('[data-troll-loading-pct]');
        var status = root.querySelector('[data-troll-loading-status]');
        var done = root.querySelector('[data-troll-loading-done]');
        var lines = ['Lade neuronales Talent-Modell …', 'Vergleiche mit Erdis Rundenzeiten …',
            'Suche nach Talent …', 'Talent nicht gefunden. Suche erneut …'];
        var p = 0, li = 0;
        function step() {
            p += reduce ? 100 : (2 + rand(7));
            if (p >= 99) p = 99;
            if (bar) bar.style.width = p + '%';
            if (pct) pct.textContent = p;
            var nextLine = Math.min(lines.length - 1, Math.floor(p / 25));
            if (status && nextLine > li) { li = nextLine; status.textContent = lines[li]; }
            if (p >= 99) { if (status) status.textContent = 'Fehler: Talent nicht gefunden.'; show(done); return; }
            setTimeout(step, reduce ? 0 : 170 + rand(220));
        }
        setTimeout(step, reduce ? 0 : 300);
    })();

    // ---- Fake-Degradierung: verzögerte Auflösung ----
    (function () {
        var root = card.querySelector('[data-troll-reveal]');
        if (!root) return;
        var text = root.querySelector('[data-troll-reveal-text]');
        var btn = root.querySelector('[data-troll-reveal-btn]');
        setTimeout(function () { show(text); show(btn); }, reduce ? 0 : 1500);
    })();

    // ---- Reaktionstest: Ampel ----
    (function () {
        var root = card.querySelector('[data-troll-reaction]');
        if (!root) return;
        var light = root.querySelector('[data-troll-light]');
        var result = root.querySelector('[data-troll-reaction-result]');
        var done = root.querySelector('[data-troll-reaction-done]');
        if (!light) return;
        var state = 'idle', goAt = 0, timer = null;

        function arm() {
            state = 'wait';
            light.className = 'troll-light';
            light.textContent = 'Warte auf Grün …';
            timer = setTimeout(function () {
                state = 'go'; goAt = (window.performance || Date).now();
                light.classList.add('is-go'); light.textContent = 'KLICK!';
            }, 900 + rand(2600));
        }

        light.addEventListener('click', function () {
            if (state === 'idle') { arm(); return; }
            if (state === 'wait') {
                clearTimeout(timer); state = 'idle';
                light.classList.add('is-early'); light.textContent = 'Frühstart! Erdi lacht. Nochmal.';
                setTimeout(arm, 800); return;
            }
            if (state === 'go') {
                var ms = Math.round((window.performance || Date).now() - goAt);
                state = 'done'; light.textContent = ms + ' ms';
                if (result) result.textContent = ms < 250 ? 'Nicht schlecht. Für einen Menschen.'
                    : ms < 450 ? 'Erdi hat das schon im Schlaf geschafft.'
                    : 'Schnecke. Erdi wartet immer noch.';
                show(done);
            }
        });
        light.textContent = 'Start (klick mich)';
    })();

    // ---- Glücksrad ----
    (function () {
        var root = card.querySelector('[data-troll-wheel]');
        if (!root) return;
        var face = root.querySelector('[data-troll-wheel-face]');
        var result = root.querySelector('[data-troll-wheel-result]');
        var spin = root.querySelector('[data-troll-wheel-spin]');
        var done = root.querySelector('[data-troll-wheel-done]');
        if (!spin) return;
        var prizes = ['+5 Strafpunkte (gelten nur in Erdis Kopf)', 'Pole Position … im nächsten Leben',
            'Eine gratis Durchfahrtsstrafe', 'Erdis Respekt (leider nicht einlösbar)',
            'Ein DNF-Gutschein', '−10 Sekunden auf deine beste Ausrede'];
        spin.addEventListener('click', function () {
            if (face) face.classList.add('is-spinning');
            if (result) result.textContent = 'Dreht …';
            setTimeout(function () {
                if (face) face.classList.remove('is-spinning');
                if (result) result.textContent = '🎁 ' + prizes[rand(prizes.length)];
                show(done); hide(spin);
            }, reduce ? 0 : 1400);
        });
    })();

    // ---- Würfel ----
    (function () {
        var root = card.querySelector('[data-troll-dice]');
        if (!root) return;
        var face = root.querySelector('[data-troll-dice-face]');
        var result = root.querySelector('[data-troll-dice-result]');
        var roll = root.querySelector('[data-troll-dice-roll]');
        var done = root.querySelector('[data-troll-dice-done]');
        if (!roll) return;
        var faces = ['⚀', '⚁', '⚂', '⚃', '⚄', '⚅'];
        var lines = ['P1! … aber nur in dieser Animation.', 'P2. Erster der Verlierer.',
            'P3. Erdi nickt anerkennend.', 'Mittelfeld. Wie immer.', 'Hinterbänkler. Üben!', 'DNF. Erdi seufzt.'];
        roll.addEventListener('click', function () {
            var ticks = reduce ? 1 : 10, n = 0;
            (function tick() {
                n = rand(6);
                if (face) face.textContent = faces[n];
                if (--ticks > 0) { setTimeout(tick, 70); return; }
                if (result) result.textContent = 'Startplatz ' + (n + 1) + ': ' + lines[n];
                show(done); hide(roll);
            })();
        });
    })();

    // ---- Weglaufende Buttons (fake-terms „Ablehnen" + runaway-button) ----
    Array.prototype.forEach.call(card.querySelectorAll('[data-troll-dodge]'), function (btn) {
        var isFinal = btn.hasAttribute('data-troll-final');
        var maxDodges = 3, dodges = 0;
        function jump(e) {
            if (isFinal && dodges >= maxDodges) { btn.style.transform = ''; return; }
            var dx = (rand(2) ? 1 : -1) * (40 + rand(120));
            var dy = (rand(2) ? 1 : -1) * (20 + rand(60));
            btn.style.transform = 'translate(' + dx + 'px,' + dy + 'px)';
            dodges++;
            if (e && e.cancelable) e.preventDefault();
        }
        btn.addEventListener('mouseenter', jump);
        btn.addEventListener('click', function (e) {
            if (isFinal && dodges >= maxDodges) return; // müde → Klick/Link durchlassen
            jump(e);
        });
    });

    // ---- Fake-Zwangsupdate: zählt sinnlos hoch und endet im Nichts ----
    (function () {
        var root = card.querySelector('[data-troll-update]');
        if (!root) return;
        var n = root.querySelector('[data-troll-update-n]');
        var bar = root.querySelector('[data-troll-update-bar]');
        var done = root.querySelector('[data-troll-update-done]');
        var count = 1, pct = 0;
        function step() {
            count += 1 + rand(40);
            if (count > 4000) count = 4000;
            pct += reduce ? 100 : (3 + rand(6));
            if (pct >= 100) pct = 100;
            if (n) n.textContent = count;
            if (bar) bar.style.width = pct + '%';
            if (pct >= 100 || count >= 4000) { show(done); return; }
            setTimeout(step, reduce ? 0 : 150 + rand(180));
        }
        setTimeout(step, reduce ? 0 : 300);
    })();

    // ---- Fake-Ban: Countdown, der sich als Scherz auflöst ----
    (function () {
        var root = card.querySelector('[data-troll-ban]');
        if (!root) return;
        var title = root.querySelector('[data-troll-ban-title]');
        var text = root.querySelector('[data-troll-ban-text]');
        var count = root.querySelector('[data-troll-ban-count]');
        var done = root.querySelector('[data-troll-ban-done]');
        var n = 3;
        function tick() {
            n -= 1;
            if (n > 0) { if (count) count.textContent = n; setTimeout(tick, reduce ? 0 : 1000); return; }
            if (title) { title.classList.remove('troll-shake'); title.innerHTML = 'War nur Spaß. <em>Oder?</em>'; }
            if (text) text.innerHTML = 'Erdi konnte sich gerade noch zurückhalten. Diesmal.';
            show(done);
        }
        setTimeout(tick, reduce ? 0 : 1000);
    })();

    // ---- Treue-Button: 3 Sekunden gedrückt halten ----
    (function () {
        var root = card.querySelector('[data-troll-hold]');
        if (!root) return;
        var btn = root.querySelector('[data-troll-hold-btn]');
        var fill = root.querySelector('[data-troll-hold-fill]');
        var label = root.querySelector('[data-troll-hold-label]');
        var done = root.querySelector('[data-troll-hold-done]');
        if (!btn) return;
        var HOLD_MS = 3000, start = 0, raf = null, finished = false;
        function frame(now) {
            var p = Math.min(100, ((now - start) / HOLD_MS) * 100);
            if (fill) fill.style.width = p + '%';
            if (p >= 100) { finish(); return; }
            raf = requestAnimationFrame(frame);
        }
        function begin(e) {
            if (finished) return;
            if (e && e.cancelable) e.preventDefault();
            if (reduce) { finish(); return; }
            start = (window.performance || Date).now();
            if (label) label.textContent = 'Halten …';
            raf = requestAnimationFrame(frame);
        }
        function cancel() {
            if (finished) return;
            if (raf) cancelAnimationFrame(raf);
            if (fill) fill.style.width = '0%';
            if (label) label.textContent = 'Losgelassen! Erdi ist enttäuscht. Nochmal.';
        }
        function finish() {
            finished = true;
            if (raf) cancelAnimationFrame(raf);
            if (fill) fill.style.width = '100%';
            if (label) label.textContent = 'Erdi ist zufrieden.';
            show(done);
        }
        btn.addEventListener('mousedown', begin);
        btn.addEventListener('touchstart', begin, { passive: false });
        ['mouseup', 'mouseleave', 'touchend', 'touchcancel'].forEach(function (ev) {
            btn.addEventListener(ev, cancel);
        });
    })();

    // ---- Whack-a-Erdi: flüchtiges Ziel mehrfach treffen ----
    (function () {
        var root = card.querySelector('[data-troll-whack]');
        if (!root) return;
        var field = root.querySelector('[data-troll-whack-field]');
        var target = root.querySelector('[data-troll-whack-target]');
        var result = root.querySelector('[data-troll-whack-result]');
        var goalEl = root.querySelector('[data-troll-whack-goal]');
        var done = root.querySelector('[data-troll-whack-done]');
        if (!field || !target) return;
        var goal = parseInt(goalEl && goalEl.textContent, 10) || 3;
        var hits = 0;
        function move() {
            var tw = target.offsetWidth || 56, th = target.offsetHeight || 56;
            target.style.left = rand(Math.max(1, field.clientWidth - tw)) + 'px';
            target.style.top = rand(Math.max(1, field.clientHeight - th)) + 'px';
        }
        target.addEventListener('click', function () {
            hits++;
            if (result) result.textContent = 'Treffer: ' + hits;
            if (hits >= goal) {
                target.style.display = 'none';
                if (result) result.textContent = 'Erwischt! ' + hits + ' Treffer. Erdi ist beeindruckt.';
                show(done);
                return;
            }
            move();
        });
        target.addEventListener('mouseenter', move); // flüchtig: weicht beim Drüberfahren aus
        move();
    })();

    // ---- Roter Knopf: Umkehr-Psychologie ----
    (function () {
        var root = card.querySelector('[data-troll-red]');
        if (!root) return;
        var btn = root.querySelector('[data-troll-red-btn]');
        var text = root.querySelector('[data-troll-red-text]');
        var done = root.querySelector('[data-troll-red-done]');
        if (!btn) return;
        var lines = ['Ich hab doch gesagt: NICHT drücken.', 'Jetzt ist es zu spät.',
            'Erdi hat sich das gemerkt.', 'Na gut — du darfst trotzdem rein.'];
        var i = 0;
        btn.addEventListener('click', function () {
            if (text) text.textContent = lines[Math.min(i, lines.length - 1)];
            i++;
            btn.classList.add('is-pressed');
            if (i >= lines.length) { show(done); btn.disabled = true; }
        });
        // Sicherheitsnetz: wer brav nicht drückt, kommt nach 6 s trotzdem rein.
        setTimeout(function () { show(done); }, reduce ? 0 : 6000);
    })();

    // ---- Magische 8-Ball ----
    (function () {
        var root = card.querySelector('[data-troll-8ball]');
        if (!root) return;
        var face = root.querySelector('[data-troll-8ball-face]');
        var result = root.querySelector('[data-troll-8ball-result]');
        var shake = root.querySelector('[data-troll-8ball-shake]');
        var done = root.querySelector('[data-troll-8ball-done]');
        if (!shake) return;
        var answers = ['Definitiv nicht.', 'Frag Erdi später nochmal.', 'Zeichen deuten auf P-letzter.',
            'Ja — aber nur im Training.', 'Erdi sagt nein.', 'So sicher wie ein Reifenschaden in Runde 1.',
            'Die Kugel lacht nur.', 'Vielleicht. Wenn du übst.'];
        shake.addEventListener('click', function () {
            if (face) face.classList.add('is-spinning');
            if (result) result.textContent = 'Schüttelt …';
            setTimeout(function () {
                if (face) face.classList.remove('is-spinning');
                if (result) result.textContent = '🎱 ' + answers[rand(answers.length)];
                show(done);
            }, reduce ? 0 : 1100);
        });
    })();

    // ---- Gezinkter Münzwurf: landet immer auf „Kopf" ----
    (function () {
        var root = card.querySelector('[data-troll-coin]');
        if (!root) return;
        var face = root.querySelector('[data-troll-coin-face]');
        var result = root.querySelector('[data-troll-coin-result]');
        var flip = root.querySelector('[data-troll-coin-flip]');
        var done = root.querySelector('[data-troll-coin-done]');
        if (!flip) return;
        flip.addEventListener('click', function () {
            if (face) face.classList.add('is-spinning');
            if (result) result.textContent = 'Die Münze dreht sich …';
            setTimeout(function () {
                if (face) { face.classList.remove('is-spinning'); face.textContent = '👑'; }
                if (result) result.textContent = 'Kopf! Du darfst rein. (Erdi hatte nie eine Zahl-Seite.)';
                show(done); hide(flip);
            }, reduce ? 0 : 1200);
        });
    })();
})();
