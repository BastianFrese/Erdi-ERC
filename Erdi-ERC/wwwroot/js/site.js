// Please see documentation at https://learn.microsoft.com/aspnet/core/client-side/bundling-and-minification
// for details on configuring this project to bundle and minify static web assets.

// Write your JavaScript code.

(() => {
    const shouldUpgrade = (select) => {
        if (!(select instanceof HTMLSelectElement)) return false;
        if (select.multiple || select.size > 1) return false;
        if (select.classList.contains('js-native-select')) return false;
        if (select.dataset.native === 'true') return false;
        if (select.dataset.enhanced === 'erdi-select') return false;
        return true;
    };

    const syncNativeText = (triggerLabel, select) => {
        const selected = select.options[select.selectedIndex];
        triggerLabel.textContent = selected ? selected.text : '';
    };

    const restoreMenuToWrapper = (root) => {
        const menu = root.querySelector('.erdi-select__menu') || document.querySelector(`.erdi-select__menu[data-owner="${root.dataset.selectId}"]`);
        if (!menu) return;

        if (menu.parentElement !== root) {
            root.appendChild(menu);
        }

        menu.classList.remove('erdi-select__menu--portal');
        menu.style.left = '';
        menu.style.top = '';
        menu.style.width = '';
        menu.style.maxHeight = '';
        menu.style.zIndex = '';
    };

    const closeAll = (except = null) => {
        document.querySelectorAll('.erdi-select.is-open').forEach(root => {
            if (except && root === except) return;
            root.classList.remove('is-open');

            const menu = root.querySelector('.erdi-select__menu') || document.querySelector(`.erdi-select__menu[data-owner="${root.dataset.selectId}"]`);
            const trigger = root.querySelector('.erdi-select__trigger');

            if (menu) menu.hidden = true;
            if (trigger) trigger.setAttribute('aria-expanded', 'false');

            restoreMenuToWrapper(root);
        });
    };

    const build = (select, index) => {
        if (!shouldUpgrade(select)) return;

        select.dataset.enhanced = 'erdi-select';

        const wrapper = document.createElement('div');
        wrapper.className = 'erdi-select';
        wrapper.dataset.selectId = `erdi-select-${index}`;

        const trigger = document.createElement('button');
        trigger.type = 'button';
        trigger.className = 'erdi-select__trigger';
        trigger.setAttribute('aria-haspopup', 'listbox');
        trigger.setAttribute('aria-expanded', 'false');

        const label = document.createElement('span');
        label.className = 'erdi-select__label';

        const chevron = document.createElement('i');
        chevron.className = 'bi bi-chevron-down erdi-select__chevron';
        chevron.setAttribute('aria-hidden', 'true');

        trigger.appendChild(label);
        trigger.appendChild(chevron);

        const menu = document.createElement('div');
        menu.className = 'erdi-select__menu';
        menu.dataset.owner = wrapper.dataset.selectId;
        menu.setAttribute('role', 'listbox');
        menu.hidden = true;

        const positionMenu = () => {
            if (!wrapper.classList.contains('is-open')) return;
            const rect = trigger.getBoundingClientRect();
            const gap = 6;
            const viewportPadding = 12;
            const maxHeight = Math.max(180, window.innerHeight - rect.bottom - gap - viewportPadding);

            menu.style.left = `${Math.max(viewportPadding, rect.left)}px`;
            menu.style.top = `${rect.bottom + gap}px`;
            menu.style.width = `${Math.max(160, rect.width)}px`;
            menu.style.maxHeight = `${maxHeight}px`;
            menu.style.zIndex = '5000';
        };

        // Nur neu positionieren, wenn das Menü tatsächlich offen ist — und
        // rAF-gedrosselt, damit ein Scroll-Burst (Mausrad, Touch) nicht 60×
        // pro Frame getBoundingClientRect() + Style-Writes auslöst.
        // Vorher: positionMenu hing an window-scroll (capture), rief für jede
        // offene .erdi-select getBoundingClientRect auf → Layout-Thrash beim Scrollen.
        let repositionTicking = false;
        const scheduleReposition = () => {
            if (repositionTicking) return;
            repositionTicking = true;
            requestAnimationFrame(() => { repositionTicking = false; positionMenu(); });
        };

        const rebuildOptions = () => {
            menu.innerHTML = '';
            Array.from(select.options).forEach((opt, optionIndex) => {
                const item = document.createElement('button');
                item.type = 'button';
                item.className = 'erdi-select__option';
                item.setAttribute('role', 'option');
                item.dataset.index = String(optionIndex);
                item.textContent = opt.text;
                if (opt.disabled) item.classList.add('is-disabled');
                if (opt.selected) item.classList.add('is-selected');

                item.addEventListener('click', () => {
                    if (opt.disabled) return;
                    select.selectedIndex = optionIndex;
                    select.dispatchEvent(new Event('change', { bubbles: true }));
                    syncNativeText(label, select);
                    rebuildOptions();
                    closeAll();
                });

                menu.appendChild(item);
            });
            syncNativeText(label, select);
        };

        trigger.addEventListener('click', () => {
            const open = wrapper.classList.contains('is-open');
            closeAll(wrapper);

            if (open) {
                wrapper.classList.remove('is-open');
                menu.hidden = true;
                trigger.setAttribute('aria-expanded', 'false');
                restoreMenuToWrapper(wrapper);
                return;
            }

            wrapper.classList.add('is-open');
            trigger.setAttribute('aria-expanded', 'true');
            menu.hidden = false;
            menu.classList.add('erdi-select__menu--portal');
            document.body.appendChild(menu);
            positionMenu();
        });

        select.addEventListener('change', () => {
            syncNativeText(label, select);
            rebuildOptions();
        });

        select.classList.add('erdi-select__native');
        select.parentNode.insertBefore(wrapper, select);
        wrapper.appendChild(select);
        wrapper.appendChild(trigger);
        wrapper.appendChild(menu);

        rebuildOptions();

        window.addEventListener('resize', scheduleReposition, { passive: true });
        // Nur Scroll repositionieren, wenn überhaupt ein Menü offen ist — der
        // rAF-Throttle (scheduleReposition) sorgt zusätzlich dafür, dass selbst
        // bei mehreren offenen Selects höchstens ein Style-Write pro Frame passiert.
        // Vorher: synchron auf jedem Scroll-Tick, mit getBoundingClientRect() pro
        // offener .erdi-select → massiver Layout-Thrash beim Scrollen.
        document.addEventListener('scroll', () => {
            if (wrapper.classList.contains('is-open')) scheduleReposition();
        }, { passive: true, capture: true });
    };

    document.querySelectorAll('select').forEach((select, index) => build(select, index));

    document.addEventListener('click', (ev) => {
        const target = ev.target instanceof Element ? ev.target : null;
        const insideSelect = !!target?.closest('.erdi-select');
        const insidePortalMenu = !!target?.closest('.erdi-select__menu');
        if (!insideSelect && !insidePortalMenu) closeAll();
    });

    document.addEventListener('keydown', (ev) => {
        if (ev.key === 'Escape') closeAll();
    });
})();

/* ---------------------------------------------------------------------------
   Navigationsreihe auf schmalen Schirmen.

   Auf 390 px passt die Reihe nicht mehr; sie ist seitlich wischbar, aber man
   sah es nicht: der letzte Punkt war einfach abgeschnitten. Hier bekommt sie
   einen weichen Rand, solange es etwas zu wischen gibt, und die aktive Seite
   wird beim Laden in den Blick gerueckt — sonst startet man auf "RACING HUB"
   und die eigene Seite liegt ausserhalb.

   Gemessen wird nur die Reihe selbst, nicht der Schirm: scrollWidth gegen
   clientWidth. Die Klassen setzt das CSS (99-responsive.css).
   --------------------------------------------------------------------------- */
(function () {
    const reihe = document.querySelector('.strip__nav');
    if (!reihe) return;

    const aktualisiere = () => {
        const scrollbar = reihe.scrollWidth - reihe.clientWidth > 2;
        const amEnde = reihe.scrollLeft + reihe.clientWidth >= reihe.scrollWidth - 2;
        reihe.classList.toggle('is-scrollable', scrollbar);
        reihe.classList.toggle('is-at-end', scrollbar && amEnde);
    };

    reihe.addEventListener('scroll', aktualisiere, { passive: true });
    window.addEventListener('resize', aktualisiere, { passive: true });

    const aktiv = reihe.querySelector('a.active');
    if (aktiv && reihe.scrollWidth > reihe.clientWidth) {
        // scrollIntoView blockweise waere zu weit: die Reihe soll nur seitlich
        // nachziehen, nicht die Seite springen lassen.
        const ziel = aktiv.offsetLeft - (reihe.clientWidth - aktiv.offsetWidth) / 2;
        reihe.scrollLeft = Math.max(0, ziel);
    }

    aktualisiere();
})();

/* ---------------------------------------------------------------------------
   Hinweis auf die neue Gestalt.

   Einmal je Browser. Ohne JavaScript erscheint er nicht — das ist Absicht:
   eine Hoeflichkeitsmeldung ist kein Inhalt, der ohne Skript dastehen muss,
   und so gibt es auch kein Aufblitzen beim Laden (das Element ist per CSS
   display:none, bis diese Datei es einblendet).

   Zwei Dinge, die diesen Hinweis von einem Dauerzustand unterscheiden:
   - er verabschiedet sich von selbst, wenn der Browser ihn kennt, und
   - er erscheint gar nicht mehr nach dem Ablaufdatum unten. Eine Meldung
     ueber eine Umstellung ist nach ein paar Wochen nur noch Rauschen.
   --------------------------------------------------------------------------- */
(function () {
    const hinweis = document.getElementById('designNotice');
    if (!hinweis) return;

    const schluessel = 'erc.design-notice.dismissed.v1';
    const bis = new Date('2026-12-31T23:59:59');   // danach nie wieder

    let bekannt = false;
    try { bekannt = localStorage.getItem(schluessel) === '1'; } catch (e) { /* privater Modus */ }
    if (bekannt || Date.now() > bis.getTime()) return;

    const schliessen = () => {
        hinweis.classList.remove('is-open');
        try { localStorage.setItem(schluessel, '1'); } catch (e) { /* dann eben jedes Mal */ }
    };

    // Kurz warten: nicht im ersten Bild auftauchen. Die Seite soll sich erst
    // setzen, sonst sieht der Hinweis wie ein Fehler aus.
    setTimeout(() => hinweis.classList.add('is-open'), 1200);

    hinweis.querySelector('[data-design-notice-close]')?.addEventListener('click', schliessen);
    document.addEventListener('keydown', (ereignis) => {
        if (ereignis.key === 'Escape' && hinweis.classList.contains('is-open')) schliessen();
    });
})();

/* ---------------------------------------------------------------------------
   Aufklapper "Mehr" in der Navigation.

   Reiner Aufklapper, kein Menue im Sinne von role="menu": das verlangt
   Pfeiltasten-Navigation und Fokusverwaltung. Hier fuehrt Tab durch die Links,
   Escape schliesst und setzt den Fokus auf den Knopf zurueck, ein Klick
   daneben schliesst ebenfalls. aria-expanded am Knopf beschreibt den Zustand.
   --------------------------------------------------------------------------- */
(function () {
    const knopf = document.getElementById('moreBtn');
    const panel = document.getElementById('morePanel');
    if (!knopf || !panel) return;

    const setzen = (offen) => {
        panel.classList.toggle('is-open', offen);
        knopf.setAttribute('aria-expanded', offen ? 'true' : 'false');
    };

    knopf.addEventListener('click', () => setzen(!panel.classList.contains('is-open')));

    document.addEventListener('click', (ereignis) => {
        if (!panel.classList.contains('is-open')) return;
        if (panel.contains(ereignis.target) || knopf.contains(ereignis.target)) return;
        setzen(false);
    });

    document.addEventListener('keydown', (ereignis) => {
        if (ereignis.key !== 'Escape' || !panel.classList.contains('is-open')) return;
        setzen(false);
        knopf.focus();
    });
})();

/* ---------------------------------------------------------------------------
   Navigation: was nicht mehr passt, wandert nach "Mehr".

   Auf schmalen Schirmen passen die sieben Hauptpunkte nicht mehr in die Reihe.
   Sie war deshalb seitlich wischbar — man musste erst wischen, um "Kalender"
   oder "Bewerbung" ueberhaupt zu sehen. Hier wird stattdessen gemessen: die
   Hauptpunkte stellen sich der Reihe nach auf, und alles ab dem ersten, der
   nicht mehr passt, wandert vorne in den Aufklapper — in der urspruenglichen
   Reihenfolge.

   Das Wischen bleibt als Rueckfall im CSS (overflow-x: auto): laeuft kein
   JavaScript, verhaelt sich die Reihe wie vorher. Laeuft es, gibt es nichts zu
   wischen.

   Bei jeder Groessenaenderung wird zuerst alles zurueckgesetzt und neu
   verteilt — so bleibt nichts haengen, wenn das Fenster wieder breiter wird.
   --------------------------------------------------------------------------- */
(function () {
    const reihe = document.querySelector('.strip__nav');
    const knopf = document.getElementById('moreBtn');
    const panel = document.getElementById('morePanel');
    if (!reihe || !knopf || !panel) return;
    const liste = panel.querySelector('.strip__more-inner');
    if (!liste) return;

    // Die Hauptpunkte einmal merken: Reihenfolge und Zuordnung bleiben erhalten,
    // egal wie oft umverteilt wird.
    const hauptpunkte = [...reihe.children].filter((el) => el.tagName === 'A');
    if (!hauptpunkte.length) return;

    const verteilen = () => {
        // 1. Alles zurueck in die Reihe, in der gemerkten Reihenfolge.
        for (const a of hauptpunkte) {
            if (!a.hasAttribute('data-verschoben')) continue;
            a.removeAttribute('data-verschoben');
            reihe.insertBefore(a, knopf);
        }

        // 2. Wie viel Platz bleibt neben dem Knopf?
        const luecke = parseFloat(getComputedStyle(reihe).columnGap) || 0;
        const platz = reihe.clientWidth - knopf.offsetWidth - luecke;
        if (platz <= 0) return;

        // 3. Von links nach rechts aufstellen: der erste, der nicht mehr passt,
        //    nimmt alle folgenden mit.
        let belegt = 0;
        const wandern = [];
        for (const a of hauptpunkte) {
            belegt += a.offsetWidth + luecke;
            if (belegt > platz) wandern.push(a);
        }
        if (!wandern.length) return;

        for (const a of wandern) a.setAttribute('data-verschoben', '');
        liste.prepend(...wandern);
    };

    verteilen();
    // Die Schrift laedt nach; danach sind die Breiten andere.
    if (document.fonts && document.fonts.ready) document.fonts.ready.then(verteilen);

    let zeitgeber;
    window.addEventListener('resize', () => {
        clearTimeout(zeitgeber);
        zeitgeber = setTimeout(verteilen, 150);
    });
})();
