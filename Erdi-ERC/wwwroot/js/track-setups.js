(() => {
    const cards = Array.from(document.querySelectorAll('.setup-card[data-setup-json]'));
    if (cards.length === 0) return;

    const editorConfig = window.erdiSetupEditorConfig || null;
    const metricConfig = Array.isArray(window.erdiSetupMetricConfig) ? window.erdiSetupMetricConfig : [];
    const categoriesFromConfig = Array.isArray(editorConfig?.Categories) ? editorConfig.Categories : [];

    const categoryMeta = categoriesFromConfig.reduce((acc, category) => {
        if (!category?.Key) return acc;
        acc[category.Key] = { title: category.Label || category.Key, icon: category.Icon || '•' };
        return acc;
    }, {});

    const fieldMeta = categoriesFromConfig
        .flatMap(category => Array.isArray(category.Fields) ? category.Fields : [])
        .reduce((acc, field) => {
            if (!field?.Key) return acc;
            acc[field.Key] = {
                label: field.Label || field.Key,
                min: Number(field.Min),
                max: Number(field.Max),
                step: Number(field.Step),
                defaultValue: Number(field.DefaultValue)
            };
            return acc;
        }, {});

    const clamp = (value, min = 0, max = 100) => Math.min(max, Math.max(min, value));
    const prettyLabel = (key) => fieldMeta[key]?.label || key;

    const ranges = Object.entries(fieldMeta).reduce((acc, [key, meta]) => {
        const min = Number.isFinite(meta.min) ? meta.min : 0;
        const max = Number.isFinite(meta.max) ? meta.max : 100;
        const step = Number.isFinite(meta.step) ? meta.step : 1;
        acc[key] = [min, max, step];
        return acc;
    }, {});

    const normalizeCategories = (rawCategories) => {
        if (!rawCategories || typeof rawCategories !== 'object') return null;

        const normalized = {};
        categoriesFromConfig.forEach(category => {
            const incomingCategory = rawCategories?.[category.Key] ?? {};
            const categoryValues = {};

            (Array.isArray(category.Fields) ? category.Fields : []).forEach(field => {
                const incoming = Number(incomingCategory?.[field.Key]);
                const fallback = Number.isFinite(Number(field.DefaultValue)) ? Number(field.DefaultValue) : 0;
                categoryValues[field.Key] = Number.isFinite(incoming) ? incoming : fallback;
            });

            normalized[category.Key] = categoryValues;
        });

        return normalized;
    };

    const readValue = (categories, category, key, fallback = 0) => {
        const value = categories?.[category]?.[key];
        const parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : fallback;
    };

    const renderMetrics = (card, scores) => {
        card.querySelectorAll('.setup-user-metric, .setup-metric').forEach(metric => {
            const metricKey = metric.getAttribute('data-metric');
            if (!metricKey || !(metricKey in scores)) return;

            const score = Math.round(scores[metricKey]);
            const bar = metric.querySelector('div > i, .setup-metric__bar i');
            const valueLabel = metric.querySelector('strong, .setup-metric__value');
            if (bar) bar.style.width = `${score}%`;
            if (valueLabel) valueLabel.textContent = String(score);
        });
    };

    const calcScores = (categories) => {
        if (metricConfig.length === 0) {
            return { topSpeed: 0, traction: 0, rotation: 0, stability: 0, tyreWear: 0 };
        }

        const scores = {};
        metricConfig.forEach(metric => {
            if (!metric?.Key) return;
            let value = Number(metric.BaseValue) || 0;
            (Array.isArray(metric.Terms) ? metric.Terms : []).forEach(term => {
                const cat = term?.Category;
                const field = term?.Field;
                const coefficient = Number(term?.Coefficient) || 0;
                if (!cat || !field) return;
                const fieldValue = readValue(categories, cat, field, 0);
                value += fieldValue * coefficient;
            });
            scores[metric.Key] = clamp(value);
        });

        return scores;
    };

    const buildEditor = (root, categories) => {
        const editor = document.createElement('div');
        editor.className = 'setup-ingame-editor';

        const menu = document.createElement('aside');
        menu.className = 'setup-ingame-editor__menu';

        const content = document.createElement('div');
        content.className = 'setup-ingame-editor__content';

        const categoryKeys = Object.keys(categories).filter(key => categories[key] && typeof categories[key] === 'object');
        if (categoryKeys.length === 0) return null;

        categoryKeys.forEach((categoryKey, index) => {
            const tab = document.createElement('button');
            tab.type = 'button';
            tab.className = `setup-ingame-tab ${index === 0 ? 'is-active' : ''}`;
            tab.dataset.target = categoryKey;
            const meta = categoryMeta[categoryKey];
            tab.textContent = `${meta?.icon ?? '•'} ${meta?.title ?? categoryKey}`;
            menu.appendChild(tab);

            const panel = document.createElement('div');
            panel.className = `setup-ingame-panel ${index === 0 ? 'is-active' : ''}`;
            panel.dataset.panel = categoryKey;

            Object.keys(categories[categoryKey]).forEach(key => {
                const value = Number(categories[categoryKey][key]);
                if (!Number.isFinite(value)) return;

                const [min, max, step] = ranges[key] || [0, 100, 1];
                const row = document.createElement('div');
                row.className = 'setup-ingame-row is-f1';
                row.dataset.key = key;

                const label = document.createElement('label');
                label.textContent = prettyLabel(key);

                const slider = document.createElement('input');
                slider.type = 'range';
                slider.min = String(min);
                slider.max = String(max);
                slider.step = String(step);
                slider.value = String(value);
                slider.disabled = false;

                const span = max - min;
                const pct = span > 0 ? Math.max(0, Math.min(100, ((value - min) / span) * 100)) : 0;
                slider.style.setProperty('--range-fill', pct + '%');

                const number = document.createElement('input');
                number.type = 'number';
                number.className = 'form-control form-control-sm';
                number.min = String(min);
                number.max = String(max);
                number.step = String(step);
                number.value = String(value);
                number.readOnly = false;
                number.tabIndex = 0;

                const valueChip = document.createElement('span');
                valueChip.className = 'setup-ingame-value';
                valueChip.textContent = String(value);

                const hint = document.createElement('span');
                hint.className = 'setup-ingame-hint';
                hint.textContent = `${min} - ${max}`;

                row.appendChild(label);
                row.appendChild(slider);
                row.appendChild(number);
                row.appendChild(valueChip);
                row.appendChild(hint);
                panel.appendChild(row);
            });

            content.appendChild(panel);
        });

        editor.appendChild(menu);
        editor.appendChild(content);
        root.innerHTML = '';
        root.appendChild(editor);

        const tabs = Array.from(editor.querySelectorAll('.setup-ingame-tab'));
        const panels = Array.from(editor.querySelectorAll('.setup-ingame-panel'));

        tabs.forEach(tab => {
            tab.addEventListener('click', (ev) => {
                ev.stopPropagation();
                const target = tab.dataset.target;
                tabs.forEach(t => t.classList.toggle('is-active', t === tab));
                panels.forEach(p => p.classList.toggle('is-active', p.dataset.panel === target));
            });
        });

        return editor;
    };

    cards.forEach(card => {
        const raw = card.getAttribute('data-setup-json');
        if (!raw) return;

        let categories = null;
        try {
            const payload = JSON.parse(raw);
            const rawCategories = payload?.categories || payload;
            categories = normalizeCategories(rawCategories);
        } catch {
            return;
        }

        if (!categories) return;

        // Deep-copy for reset
        const originalCategories = JSON.parse(JSON.stringify(categories));

        const root = card.querySelector('[data-setup-root]');
        if (root) root.hidden = true;

        // Metriken sofort (billig: nur Rechnen) — aber den schweren Slider-Editor NICHT
        // beim Laden bauen. Sonst blockiert pro Karte das Aufbauen des DOM den Main-Thread,
        // genau wenn man nach dem Laden zu scrollen beginnt. Der Editor wird beim ersten
        // Aufklappen lazy gebaut (siehe ensureEditor()).
        renderMetrics(card, calcScores(categories));

        let editorBuilt = false;

        // ── Helpers ──────────────────────────────────────────────────────
        const updateFill = (slider) => {
            const min = Number(slider.min), max = Number(slider.max), val = Number(slider.value);
            const pct = max > min ? ((val - min) / (max - min)) * 100 : 0;
            slider.style.setProperty('--range-fill', pct + '%');
        };

        const readCategoriesFromDom = () => {
            const live = JSON.parse(JSON.stringify(originalCategories));
            if (!root) return live;
            root.querySelectorAll('.setup-ingame-row[data-key]').forEach(row => {
                const key = row.dataset.key;
                const cat = Object.keys(live).find(c => key in live[c]);
                if (!cat) return;
                const slider = row.querySelector('input[type="range"]');
                if (slider) live[cat][key] = Number(slider.value);
            });
            return live;
        };

        const resetCard = () => {
            if (!root) return;
            root.querySelectorAll('.setup-ingame-row[data-key]').forEach(row => {
                const key = row.dataset.key;
                const cat = Object.keys(originalCategories).find(c => key in originalCategories[c]);
                if (!cat) return;
                const orig = originalCategories[cat][key];
                const slider = row.querySelector('input[type="range"]');
                const number = row.querySelector('input[type="number"]');
                const chip   = row.querySelector('.setup-ingame-value');
                if (slider) { slider.value = orig; updateFill(slider); }
                if (number) number.value = orig;
                if (chip)   chip.textContent = orig;
            });
            renderMetrics(card, calcScores(readCategoriesFromDom()));
        };

        // ── Live-Updates verdrahten (erst nach dem Bauen des Editors) ─────
        const wireRows = () => {
            root.querySelectorAll('.setup-ingame-row[data-key]').forEach(row => {
                const slider = row.querySelector('input[type="range"]');
                const number = row.querySelector('input[type="number"]');
                const chip   = row.querySelector('.setup-ingame-value');

                if (slider) {
                    slider.addEventListener('input', () => {
                        if (number) number.value = slider.value;
                        if (chip)   chip.textContent = slider.value;
                        updateFill(slider);
                        renderMetrics(card, calcScores(readCategoriesFromDom()));
                    });
                }
                if (number) {
                    number.addEventListener('change', () => {
                        const min = Number(slider?.min ?? 0), max = Number(slider?.max ?? 100);
                        const v = Math.min(max, Math.max(min, Number(number.value)));
                        number.value = v;
                        if (slider) { slider.value = v; updateFill(slider); }
                        if (chip) chip.textContent = v;
                        renderMetrics(card, calcScores(readCategoriesFromDom()));
                    });
                }
            });
        };

        // Editor (Slider-DOM + Wiring) genau einmal bei Bedarf bauen.
        const ensureEditor = () => {
            if (editorBuilt || !root) return;
            buildEditor(root, categories);
            wireRows();
            editorBuilt = true;
        };

        // Reset-Button: nur sinnvoll, wenn der Editor schon gebaut wurde (vorher nichts verändert).
        const resetBtn = card.querySelector('[data-sandbox-reset]');
        if (resetBtn) {
            resetBtn.addEventListener('click', ev => {
                ev.stopPropagation();
                if (editorBuilt) resetCard();
            });
        }

        // ── Expand/Collapse ──────────────────────────────────────────────
        // A11y: Die Karte ist KEIN role="button" mehr — sie enthält Formulare,
        // Slider und verschachtelte Buttons, und ein Button-Role um interaktive
        // Inhalte ist ein ARIA-Antipattern. Stattdessen steuert ein dedizierter,
        // fokussierbarer Toggle-Button die Sichtbarkeit; der Klick auf die
        // Kartenfläche bleibt als reine Maus-Bequemlichkeit erhalten.
        card.classList.add('setup-card--collapsible');

        const toggleBtn = card.querySelector('[data-setup-toggle]');
        const toggleLabel = toggleBtn?.querySelector('[data-toggle-label]');

        const setExpanded = (expand) => {
            if (!root) return;
            if (expand) ensureEditor();   // Slider-Editor lazy bauen, bevor er sichtbar wird
            card.classList.toggle('is-expanded', expand);
            root.hidden = !expand;
            if (toggleBtn) toggleBtn.setAttribute('aria-expanded', expand ? 'true' : 'false');
            if (toggleLabel) toggleLabel.textContent = expand ? 'Regler ausblenden' : 'Regler anzeigen';
        };

        const toggle = (force) => {
            const expand = typeof force === 'boolean' ? force : !card.classList.contains('is-expanded');
            setExpanded(expand);
        };

        // Dedizierter Button = barrierefreie Bedienung (Tastatur/Screenreader).
        if (toggleBtn) {
            toggleBtn.addEventListener('click', (ev) => { ev.stopPropagation(); toggle(); });
        }

        // Klick auf die Kartenfläche (außerhalb interaktiver Elemente) togglet ebenfalls — Maus-Komfort.
        card.addEventListener('click', (ev) => {
            const interactive = ev.target.closest('a, button, input, select, textarea, label, .setup-ingame-tab, .setup-card__tools, [data-no-toggle]');
            if (interactive && card.contains(interactive)) return;
            toggle();
        });
    });
})();
