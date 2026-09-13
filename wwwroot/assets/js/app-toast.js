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

    function ensureContainer() {
        var container = document.getElementById('appToastContainer');
        if (!container) {
            container = document.createElement('div');
            container.id = 'appToastContainer';
            container.className = 'toast-container position-fixed top-0 start-0 p-3';
            container.style.zIndex = 1080;
            document.body.appendChild(container);
        }
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

        var toastEl = document.createElement('div');
        toastEl.className = 'toast align-items-center border-0 bg-' + type + '-transparent mb-2';
        toastEl.setAttribute('role', 'alert');
        toastEl.setAttribute('aria-live', 'assertive');
        toastEl.setAttribute('aria-atomic', 'true');

        var flex = document.createElement('div');
        flex.className = 'd-flex';

        var body = document.createElement('div');
        body.className = 'toast-body d-flex align-items-center gap-2';

        var icon = document.createElement('i');
        icon.className = 'ti ' + ICONS[type] + ' fs-18 text-' + type;

        var text = document.createElement('span');
        text.textContent = message;

        body.appendChild(icon);
        body.appendChild(text);

        var closeBtn = document.createElement('button');
        closeBtn.type = 'button';
        closeBtn.className = 'btn-close me-2 m-auto';
        closeBtn.setAttribute('data-bs-dismiss', 'toast');
        closeBtn.setAttribute('aria-label', 'بستن پیام');

        flex.appendChild(body);
        flex.appendChild(closeBtn);
        toastEl.appendChild(flex);
        container.appendChild(toastEl);

        var toast = new bootstrap.Toast(toastEl, { delay: 4000 });
        toast.show();
        toastEl.addEventListener('hidden.bs.toast', function () {
            toast.dispose();
            toastEl.remove();
        });
    };
})();
