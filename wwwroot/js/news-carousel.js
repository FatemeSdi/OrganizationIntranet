// اسلایدر «اخبار سازمان» در صفحه‌ی اصلی پورتال: هر چند ثانیه روی کارت بعدی متمرکز می‌شود.
// با hover، فوکوس صفحه‌کلید یا پنهان‌بودن تب متوقف می‌شود و با prefers-reduced-motion خودکار حرکت نمی‌کند.
(() => {
    'use strict';
    const root = document.querySelector('[data-news-carousel]');
    if (!root) return;
    const track = root.querySelector('.news-track');
    const cards = Array.from(track.querySelectorAll('.news-card'));
    const dots = Array.from(root.querySelectorAll('[data-news-dot]'));
    const section = root.closest('section');
    const prev = section.querySelector('[data-news-prev]');
    const next = section.querySelector('[data-news-next]');
    const reduced = window.matchMedia('(prefers-reduced-motion: reduce)');
    const INTERVAL = 6000;
    let active = 0, timer = null, paused = false, settle = null;

    function setActive(index) {
        active = index;
        cards.forEach((card, i) => card.classList.toggle('is-active', i === index));
        dots.forEach((dot, i) => dot.setAttribute('aria-current', String(i === index)));
    }
    // فقط track افقی جابه‌جا می‌شود (نه کل صفحه)؛ فاصله‌ی نسبی مرکزها در RTL و LTR یکسان کار می‌کند.
    function go(index, smooth = true) {
        index = (index + cards.length) % cards.length;
        const card = cards[index].getBoundingClientRect(), box = track.getBoundingClientRect();
        track.scrollBy({ left: (card.left + card.width / 2) - (box.left + box.width / 2), behavior: smooth && !reduced.matches ? 'smooth' : 'auto' });
        setActive(index);
    }
    function nearest() {
        const box = track.getBoundingClientRect(), center = box.left + box.width / 2;
        let best = 0, distance = Infinity;
        cards.forEach((card, i) => {
            const r = card.getBoundingClientRect(), d = Math.abs(r.left + r.width / 2 - center);
            if (d < distance) { distance = d; best = i; }
        });
        return best;
    }
    function schedule() {
        clearInterval(timer);
        if (cards.length < 2 || reduced.matches) return;
        timer = setInterval(() => { if (!paused && !document.hidden) go(active + 1); }, INTERVAL);
    }

    track.addEventListener('scroll', () => {
        clearTimeout(settle);
        settle = setTimeout(() => setActive(nearest()), 120);
    }, { passive: true });
    prev?.addEventListener('click', () => { go(active - 1); schedule(); });
    next?.addEventListener('click', () => { go(active + 1); schedule(); });
    dots.forEach((dot, i) => dot.addEventListener('click', () => { go(i); schedule(); }));
    track.addEventListener('keydown', event => {
        // در RTL «بعدی» سمت چپ است.
        if (event.key === 'ArrowLeft') { event.preventDefault(); go(active + 1); schedule(); }
        if (event.key === 'ArrowRight') { event.preventDefault(); go(active - 1); schedule(); }
    });
    root.addEventListener('mouseenter', () => { paused = true; });
    root.addEventListener('mouseleave', () => { paused = false; });
    root.addEventListener('focusin', () => { paused = true; });
    root.addEventListener('focusout', event => { if (!root.contains(event.relatedTarget)) paused = false; });
    reduced.addEventListener?.('change', schedule);

    go(0, false);
    schedule();
})();
