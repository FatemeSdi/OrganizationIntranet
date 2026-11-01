/*
 * هایلایت منوی هدر صفحه‌ی اصلی پورتال هنگام اسکرول (scroll-spy).
 * هر لینک هدر که به یک بخش صفحه اشاره کند (href="#id" یا data-spy-target="#id") وقتی آن بخش به زیر هدر برسد «فعال» می‌شود؛
 * بالای صفحه «خانه» فعال است و ته صفحه آخرین بخش. با کلیک روی لینک هم بلافاصله فعال می‌شود.
 * ترتیب بخش‌ها از روی جای واقعی آن‌ها در صفحه حساب می‌شود، نه ترتیب لینک‌ها.
 */
(function () {
    'use strict';
    var nav = document.querySelector('nav.main');
    if (!nav) return;
    var links = Array.prototype.slice.call(nav.querySelectorAll('a'));
    var home = null;
    var sections = [];
    links.forEach(function (a) {
        var target = a.getAttribute('data-spy-target') || a.getAttribute('href') || '';
        if (target === '#main' || a.hasAttribute('data-spy-home')) { home = a; return; }
        if (target.charAt(0) !== '#' || target.length < 2) return;
        var el = document.getElementById(target.slice(1));
        if (el) sections.push({ link: a, el: el });
    });
    if (!sections.length) return;

    var header = document.querySelector('header.site');
    var ticking = false;
    var current = null;

    function offset() { return (header ? header.offsetHeight : 0) + 32; }
    // جای واقعی بخش در صفحه؛ offsetTop به‌خاطر offsetParentهای متفاوت قابل مقایسه نیست.
    function top(el) { return el.getBoundingClientRect().top + window.pageYOffset; }

    function setActive(link) {
        if (link === current) return;
        current = link;
        links.forEach(function (a) { a.classList.remove('active'); a.removeAttribute('aria-current'); });
        if (link) { link.classList.add('active'); link.setAttribute('aria-current', 'location'); }
    }

    function update() {
        ticking = false;
        var line = offset();
        var active = home;
        // بخشی که بالای آن از خط زیر هدر گذشته؛ آخرین مورد به ترتیب جای واقعی در صفحه.
        sections.slice().sort(function (x, y) { return top(x.el) - top(y.el); }).forEach(function (s) {
            if (s.el.getBoundingClientRect().top <= line) active = s.link;
        });
        var atBottom = window.innerHeight + window.pageYOffset >= document.documentElement.scrollHeight - 2;
        if (atBottom) {
            var last = sections.slice().sort(function (x, y) { return top(y.el) - top(x.el); })[0];
            if (last) active = last.link;
        }
        setActive(active);
    }

    function schedule() {
        // تایمر کوتاه به‌جای requestAnimationFrame: در تب پس‌زمینه یا پنجره‌ی مخفی rAF اجرا نمی‌شود و فوکوس منو به‌روز نمی‌ماند.
        if (!ticking) { ticking = true; window.setTimeout(update, 30); }
    }

    window.addEventListener('scroll', schedule, { passive: true });
    window.addEventListener('resize', schedule);
    window.addEventListener('load', update);
    links.forEach(function (a) {
        a.addEventListener('click', function () {
            var isSpyLink = a === home || sections.some(function (s) { return s.link === a; });
            if (isSpyLink && (a.getAttribute('href') || '').charAt(0) === '#') setActive(a);
        });
    });
    update();
})();
