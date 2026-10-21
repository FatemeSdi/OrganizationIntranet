/* هلپر مشترک نمایش پیغام‌ها با کامپوننت Toast خود بوت‌استرپ/تمپلیت yzen،
   به‌جای کتابخانه‌های شخص‌ثالث، تا در سراسر سامانه یکدست باشد. */
(function () {
    'use strict';

    var ICONS = {
        success: 'ti-circle-check',
        danger: 'ti-alert-circle',
        warning: 'ti-alert-triangle',
        info: 'ti-info-circle'
    };

    var TITLES = {
        success: 'موفق',
        danger: 'خطا',
        warning: 'هشدار',
        info: 'اطلاعیه'
    };

    // محل نمایش در یک جا تعیین می‌شود: بالای صفحه و وسط افقی (در RTL هم با bootstrap.rtl درست وسط می‌ماند).
    var CONTAINER_CLASS = 'toast-container position-fixed top-0 start-50 translate-middle-x p-3';

    function ensureContainer() {
        var container = document.getElementById('appToastContainer');
        if (!container) {
            container = document.createElement('div');
            container.id = 'appToastContainer';
            container.style.zIndex = 1080;
            document.body.appendChild(container);
        }
        container.className = CONTAINER_CLASS; // حتی اگر Layout کلاس قدیمی داشته باشد
        return container;
    }

    window.showToast = function (message, type) {
        if (!message) return;
        if (typeof bootstrap === 'undefined' || !bootstrap.Toast) {
            // اگر bootstrap.bundle.min.js قبل از این فایل لود نشده باشد، به‌جای شکست بی‌صدا این خطا در کنسول دیده می‌شود.
            console.error('showToast: bootstrap.Toast در دسترس نیست؛ ترتیب لود اسکریپت‌ها را بررسی کنید.');
            return;
        }
        type = (type && ICONS[type]) ? type : 'info';

        var container = ensureContainer();

        // الگوی «Colored toast» تمپلیت yzen: .colored-toast پس‌زمینه را blur می‌کند و هدر رنگی عنوان و دکمه بستن را نگه می‌دارد.
        var toastEl = document.createElement('div');
        toastEl.className = 'toast colored-toast bg-' + type + '-transparent mb-2';
        toastEl.setAttribute('role', 'alert');
        toastEl.setAttribute('aria-live', 'assertive');
        toastEl.setAttribute('aria-atomic', 'true');

        var header = document.createElement('div');
        header.className = 'toast-header bg-' + type + ' text-fixed-white';

        var icon = document.createElement('i');
        icon.className = 'ti ' + ICONS[type] + ' fs-16 me-2';
        icon.setAttribute('aria-hidden', 'true');

        var title = document.createElement('strong');
        title.className = 'me-auto';
        title.textContent = TITLES[type];

        var closeBtn = document.createElement('button');
        closeBtn.type = 'button';
        closeBtn.className = 'btn-close';
        closeBtn.setAttribute('data-bs-dismiss', 'toast');
        closeBtn.setAttribute('aria-label', 'بستن پیام');

        header.appendChild(icon);
        header.appendChild(title);
        header.appendChild(closeBtn);

        var body = document.createElement('div');
        body.className = 'toast-body';
        body.textContent = message;

        toastEl.appendChild(header);
        toastEl.appendChild(body);
        container.appendChild(toastEl);

        var toast = new bootstrap.Toast(toastEl, { delay: 4000 });
        toast.show();
        toastEl.addEventListener('hidden.bs.toast', function () {
            toast.dispose();
            toastEl.remove();
        });
    };

    // پیام‌های سمت سرور (partial _ToastMessages): <div class="d-none" data-toast="success|danger|warning|info">متن</div>
    function showServerToasts() {
        document.querySelectorAll('[data-toast]').forEach(function (el) {
            window.showToast(el.textContent.trim(), el.getAttribute('data-toast'));
            el.removeAttribute('data-toast');
        });
    }
    if (document.readyState === 'loading') document.addEventListener('DOMContentLoaded', showServerToasts);
    else showServerToasts();
})();
