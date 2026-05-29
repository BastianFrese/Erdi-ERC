(() => {
    const storageKey = 'erc.intro.video.seen.v1';
    const overlay = document.getElementById('introVideoOverlay');
    const player = document.getElementById('introVideoPlayer');
    const closeBtn = document.getElementById('introVideoClose');
    const replayBtn = document.getElementById('introVideoReplay');
    const guideOverlay = document.getElementById('introGuideOverlay');
    const guideCloseBtn = document.getElementById('introGuideClose');
    if (!overlay || !player || !closeBtn) return;

    let showGuideOnClose = false;

    const hideGuide = () => {
        if (!guideOverlay) return;
        guideOverlay.style.display = 'none';
        guideOverlay.setAttribute('aria-hidden', 'true');
    };

    const showGuide = () => {
        if (!guideOverlay) return;
        guideOverlay.style.display = 'flex';
        guideOverlay.setAttribute('aria-hidden', 'false');
    };

    const hideOverlay = (markSeen) => {
        overlay.style.display = 'none';
        overlay.setAttribute('aria-hidden', 'true');
        player.pause();
        if (markSeen) {
            localStorage.setItem(storageKey, '1');
        }

        if (showGuideOnClose) {
            showGuide();
            showGuideOnClose = false;
        }
    };

    const showOverlay = (forceWithSound, withGuideAfterClose) => {
        hideGuide();
        showGuideOnClose = withGuideAfterClose;
        overlay.style.display = 'flex';
        overlay.setAttribute('aria-hidden', 'false');
        player.currentTime = 0;
        player.muted = !forceWithSound;
        player.play().catch(() => { });
    };

    if (!localStorage.getItem(storageKey)) {
        showOverlay(false, true);
    }

    closeBtn.addEventListener('click', () => hideOverlay(true));
    replayBtn?.addEventListener('click', () => showOverlay(true, true));
    overlay.addEventListener('click', (event) => {
        if (event.target === overlay) {
            hideOverlay(true);
        }
    });
    player.addEventListener('ended', () => hideOverlay(true));
    guideCloseBtn?.addEventListener('click', hideGuide);
    guideOverlay?.addEventListener('click', (event) => {
        if (event.target === guideOverlay) {
            hideGuide();
        }
    });
    document.addEventListener('keydown', (event) => {
        if (event.key === 'Escape' && overlay.style.display !== 'none') {
            hideOverlay(true);
            return;
        }

        if (event.key === 'Escape' && guideOverlay && guideOverlay.style.display !== 'none') {
            hideGuide();
        }
    });
})();

(() => {
    const countdown = document.getElementById('nextStreamCountdown');
    if (countdown) {
        const target = new Date(countdown.dataset.target || '');
        if (!Number.isNaN(target.getTime())) {
            const renderCountdown = () => {
                const diff = target.getTime() - Date.now();
                if (diff <= 0) {
                    countdown.textContent = 'Der Stream läuft oder hat bereits begonnen.';
                    return;
                }

                const totalSeconds = Math.floor(diff / 1000);
                const days = Math.floor(totalSeconds / 86400);
                const hours = Math.floor((totalSeconds % 86400) / 3600);
                const minutes = Math.floor((totalSeconds % 3600) / 60);
                const seconds = totalSeconds % 60;
                countdown.textContent = `${days}T ${hours}Std ${minutes}Min ${seconds}Sek`;
            };

            renderCountdown();
            window.setInterval(renderCountdown, 1000);
        }
    }
})();

(() => {
    const countdown = document.getElementById('nextEventStatCountdown');
    if (!countdown) return;

    const target = new Date(countdown.dataset.target || '');
    if (Number.isNaN(target.getTime())) return;

    const renderCountdown = () => {
        const diff = target.getTime() - Date.now();
        if (diff <= 0) {
            countdown.textContent = 'Startet jetzt oder läuft bereits';
            return;
        }

        const totalSeconds = Math.floor(diff / 1000);
        const days = Math.floor(totalSeconds / 86400);
        const hours = Math.floor((totalSeconds % 86400) / 3600);
        const minutes = Math.floor((totalSeconds % 3600) / 60);

        if (days > 0) {
            countdown.textContent = `In ${days}T ${hours}Std ${minutes}Min`;
            return;
        }

        const seconds = totalSeconds % 60;
        countdown.textContent = `In ${hours}Std ${minutes}Min ${seconds}Sek`;
    };

    renderCountdown();
    window.setInterval(renderCountdown, 1000);
})();

(function () {
    const panel = document.querySelector('.js-winner-slideshow');
    if (!panel) return;

    const slides = Array.from(panel.querySelectorAll('.js-winner-slide'));
    const dots = Array.from(panel.querySelectorAll('.winner-dot'));
    const progress = panel.querySelector('.js-winner-progress');
    const slidesWrap = panel.querySelector('.js-winner-slides');
    if (slides.length <= 1) return;

    let currentIndex = slides.findIndex(s => s.classList.contains('is-active'));
    if (currentIndex < 0) currentIndex = 0;

    const intervalMs = 4600;
    let timer = null;

    const measureHeight = (slide) => {
        const prevStyle = {
            position: slide.style.position,
            inset: slide.style.inset,
            opacity: slide.style.opacity,
            pointerEvents: slide.style.pointerEvents,
            display: slide.style.display
        };

        slide.style.position = 'relative';
        slide.style.inset = 'auto';
        slide.style.opacity = '1';
        slide.style.pointerEvents = 'none';
        slide.style.display = '';
        const h = slide.offsetHeight;
        slide.style.position = prevStyle.position;
        slide.style.inset = prevStyle.inset;
        slide.style.opacity = prevStyle.opacity;
        slide.style.pointerEvents = prevStyle.pointerEvents;
        slide.style.display = prevStyle.display;
        return h;
    };

    const syncContainerHeight = (index) => {
        if (!slidesWrap) return;
        const targetHeight = measureHeight(slides[index]);
        slidesWrap.style.height = `${targetHeight}px`;
    };

    const updateDots = () => {
        dots.forEach((d, i) => d.classList.toggle('is-active', i === currentIndex));
    };

    const restartProgress = () => {
        if (!progress) return;
        progress.style.transition = 'none';
        progress.style.width = '0%';
        requestAnimationFrame(() => {
            requestAnimationFrame(() => {
                progress.style.transition = `width ${intervalMs}ms linear`;
                progress.style.width = '100%';
            });
        });
    };

    const showSlide = (nextIndex) => {
        if (nextIndex === currentIndex) return;

        const current = slides[currentIndex];
        const next = slides[nextIndex];

        syncContainerHeight(nextIndex);

        current.classList.remove('is-active');
        current.classList.add('is-leave');

        next.classList.remove('is-hidden');
        next.classList.add('is-enter');

        requestAnimationFrame(() => {
            next.classList.remove('is-enter');
            next.classList.add('is-active');
        });

        setTimeout(() => {
            current.classList.remove('is-leave');
            current.classList.add('is-hidden');
        }, 620);

        currentIndex = nextIndex;
        updateDots();
        restartProgress();
    };

    const start = () => {
        clearInterval(timer);
        timer = setInterval(() => {
            const nextIndex = (currentIndex + 1) % slides.length;
            showSlide(nextIndex);
        }, intervalMs);
        restartProgress();
    };

    panel.addEventListener('mouseenter', () => {
        clearInterval(timer);
        if (progress) {
            const computedWidth = window.getComputedStyle(progress).width;
            progress.style.transition = 'none';
            progress.style.width = computedWidth;
        }
    });

    panel.addEventListener('mouseleave', () => {
        start();
    });

    dots.forEach((dot, i) => {
        dot.addEventListener('click', () => {
            showSlide(i);
            start();
        });
    });

    window.addEventListener('resize', () => syncContainerHeight(currentIndex));

    syncContainerHeight(currentIndex);
    updateDots();
    start();
})();

(() => {
    const countdown = document.getElementById('raceHudCountdown');
    if (!countdown) return;

    const target = new Date(countdown.dataset.target || '');
    if (Number.isNaN(target.getTime())) {
        countdown.textContent = 'TBA';
        return;
    }

    const renderCountdown = () => {
        const diff = target.getTime() - Date.now();
        if (diff <= 0) {
            countdown.textContent = 'LIVE';
            return;
        }

        const totalSeconds = Math.floor(diff / 1000);
        const days = Math.floor(totalSeconds / 86400);
        const hours = Math.floor((totalSeconds % 86400) / 3600);
        const minutes = Math.floor((totalSeconds % 3600) / 60);
        const seconds = totalSeconds % 60;

        if (days > 0) {
            countdown.textContent = `${days}T ${hours}H ${minutes}M`;
            return;
        }

        countdown.textContent = `${hours.toString().padStart(2, '0')}:${minutes.toString().padStart(2, '0')}:${seconds.toString().padStart(2, '0')}`;
    };

    renderCountdown();
    window.setInterval(renderCountdown, 1000);
})();
