// منوی کاربر در پوسته‌ی صفحه‌های داخلی پورتال: باز/بسته با کلیک، Escape و کلیک بیرون.
(() => {
    'use strict';
    const button = document.getElementById('ppUserBtn');
    const menu = document.getElementById('ppUserMenu');
    if (!button || !menu) return;
    const setOpen = open => {
        menu.hidden = !open;
        button.setAttribute('aria-expanded', String(open));
    };
    button.addEventListener('click', event => {
        event.stopPropagation();
        setOpen(menu.hidden);
        if (!menu.hidden) menu.querySelector('a')?.focus();
    });
    menu.addEventListener('click', event => event.stopPropagation());
    document.addEventListener('click', () => setOpen(false));
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && !menu.hidden) { setOpen(false); button.focus(); }
    });

    // نمایش/پنهان‌کردن رمز در فیلدهای [data-password-toggle]
    document.querySelectorAll('[data-password-toggle]').forEach(toggle => {
        const input = document.getElementById(toggle.dataset.passwordToggle);
        toggle.addEventListener('click', () => {
            const show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            toggle.setAttribute('aria-pressed', String(show));
            toggle.setAttribute('aria-label', show ? 'پنهان‌کردن رمز' : 'نمایش رمز');
            toggle.querySelector('i').className = show ? 'ti ti-eye-off' : 'ti ti-eye';
        });
    });

    // جلوگیری از ارسال دوباره‌ی فرم‌ها
    document.querySelectorAll('form[data-busy]').forEach(form => form.addEventListener('submit', event => {
        const submitter = event.submitter || form.querySelector('[type=submit]');
        setTimeout(() => { if (submitter) submitter.disabled = true; }, 0);
    }));
})();
