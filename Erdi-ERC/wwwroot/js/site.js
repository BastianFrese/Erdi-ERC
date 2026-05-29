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

        window.addEventListener('resize', positionMenu, { passive: true });
        window.addEventListener('scroll', positionMenu, true);
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
