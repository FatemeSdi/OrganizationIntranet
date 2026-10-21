// رفتارهای مشترک پنل مدیریت؛ فقط با data-attribute فعال می‌شوند و بدون JavaScript هم صفحه کار می‌کند.
(function () {
    'use strict';

    function normalize(text) {
        return (text || '').toString().trim().toLowerCase()
            .replace(/[۰-۹]/g, function (d) { return String(d.charCodeAt(0) - 1776); })
            .replace(/[٠-٩]/g, function (d) { return String(d.charCodeAt(0) - 1632); })
            .replace(/ي/g, 'ی').replace(/ك/g, 'ک');
    }

    var fa = function (n) { return Number(n).toLocaleString('fa-IR'); };

    // نوار صفحه‌بندی سمت کاربر؛ فقط با createElement و textContent ساخته می‌شود.
    function renderPager(pager, page, pages, first, last, total, noun, go) {
        pager.textContent = '';
        pager.hidden = total === 0;
        var info = document.createElement('span');
        info.textContent = 'نمایش ' + fa(first) + ' تا ' + fa(last) + ' از ' + fa(total) + ' ' + noun;
        pager.appendChild(info);
        if (pages <= 1) return;
        var list = document.createElement('ul');
        list.className = 'pagination pagination-sm';
        function item(label, target, options) {
            var li = document.createElement('li');
            li.className = 'page-item' + (options.active ? ' active' : '') + (options.disabled ? ' disabled' : '');
            var button = document.createElement('button');
            button.type = 'button';
            button.className = 'page-link';
            if (options.icon) { var i = document.createElement('i'); i.className = options.icon; button.appendChild(i); button.setAttribute('aria-label', label); }
            else button.textContent = label;
            if (options.active) button.setAttribute('aria-current', 'page');
            if (options.disabled) button.disabled = true;
            else button.addEventListener('click', function () { go(target); });
            li.appendChild(button);
            list.appendChild(li);
        }
        item('صفحه‌ی قبل', page - 1, { icon: 'ti ti-chevron-right', disabled: page === 1 });
        var previous = 0;
        for (var p = 1; p <= pages; p++) {
            if (p !== 1 && p !== pages && Math.abs(p - page) > 2) continue;
            if (previous && p - previous > 1) item('…', 0, { disabled: true });
            item(fa(p), p, { active: p === page });
            previous = p;
        }
        item('صفحه‌ی بعد', page + 1, { icon: 'ti ti-chevron-left', disabled: page === pages });
        pager.appendChild(list);
    }

    // فیلتر و صفحه‌بندی سمت کاربر برای جدول‌هایی که همه‌ی ردیف‌ها را دارند: [data-filter-scope] شامل کنترل‌های [data-filter="کلید"] و ردیف‌های [data-row].
    // کلید search با data-search ردیف مقایسه می‌شود؛ کلیدهای دیگر با data-<کلید> (چند مقدار با | جدا می‌شود).
    // اگر scope دارای data-page-size و عنصر [data-pager] باشد، ردیف‌های پیداشده صفحه‌به‌صفحه نمایش داده می‌شوند.
    document.querySelectorAll('[data-filter-scope]').forEach(function (scope) {
        var controls = scope.querySelectorAll('[data-filter]');
        var rows = scope.querySelectorAll('[data-row]');
        var count = scope.querySelector('[data-filter-count]');
        var empty = scope.querySelector('[data-filter-empty]');
        var pager = scope.querySelector('[data-pager]');
        var pageSize = parseInt(scope.dataset.pageSize, 10) || 0;
        var page = 1;
        function apply(keepPage) {
            if (!keepPage) page = 1;
            var matched = [];
            rows.forEach(function (row) {
                var show = Array.prototype.every.call(controls, function (control) {
                    var value = normalize(control.value);
                    if (!value) return true;
                    var key = control.dataset.filter;
                    if (key === 'search' || control.dataset.filterMode === 'contains') return normalize(row.dataset[key]).indexOf(value) !== -1;
                    return normalize(row.dataset[key]).split('|').indexOf(value) !== -1;
                });
                if (show) matched.push(row);
                row.hidden = true;
            });
            var pages = pageSize ? Math.max(1, Math.ceil(matched.length / pageSize)) : 1;
            page = Math.min(page, pages);
            var start = pageSize ? (page - 1) * pageSize : 0, end = pageSize ? start + pageSize : matched.length;
            matched.slice(start, end).forEach(function (row) { row.hidden = false; });
            if (count) count.textContent = fa(matched.length);
            if (empty) empty.hidden = matched.length !== 0 || rows.length === 0;
            renderChips();
            if (pager && pageSize)
                renderPager(pager, page, pages, matched.length ? start + 1 : 0, Math.min(end, matched.length), matched.length, scope.dataset.noun || 'مورد',
                    function (target) { page = target; apply(true); scope.scrollIntoView({ block: 'start', behavior: 'smooth' }); });
        }
        // برچسب فیلترهای فعال جستجوی پیشرفته ([data-filter-label]) با دکمه‌ی حذف هرکدام؛ شمارنده روی دکمه‌ی بازکردن پنل.
        var chips = scope.querySelector('[data-filter-chips]');
        var activeCount = scope.querySelector('[data-filter-active-count]');
        function renderChips() {
            var active = Array.prototype.filter.call(controls, function (c) { return c.dataset.filterLabel && c.value.trim(); });
            if (activeCount) { activeCount.textContent = fa(active.length); activeCount.hidden = active.length === 0; }
            if (!chips) return;
            chips.textContent = '';
            chips.hidden = active.length === 0;
            active.forEach(function (control) {
                var text = control.tagName === 'SELECT' ? control.options[control.selectedIndex].text : control.value.trim();
                var chip = document.createElement('button');
                chip.type = 'button';
                chip.className = 'filter-chip';
                chip.setAttribute('aria-label', 'حذف فیلتر ' + control.dataset.filterLabel);
                var label = document.createElement('span');
                label.textContent = control.dataset.filterLabel + ': ' + text;
                var x = document.createElement('i');
                x.className = 'ti ti-x';
                x.setAttribute('aria-hidden', 'true');
                chip.appendChild(label);
                chip.appendChild(x);
                chip.addEventListener('click', function () { control.value = ''; apply(false); control.focus(); });
                chips.appendChild(chip);
            });
        }
        controls.forEach(function (control) { control.addEventListener(control.tagName === 'SELECT' ? 'change' : 'input', function () { apply(false); }); });
        scope.querySelectorAll('[data-filter-clear]').forEach(function (clear) {
            clear.addEventListener('click', function () { controls.forEach(function (c) { c.value = ''; }); apply(false); });
        });
        apply(false);
    });

    // شمارنده‌ی موارد انتخاب‌شده در _PickList.
    document.querySelectorAll('[data-pick-list]').forEach(function (list) {
        var count = list.querySelector('[data-pick-count]');
        list.addEventListener('change', function () {
            count.textContent = fa(list.querySelectorAll('input[type=checkbox]:checked').length);
        });
    });

    // سوییچ وضعیت که فرم خودش را ارسال می‌کند؛ برای غیرفعال‌کردن، در صورت وجود data-confirm-off تأیید می‌گیرد.
    document.querySelectorAll('[data-autosubmit]').forEach(function (input) {
        input.addEventListener('change', function () {
            var message = input.dataset.confirmOff;
            if (!input.checked && message && !window.confirm(message)) { input.checked = true; return; }
            input.disabled = true;
            input.form.requestSubmit ? input.form.requestSubmit() : input.form.submit();
        });
    });

    // دکمه‌های ارسال با data-confirm پیش از ارسال تأیید می‌گیرند.
    document.addEventListener('click', function (e) {
        var button = e.target.closest('[data-confirm]');
        if (button && !window.confirm(button.dataset.confirm)) { e.preventDefault(); e.stopImmediatePropagation(); }
    }, true);

    // جلوگیری از ارسال دوباره: دکمه پس از ساخته‌شدن داده‌ی فرم (تیک بعدی) غیرفعال می‌شود تا name/value آن ارسال شود.
    document.querySelectorAll('form[data-busy]').forEach(function (form) {
        form.addEventListener('submit', function (e) {
            if (e.defaultPrevented) return;
            var button = e.submitter || form.querySelector('[type=submit]');
            form.dataset.submitted = '1';
            setTimeout(function () {
                if (!button) return;
                button.disabled = true;
                if (!button.querySelector('.spinner-border'))
                    button.insertAdjacentHTML('beforeend', '<span class="spinner-border spinner-border-sm ms-2" aria-hidden="true"></span>');
            }, 0);
        });
    });

    // هشدار تغییرات ذخیره‌نشده در فرم‌های ویرایش.
    document.querySelectorAll('form[data-dirty-guard]').forEach(function (form) {
        var dirty = false;
        form.addEventListener('input', function () { dirty = true; });
        form.addEventListener('change', function () { dirty = true; });
        window.addEventListener('beforeunload', function (e) {
            if (dirty && form.dataset.submitted !== '1') { e.preventDefault(); e.returnValue = ''; }
        });
        form.addEventListener('submit', function () { form.dataset.submitted = '1'; });
    });

    // شمارنده‌ی کاراکتر برای [data-counter] با maxlength.
    document.querySelectorAll('[data-counter][maxlength]').forEach(function (field) {
        var max = parseInt(field.getAttribute('maxlength'), 10);
        var counter = document.createElement('small');
        counter.className = 'char-counter';
        counter.setAttribute('aria-live', 'polite');
        field.insertAdjacentElement('afterend', counter);
        function update() {
            var length = field.value.length;
            counter.textContent = length.toLocaleString('fa-IR') + ' / ' + max.toLocaleString('fa-IR');
            counter.classList.toggle('is-near', length > max * 0.9);
        }
        field.addEventListener('input', update);
        update();
    });
})();
