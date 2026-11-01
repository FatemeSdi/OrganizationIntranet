// انتخاب‌گرهای پنل مدیریت: ۱) انتخاب کاربر/نقش با فهرست کشویی قابل جست‌وجو و موارد انتخاب‌شده به‌صورت رادیوباتن؛
// ۲) انتخاب آیکن با نمایش خود آیکن. فقط با createElement/textContent ساخته می‌شوند (هیچ innerHTML با داده‌ی کاربر نیست).
// منطق ثبت لحظه‌ی انتخاب‌ها در سرور هم بررسی می‌شود؛ این فایل فقط رابط کاربری است.
(function () {
    'use strict';

    function normalize(text) {
        return (text || '').toString().trim().toLowerCase()
            .replace(/[۰-۹]/g, function (d) { return String(d.charCodeAt(0) - 1776); })
            .replace(/[٠-٩]/g, function (d) { return String(d.charCodeAt(0) - 1632); })
            .replace(/ي/g, 'ی').replace(/ك/g, 'ک').replace(/‌/g, ' ');
    }
    function toast(message, type) { if (typeof window.showToast === 'function') window.showToast(message, type); }
    function antiforgeryToken() {
        var field = document.querySelector('input[name="__RequestVerificationToken"]');
        return field ? field.value : '';
    }
    function el(tag, className, text) {
        var node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined && text !== null) node.textContent = text;
        return node;
    }
    var radioCounter = 0;

    // ---------- انتخاب کاربر/نقش ----------
    function initPicker(root) {
        var field = root.getAttribute('data-field');
        var kind = root.getAttribute('data-kind');
        var noun = root.getAttribute('data-noun') || '';
        var instantUrl = root.getAttribute('data-instant-url');
        var input = root.querySelector('[data-picker-input]');
        var menu = root.querySelector('[data-picker-menu]');
        var selected = root.querySelector('[data-picker-selected]');
        var count = root.querySelector('[data-pick-count]');
        var empty = root.querySelector('[data-picker-empty]');
        var none = root.querySelector('[data-picker-none]');
        if (!input || !menu || !selected) return;
        var activeIndex = -1;
        var busy = false;

        function options() { return Array.prototype.slice.call(menu.querySelectorAll('li[data-id]')); }
        function visibleOptions() { return options().filter(function (o) { return !o.hidden; }); }
        function rowFor(id) { return selected.querySelector('li[data-id="' + id + '"]'); }
        function optionFor(id) { return menu.querySelector('li[data-id="' + id + '"]'); }

        function refreshState() {
            var n = selected.querySelectorAll('li[data-id]').length;
            if (count) count.textContent = n.toLocaleString('fa-IR');
            if (empty) empty.hidden = n > 0;
        }
        function setActive(index) {
            var list = visibleOptions();
            list.forEach(function (o) { o.classList.remove('is-active'); o.removeAttribute('aria-selected'); });
            activeIndex = list.length ? (index + list.length) % list.length : -1;
            if (activeIndex >= 0) {
                list[activeIndex].classList.add('is-active');
                list[activeIndex].setAttribute('aria-selected', 'true');
                if (list[activeIndex].scrollIntoView) list[activeIndex].scrollIntoView({ block: 'nearest' });
            }
        }
        function filter() {
            var query = normalize(input.value);
            options().forEach(function (o) {
                var chosen = !!rowFor(o.getAttribute('data-id'));
                o.hidden = chosen || (query !== '' && normalize(o.getAttribute('data-search')).indexOf(query) === -1);
            });
            if (none) none.hidden = visibleOptions().length > 0;
            setActive(0);
        }
        function openMenu() {
            filter();
            menu.hidden = false;
            input.setAttribute('aria-expanded', 'true');
        }
        function closeMenu() {
            menu.hidden = true;
            input.setAttribute('aria-expanded', 'false');
            activeIndex = -1;
        }

        function buildRow(option) {
            var id = option.getAttribute('data-id');
            var li = el('li', 'entity-picker__item');
            li.setAttribute('data-id', id);
            var label = document.createElement('label');
            var radio = el('input', 'form-check-input m-0');
            radio.type = 'radio';
            radio.checked = true;
            radio.name = 'picked-' + (++radioCounter);
            radio.setAttribute('aria-label', 'برداشتن ' + option.getAttribute('data-label'));
            var text = el('span', 'entity-picker__text');
            text.appendChild(el('span', 'd-block text-truncate', option.getAttribute('data-label')));
            if (option.getAttribute('data-sub')) {
                var sub = el('small', 'd-block text-muted text-truncate', option.getAttribute('data-sub'));
                sub.setAttribute('dir', 'ltr');
                text.appendChild(sub);
            }
            label.appendChild(radio);
            label.appendChild(text);
            if (option.getAttribute('data-inactive')) label.appendChild(el('span', 'status-pill status-pill--muted ms-auto', 'غیرفعال'));
            var hidden = el('input');
            hidden.type = 'hidden';
            hidden.name = field;
            hidden.value = id;
            li.appendChild(label);
            li.appendChild(hidden);
            return li;
        }

        // ثبت لحظه‌ای در سرور؛ در خطا وضعیت قبلی برمی‌گردد و toast خطا نشان داده می‌شود.
        function send(granted, id, label, done) {
            if (!instantUrl) { done(true); return; }
            busy = true;
            root.classList.add('is-busy');
            root.setAttribute('aria-busy', 'true');
            fetch(instantUrl, {
                method: 'POST',
                credentials: 'same-origin',
                headers: { 'Content-Type': 'application/json', 'RequestVerificationToken': antiforgeryToken(), 'Accept': 'application/json' },
                body: JSON.stringify({ kind: kind, id: Number(id), granted: granted })
            }).then(function (response) {
                return response.json().catch(function () { return {}; }).then(function (body) { return { ok: response.ok && body.ok !== false, body: body, status: response.status }; });
            }).then(function (result) {
                if (result.ok) toast('«' + label + '» ' + (granted ? 'اضافه شد و ثبت شد.' : 'برداشته شد و ثبت شد.'), 'success');
                else toast(result.body && result.body.message ? result.body.message : 'ثبت انجام نشد (کد ' + result.status + '). صفحه را تازه کنید و دوباره تلاش کنید.', 'danger');
                done(result.ok);
            }).catch(function () {
                toast('ارتباط با سرور برقرار نشد؛ تغییر ثبت نشد.', 'danger');
                done(false);
            }).then(function () {
                busy = false;
                root.classList.remove('is-busy');
                root.removeAttribute('aria-busy');
            });
        }

        function add(option) {
            if (busy) return;
            var id = option.getAttribute('data-id');
            var label = option.getAttribute('data-label');
            if (rowFor(id)) return;
            var row = buildRow(option);
            selected.insertBefore(row, selected.firstChild);
            option.hidden = true;
            refreshState();
            send(true, id, label, function (ok) {
                if (!ok) { row.remove(); option.hidden = false; refreshState(); }
            });
        }
        function remove(row) {
            if (busy) return;
            var id = row.getAttribute('data-id');
            var option = optionFor(id);
            var label = option ? option.getAttribute('data-label') : id;
            var next = row.nextSibling;
            row.remove();
            if (option) option.hidden = false;
            refreshState();
            send(false, id, label, function (ok) {
                if (!ok) { selected.insertBefore(row, next); if (option) option.hidden = true; refreshState(); }
            });
        }

        var toggle = root.querySelector('[data-picker-toggle]');
        if (toggle) toggle.addEventListener('mousedown', function (e) { e.preventDefault(); }); // فوکوس کادر نپرد تا کلیک فلش دوباره بازش نکند
        if (toggle) toggle.addEventListener('click', function () {
            if (menu.hidden) { input.focus(); openMenu(); } else { closeMenu(); }
        });
        input.addEventListener('focus', openMenu);
        input.addEventListener('click', openMenu);
        input.addEventListener('input', function () { if (menu.hidden) openMenu(); else filter(); });
        input.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown') { e.preventDefault(); if (menu.hidden) openMenu(); else setActive(activeIndex + 1); }
            else if (e.key === 'ArrowUp') { e.preventDefault(); if (menu.hidden) openMenu(); else setActive(activeIndex - 1); }
            else if (e.key === 'Enter') {
                var list = visibleOptions();
                if (!menu.hidden && activeIndex >= 0 && list[activeIndex]) { e.preventDefault(); add(list[activeIndex]); input.value = ''; filter(); }
                else if (e.key === 'Enter') e.preventDefault(); // Enter در این کادر فرم را ارسال نکند
            }
            else if (e.key === 'Escape') { closeMenu(); }
        });
        menu.addEventListener('mousedown', function (e) { e.preventDefault(); }); // فوکوس کادر جست‌وجو نپرد
        menu.addEventListener('click', function (e) {
            var option = e.target.closest('li[data-id]');
            if (!option) return;
            add(option);
            input.value = '';
            filter();
            input.focus();
        });
        selected.addEventListener('click', function (e) {
            var row = e.target.closest('li[data-id]');
            if (!row) return;
            e.preventDefault(); // رادیوی تیک‌خورده با کلیک «برداشته» می‌شود، نه اینکه بماند
            remove(row);
        });
        document.addEventListener('click', function (e) { if (!root.contains(e.target)) closeMenu(); });
        root.addEventListener('focusout', function () {
            setTimeout(function () { if (!root.contains(document.activeElement)) closeMenu(); }, 0);
        });
        refreshState();
    }
    document.querySelectorAll('[data-picker]').forEach(initPicker);

    // ---------- انتخاب آیکن با نمایش خود آیکن ----------
    var SVG_NS = 'http://www.w3.org/2000/svg';
    function iconSvg(sprite, id) {
        var svg = document.createElementNS(SVG_NS, 'svg');
        svg.setAttribute('viewBox', '0 0 24 24');
        svg.setAttribute('fill', 'none');
        svg.setAttribute('stroke', 'currentColor');
        svg.setAttribute('stroke-width', '1.8');
        svg.setAttribute('stroke-linecap', 'round');
        svg.setAttribute('stroke-linejoin', 'round');
        svg.setAttribute('aria-hidden', 'true');
        var use = document.createElementNS(SVG_NS, 'use');
        use.setAttribute('href', sprite + '#' + id);
        svg.appendChild(use);
        return svg;
    }
    function initIconPicker(select) {
        var sprite = select.getAttribute('data-icon-sprite') || '/assets/images/application-icons.svg';
        var wrap = el('div', 'icon-picker');
        var button = el('button', 'icon-picker__button form-select');
        button.type = 'button';
        button.setAttribute('aria-haspopup', 'listbox');
        button.setAttribute('aria-expanded', 'false');
        var listbox = el('ul', 'icon-picker__list');
        listbox.setAttribute('role', 'listbox');
        listbox.hidden = true;
        var describedBy = select.id;
        if (describedBy) { listbox.id = describedBy + '-list'; button.setAttribute('aria-controls', listbox.id); }

        function paintButton() {
            var option = select.options[select.selectedIndex];
            while (button.firstChild) button.removeChild(button.firstChild);
            var holder = el('span', 'icon-picker__icon');
            holder.appendChild(iconSvg(sprite, option.value));
            button.appendChild(holder);
            button.appendChild(el('span', 'icon-picker__label', option.textContent));
        }
        function close() { listbox.hidden = true; button.setAttribute('aria-expanded', 'false'); }
        function choose(value) {
            select.value = value;
            select.dispatchEvent(new Event('change', { bubbles: true }));
            paintButton();
            Array.prototype.forEach.call(listbox.children, function (li) { li.setAttribute('aria-selected', li.getAttribute('data-value') === value ? 'true' : 'false'); });
            close();
            button.focus();
        }
        Array.prototype.forEach.call(select.options, function (option) {
            var li = el('li', 'icon-picker__option');
            li.setAttribute('role', 'option');
            li.tabIndex = -1;
            li.setAttribute('data-value', option.value);
            li.setAttribute('aria-selected', option.selected ? 'true' : 'false');
            var holder = el('span', 'icon-picker__icon');
            holder.appendChild(iconSvg(sprite, option.value));
            li.appendChild(holder);
            li.appendChild(el('span', 'icon-picker__label', option.textContent));
            li.addEventListener('click', function () { choose(option.value); });
            li.addEventListener('keydown', function (e) {
                if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); choose(option.value); }
                else if (e.key === 'ArrowDown' && li.nextSibling) { e.preventDefault(); li.nextSibling.focus(); }
                else if (e.key === 'ArrowUp' && li.previousSibling) { e.preventDefault(); li.previousSibling.focus(); }
                else if (e.key === 'Escape') { close(); button.focus(); }
            });
            listbox.appendChild(li);
        });
        button.addEventListener('click', function () {
            var opening = listbox.hidden;
            listbox.hidden = !opening;
            button.setAttribute('aria-expanded', opening ? 'true' : 'false');
            if (opening) { var current = listbox.querySelector('[aria-selected="true"]') || listbox.firstChild; if (current) current.focus(); }
        });
        button.addEventListener('keydown', function (e) {
            if (e.key === 'ArrowDown' && listbox.hidden) { e.preventDefault(); button.click(); }
            else if (e.key === 'Escape') close();
        });
        document.addEventListener('click', function (e) { if (!wrap.contains(e.target)) close(); });
        // برچسب فرم به دکمه‌ی نمایشی اشاره کند؛ select اصلی برای ارسال فرم می‌ماند ولی مخفی می‌شود.
        select.classList.add('visually-hidden');
        select.tabIndex = -1;
        select.setAttribute('aria-hidden', 'true');
        if (select.id) { var label = document.querySelector('label[for="' + select.id + '"]'); if (label) label.addEventListener('click', function (e) { e.preventDefault(); button.focus(); }); }
        paintButton();
        select.parentNode.insertBefore(wrap, select);
        wrap.appendChild(button);
        wrap.appendChild(listbox);
        wrap.appendChild(select);
    }
    document.querySelectorAll('select[data-icon-picker]').forEach(initIconPicker);
})();
