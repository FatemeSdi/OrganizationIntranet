// توست پورتال اینترانت (هم‌خوان با تم سرمه‌ای/طلایی): window.portalToast(متن، 'success' | 'danger' | 'info').
// پیام‌های سرور در عناصر پنهان [data-toast] قرار می‌گیرند و هنگام بارگذاری نمایش داده می‌شوند. متن هرگز به‌صورت HTML درج نمی‌شود.
(() => {
    'use strict';
    const TITLES = { success: 'انجام شد', danger: 'خطا', info: 'پیام' };
    const ICONS = {
        success: 'M20 6 9 17l-5-5',
        danger: 'M12 8v5M12 16.5v.5M10.3 3.9 1.8 18a2 2 0 0 0 1.7 3h17a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z',
        info: 'M12 16v-4M12 8h.01M12 21a9 9 0 1 0 0-18 9 9 0 0 0 0 18Z'
    };
    function container() {
        let box = document.getElementById('portalToasts');
        if (!box) {
            box = document.createElement('div');
            box.id = 'portalToasts';
            box.className = 'pt-stack';
            document.body.appendChild(box);
        }
        return box;
    }
    function svg(path, cls) {
        const ns = 'http://www.w3.org/2000/svg';
        const icon = document.createElementNS(ns, 'svg');
        icon.setAttribute('viewBox', '0 0 24 24'); icon.setAttribute('fill', 'none'); icon.setAttribute('stroke', 'currentColor');
        icon.setAttribute('stroke-width', '2'); icon.setAttribute('stroke-linecap', 'round'); icon.setAttribute('stroke-linejoin', 'round');
        icon.setAttribute('aria-hidden', 'true'); if (cls) icon.setAttribute('class', cls);
        const p = document.createElementNS(ns, 'path'); p.setAttribute('d', path); icon.appendChild(p);
        return icon;
    }
    function portalToast(message, type = 'success', timeout = 5000) {
        if (!message) return;
        type = TITLES[type] ? type : 'info';
        const toast = document.createElement('div');
        toast.className = `pt-toast pt-${type}`;
        toast.setAttribute('role', type === 'danger' ? 'alert' : 'status');
        toast.appendChild(svg(ICONS[type], 'pt-icon'));
        const body = document.createElement('div');
        body.className = 'pt-body';
        const title = document.createElement('b'); title.textContent = TITLES[type];
        const text = document.createElement('span'); text.textContent = message;
        body.appendChild(title); body.appendChild(text);
        toast.appendChild(body);
        const close = document.createElement('button');
        close.type = 'button'; close.className = 'pt-close'; close.setAttribute('aria-label', 'بستن پیام');
        close.appendChild(svg('M6 6l12 12M18 6 6 18'));
        toast.appendChild(close);
        const hide = () => { toast.classList.add('pt-hide'); setTimeout(() => toast.remove(), 220); };
        close.addEventListener('click', hide);
        let timer = setTimeout(hide, type === 'danger' ? timeout + 3000 : timeout);
        toast.addEventListener('mouseenter', () => clearTimeout(timer));
        toast.addEventListener('mouseleave', () => { timer = setTimeout(hide, 2500); });
        container().appendChild(toast);
    }
    window.portalToast = portalToast;
    const show = () => {
        document.querySelectorAll("[data-toast]").forEach(el => { portalToast(el.textContent.trim(), el.dataset.toast); el.remove(); });
        // خطاهای اعتبارسنجی سرور (asp-validation-summary) هم به‌صورت توست؛ کادر خلاصه پنهان می‌شود.
        document.querySelectorAll(".validation-summary-errors").forEach(summary => {
            summary.querySelectorAll("li").forEach(li => portalToast(li.textContent.trim(), "danger"));
            summary.hidden = true;
        });
    };
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', show); else show();
})();
