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

    // جدول‌های پنل در موبایل کارتی می‌شوند (admin-ui.css): برچسب هر خانه از سرستون همان ستون؛ خانه‌ی اول سربرگ کارت است.
    document.querySelectorAll('table.table-admin').forEach(function (table) {
        var headers = Array.prototype.map.call(table.querySelectorAll('thead th'), function (th) { return th.textContent.trim(); });
        if (headers.length === 0) return;
        table.classList.add('is-stacked');
        table.querySelectorAll('tbody > tr').forEach(function (row) {
            var column = 0;
            Array.prototype.forEach.call(row.children, function (cell, index) {
                var label = headers[column];
                if (index === 0 && !cell.hasAttribute('colspan') && !cell.querySelector('input, select, textarea')) cell.classList.add('is-card-head');
                else if (label && !cell.hasAttribute('data-label') && !cell.hasAttribute('colspan')) cell.setAttribute('data-label', label);
                column += parseInt(cell.getAttribute('colspan') || '1', 10);
            });
        });
    });

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
    // عمداً از beforeunload استفاده نمی‌شود: متن آن را مرورگر می‌نویسد (انگلیسی: «Changes you made may not be saved») و صفحه نمی‌تواند آن را فارسی کند.
    // به‌جای آن، خروج از صفحه (پیوند، دکمه‌ی برگشت مرورگر و ارسال فرم دیگر) با پنجره‌ی فارسی خودمان تأیید گرفته می‌شود.
    (function dirtyGuard() {
        var forms = Array.prototype.slice.call(document.querySelectorAll('form[data-dirty-guard]'));
        if (!forms.length) return;
        var dirty = false, leaving = false, sentinel = false;
        function isDirty() { return dirty && !leaving && !forms.some(function (f) { return f.dataset.submitted === '1'; }); }
        forms.forEach(function (form) {
            function mark(e) {
                // کادر جست‌وجو (مثل جست‌وجوی واحد) داده‌ای را تغییر نمی‌دهد.
                if (e && e.target && e.target.matches && e.target.matches('[data-unit-filter], [type=search]')) return;
                dirty = true;
                // یک ورودی تاریخچه برای گرفتن دکمه‌ی «برگشت» مرورگر؛ فقط یک‌بار و بعد از اولین تغییر.
                if (!sentinel) { sentinel = true; history.pushState({ dirtyGuard: true }, ''); }
            }
            form.addEventListener('input', mark);
            form.addEventListener('change', mark);
            form.addEventListener('submit', function () { form.dataset.submitted = '1'; });
        });

        var dialog = null, onLeave = null, onStay = null;
        function build() {
            var wrap = document.createElement('div');
            wrap.className = 'modal fade';
            wrap.tabIndex = -1;
            wrap.setAttribute('aria-labelledby', 'dirtyGuardTitle');
            wrap.setAttribute('aria-hidden', 'true');
            // با DOM API ساخته می‌شود تا متن فقط به‌عنوان متن درج شود.
            function el(tag, className, text, parent) {
                var node = document.createElement(tag);
                if (className) node.className = className;
                if (text) node.textContent = text;
                if (parent) parent.appendChild(node);
                return node;
            }
            var content = el('div', 'modal-content', '', el('div', 'modal-dialog modal-dialog-centered', '', wrap));
            var title = el('h5', 'modal-title', '', el('div', 'modal-header', '', content));
            title.id = 'dirtyGuardTitle';
            var icon = el('i', 'ti ti-alert-triangle text-warning me-2', '', title);
            icon.setAttribute('aria-hidden', 'true');
            title.appendChild(document.createTextNode('تغییرات ذخیره‌نشده'));
            el('div', 'modal-body', 'تغییراتی که در این صفحه داده‌اید هنوز ذخیره نشده است. اگر از صفحه خارج شوید، از بین می‌روند.', content);
            var footer = el('div', 'modal-footer', '', content);
            var stay = el('button', 'btn btn-primary', 'ماندن در صفحه', footer); stay.type = 'button'; stay.setAttribute('data-guard-stay', '');
            var out = el('button', 'btn btn-danger-light', 'خروج بدون ذخیره', footer); out.type = 'button'; out.setAttribute('data-guard-leave', '');
            document.body.appendChild(wrap);
            wrap.querySelector('[data-guard-stay]').addEventListener('click', function () { var cb = onStay; close(); if (cb) cb(); });
            wrap.querySelector('[data-guard-leave]').addEventListener('click', function () { var cb = onLeave; close(); if (cb) cb(); });
            return wrap;
        }
        function close() {
            var instance = window.bootstrap && window.bootstrap.Modal.getInstance(dialog);
            if (instance) instance.hide(); else dialog.style.display = 'none';
        }
        // پنجره‌ی تأیید فارسی؛ اگر Bootstrap بارگذاری نشده باشد، confirm فارسی ساده.
        function ask(leave, stay) {
            onLeave = leave; onStay = stay;
            if (!window.bootstrap || !window.bootstrap.Modal) {
                if (window.confirm('تغییرات ذخیره‌نشده از بین می‌روند. از صفحه خارج می‌شوید؟')) leave(); else if (stay) stay();
                return;
            }
            dialog = dialog || build();
            window.bootstrap.Modal.getOrCreateInstance(dialog, { backdrop: 'static', keyboard: false }).show();
        }
        function leave(action) { leaving = true; action(); }

        // پیوندهای داخل صفحه (منو، مسیر صفحه، دکمه‌ی برگشت و ...)
        document.addEventListener('click', function (e) {
            if (!isDirty() || e.defaultPrevented || e.button !== 0 || e.ctrlKey || e.metaKey || e.shiftKey || e.altKey) return;
            var link = e.target.closest('a[href]');
            if (!link || link.target === '_blank' || link.hasAttribute('download') || link.hasAttribute('data-bs-toggle')) return;
            var href = link.getAttribute('href');
            if (!href || href.charAt(0) === '#' || /^(javascript|mailto|tel):/i.test(href)) return;
            e.preventDefault();
            ask(function () { leave(function () { window.location.href = link.href; }); });
        }, true);

        // ارسال فرم دیگر (مثلاً خروج از حساب)؛ فرم‌های خود صفحه‌ی ویرایش مجازند.
        document.addEventListener('submit', function (e) {
            if (!isDirty() || forms.indexOf(e.target) !== -1) return;
            e.preventDefault();
            var other = e.target;
            ask(function () { leave(function () { other.submit(); }); });
        }, true);

        // دکمه‌ی برگشت مرورگر: ورودی تاریخچه‌ی ما برداشته می‌شود؛ اگر کاربر بماند دوباره گذاشته می‌شود.
        window.addEventListener('popstate', function () {
            if (!sentinel) return;
            if (!isDirty()) { if (!leaving) { sentinel = false; history.back(); } return; }
            ask(function () { leave(function () { sentinel = false; history.back(); }); }, function () { history.pushState({ dirtyGuard: true }, ''); });
        });
    })();

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

    // ستاره‌ی قرمز کنار عنوان هر فیلد اجباری در همه‌ی صفحه‌های پنل: فیلدی که صریحاً required (یا aria-required) دارد.
    // data-val-required عمداً ملاک نیست: ASP.NET آن را برای هر نوع غیرقابل‌تهی (bool، int) هم می‌گذارد و سوییچ‌ها را اشتباهاً اجباری نشان می‌داد.
    // عنوان از label[for] یا label دربرگیرنده می‌آید؛ در جدول‌های بی‌label (مثل سرویس‌های سامانه) سرستون همان ستون ستاره می‌گیرد.
    // اگر عنوان از قبل ستاره دارد (نوشته‌ی دستی در صفحه) دوباره اضافه نمی‌شود. آخر فایل است تا برچسب‌های کارتی جدول (data-label) را خراب نکند.
    (function markRequiredFields() {
        function hasStar(element) {
            var marks = element.querySelectorAll('.text-danger, .required-star');
            return Array.prototype.some.call(marks, function (m) { return m.classList.contains('required-star') || m.textContent.trim() === '*'; });
        }
        function addStar(element) {
            if (!element || hasStar(element)) return;
            var star = document.createElement('span');
            star.className = 'text-danger required-star';
            star.setAttribute('aria-hidden', 'true');
            star.textContent = '*';
            element.appendChild(document.createTextNode(' '));
            element.appendChild(star);
        }
        function labelOf(control) {
            if (control.id) {
                var labels = document.getElementsByTagName('label');
                for (var i = 0; i < labels.length; i++) if (labels[i].htmlFor === control.id) return labels[i];
            }
            return control.closest('label');
        }
        function headerOf(control) {
            var cell = control.closest('td');
            var table = cell && cell.closest('table');
            if (!table) return null;
            var column = 0;
            Array.prototype.some.call(cell.parentElement.children, function (c) {
                if (c === cell) return true;
                column += parseInt(c.getAttribute('colspan') || '1', 10);
                return false;
            });
            var headers = table.querySelectorAll('thead th');
            return headers[column] || null;
        }
        var selector = 'input[required], select[required], textarea[required], [aria-required="true"]';
        document.querySelectorAll(selector).forEach(function (control) {
            var type = (control.getAttribute('type') || '').toLowerCase();
            if (type === 'hidden' || type === 'submit' || type === 'button' || type === 'radio' || type === 'checkbox') return;
            if (control.closest('[data-no-required-star]')) return;
            addStar(labelOf(control) || headerOf(control));
        });
    })();

})();
