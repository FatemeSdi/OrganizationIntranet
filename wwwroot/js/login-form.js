/* فرم ورود مشترک Portal و Admin.
   انتخاب «روش ورود» فقط فیلدهای داخل کادر را عوض می‌کند:
   - password / ldap : نام کاربری و رمز عبور
   - national-id-sms : کد ملی ← ارسال کد ← کد تأیید ۶ رقمی
   توکن چالش در سرور (TempData) می‌ماند؛ این اسکریپت فقط فرم را با Antiforgery به همان صفحه می‌فرستد. */
(() => {
    'use strict';

    const form = document.getElementById('loginForm');
    if (!form) return;

    const NATIONAL = 'national-id-sms';
    const method = document.getElementById('loginMethod');
    const panels = form.querySelectorAll('[data-method-panel]');
    const nationalStep = document.getElementById('nationalIdStep');
    const codeStep = document.getElementById('codeStep');
    const nationalInput = form.querySelector('[name="NationalId"]');
    const codeInput = form.querySelector('[name="Code"]');
    const captcha = document.getElementById('securitySection');
    const button = document.getElementById('loginBtn');
    const buttonText = document.getElementById('loginText');
    const spinner = document.getElementById('loginSpinner');
    const resend = document.getElementById('resendCode');
    const resendTimer = document.getElementById('resendTimer');
    const changeId = document.getElementById('changeNationalId');
    const toast = (message, type) => (window.showToast ? window.showToast(message, type) : window.alert(message));

    let step = 'id';
    let timerHandle = null;

    const isNational = () => !!method && method.value === NATIONAL;
    const persianDigits = '۰۱۲۳۴۵۶۷۸۹', arabicDigits = '٠١٢٣٤٥٦٧٨٩';
    const toLatinDigits = value => value
        .replace(/[۰-۹]/g, d => String(persianDigits.indexOf(d)))
        .replace(/[٠-٩]/g, d => String(arabicDigits.indexOf(d)));
    const faNumber = n => Number(n).toLocaleString('fa-IR');

    function setEnabled(container, enabled) {
        if (!container) return;
        container.querySelectorAll('input, select, textarea').forEach(input => { input.disabled = !enabled; });
    }

    function render() {
        const national = isNational();
        panels.forEach(panel => {
            const show = (panel.dataset.methodPanel === 'national-id') === national;
            panel.hidden = !show;
            setEnabled(panel, show);
        });
        if (national) {
            nationalStep.hidden = step !== 'id';
            codeStep.hidden = step !== 'code';
            setEnabled(nationalStep, step === 'id');
            setEnabled(codeStep, step === 'code');
        }
        // CAPTCHA پیش از ارسال کد لازم است؛ در گام وارد کردن کد پنهان می‌شود.
        if (captcha) captcha.hidden = national && step === 'code';
        buttonText.textContent = national && step === 'id' ? 'ارسال کد تأیید' : 'ورود';
    }

    function busy(on, label) {
        button.disabled = on;
        spinner.classList.toggle('d-none', !on);
        if (label) buttonText.textContent = label;
        else if (!on) render();
    }

    function refreshCaptcha() {
        const refresh = form.querySelector('.captcha-refresh');
        if (refresh) refresh.click();
        const input = form.querySelector('.captchaInput');
        if (input) input.value = '';
    }

    function stopCountdown() {
        if (timerHandle) window.clearInterval(timerHandle);
        timerHandle = null;
        if (resendTimer) resendTimer.textContent = '';
        if (resend) resend.disabled = false;
    }

    function startCountdown(seconds) {
        stopCountdown();
        if (!resend) return;
        let left = seconds;
        resend.disabled = true;
        resendTimer.textContent = `(${faNumber(left)})`;
        timerHandle = window.setInterval(() => {
            left -= 1;
            if (left <= 0) { stopCountdown(); return; }
            resendTimer.textContent = `(${faNumber(left)})`;
        }, 1000);
    }

    async function post(handler) {
        const body = new URLSearchParams(new FormData(form));
        const url = new URL(form.getAttribute('action') || window.location.pathname, window.location.origin);
        if (handler) url.searchParams.set('handler', handler);
        const response = await fetch(url, {
            method: 'POST',
            body,
            credentials: 'same-origin',
            headers: { 'Content-Type': 'application/x-www-form-urlencoded', 'X-Requested-With': 'XMLHttpRequest' }
        });
        let json = null;
        try { json = await response.json(); } catch { /* not JSON */ }
        if (response.status === 400 && json && json.code === 'login_form_expired') {
            toast(json.message, 'warning');
            button.disabled = true;
            window.setTimeout(() => window.location.reload(), 1800);
            throw new Error('expired');
        }
        if (!json) throw new Error('unexpected');
        return json;
    }

    function backToNationalId() {
        step = 'id';
        stopCountdown();
        if (codeInput) codeInput.value = '';
        render();
        if (nationalInput) nationalInput.focus();
    }

    form.addEventListener('submit', async event => {
        event.preventDefault();
        const national = isNational();
        const handler = !national ? '' : step === 'id' ? 'SendCode' : 'VerifyCode';

        if (handler === 'SendCode') {
            nationalInput.value = toLatinDigits(nationalInput.value.trim());
            if (!/^[0-9]{10}$/.test(nationalInput.value)) { toast('کد ملی باید ۱۰ رقم باشد.', 'warning'); nationalInput.focus(); return; }
        }
        if (handler === 'VerifyCode') {
            codeInput.value = toLatinDigits(codeInput.value.trim());
            if (!/^[0-9]{6}$/.test(codeInput.value)) { toast('کد تأیید ۶ رقم است.', 'warning'); codeInput.focus(); return; }
        }

        busy(true, handler === 'SendCode' ? 'در حال ارسال کد...' : 'در حال ورود...');
        try {
            const result = await post(handler);
            if (result.success && result.redirectUrl) {
                buttonText.textContent = 'ورود موفق';
                window.location.href = result.redirectUrl;
                return;
            }
            if (result.success && handler === 'SendCode') {
                step = 'code';
                busy(false);
                startCountdown(result.resendIn || 60);
                toast(result.message, 'info');
                codeInput.focus();
                return;
            }
            if (result.restart) backToNationalId();
            toast(result.message || 'ورود انجام نشد؛ دوباره تلاش کنید.', 'danger');
            if (handler === 'VerifyCode') { codeInput.value = ''; codeInput.focus(); }
            else refreshCaptcha();
        } catch (error) {
            if (error.message === 'expired') return;
            toast('ورود انجام نشد؛ لطفاً دوباره تلاش کنید.', 'danger');
            if (handler !== 'VerifyCode') refreshCaptcha();
        }
        busy(false);
    });

    if (resend) resend.addEventListener('click', async () => {
        resend.disabled = true;
        try {
            const result = await post('ResendCode');
            if (result.restart) { backToNationalId(); toast(result.message, 'warning'); return; }
            toast(result.message, result.success ? 'info' : 'danger');
            startCountdown(result.resendIn || 60);
        } catch (error) {
            if (error.message !== 'expired') { toast('ارسال دوباره انجام نشد.', 'danger'); resend.disabled = false; }
        }
    });

    if (changeId) changeId.addEventListener('click', backToNationalId);
    if (method) method.addEventListener('change', () => { step = 'id'; stopCountdown(); render(); });

    // فقط رقم؛ ارقام فارسی به لاتین تبدیل می‌شوند. با کامل شدن ۶ رقم، کد خودکار ارسال می‌شود.
    [nationalInput, codeInput].forEach(input => {
        if (!input) return;
        input.addEventListener('input', () => {
            const max = input === codeInput ? 6 : 10;
            input.value = toLatinDigits(input.value).replace(/[^0-9]/g, '').slice(0, max);
            if (input === codeInput && input.value.length === 6 && !button.disabled) form.requestSubmit();
        });
    });

    // نمایش/پنهان‌کردن رمز: دکمه‌ی چشم جداگانه (.password-toggle، ورود مدیریت) یا کلیک روی آیکن قفل (.password-eye، پورتال).
    const passwordToggle = document.getElementById('Password-toggle');
    if (passwordToggle) {
        const input = passwordToggle.querySelector('input');
        const eyeButton = passwordToggle.querySelector('.password-toggle');
        if (eyeButton) {
            const icon = eyeButton.querySelector('i');
            eyeButton.addEventListener('click', () => {
                const show = input.type === 'password';
                input.type = show ? 'text' : 'password';
                icon.classList.toggle('ti-eye', !show);
                icon.classList.toggle('ti-eye-off', show);
                const label = show ? 'عدم نمایش رمز عبور' : 'نمایش رمز عبور';
                eyeButton.setAttribute('aria-label', label);
                eyeButton.setAttribute('title', label);
                eyeButton.setAttribute('aria-pressed', String(show));
            });
        } else {
            const icon = passwordToggle.querySelector('i');
            passwordToggle.addEventListener('click', event => {
                if (!event.target.closest('.password-eye')) return;
                input.type = input.type === 'password' ? 'text' : 'password';
                icon.classList.toggle('ti-lock');
                icon.classList.toggle('ti-eye');
            });
        }
    }

    render();
})();
