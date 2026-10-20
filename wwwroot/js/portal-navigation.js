(() => {
    'use strict';

    const links = Array.from(document.querySelectorAll('nav.main a[href^="#"]'));
    const activate = hash => {
        const selected = links.find(link => link.hash === hash);
        if (!selected) return;
        links.forEach(link => {
            const active = link === selected;
            link.classList.toggle('active', active);
            if (active) link.setAttribute('aria-current', 'location');
            else link.removeAttribute('aria-current');
        });
    };

    links.forEach(link => link.addEventListener('click', event => {
        if (event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
        activate(link.hash);
    }));
    window.addEventListener('hashchange', () => activate(location.hash || '#main'));
    activate(location.hash || '#main');
})();
