(function ($) {
    'use strict';

    // An expired refresh payload or antiforgery token cannot be reused.
    // Fetch a new form and its matching cookies instead of leaving a stale image.
    $(document).ajaxError(function (event, xhr, settings) {
        if (xhr.status !== 400 || !settings.url) return;
        var url = new URL(settings.url, window.location.href);
        if (url.origin === window.location.origin && /\/DNTCaptchaImage\/Refresh$/i.test(url.pathname)) {
            window.location.reload();
        }
    });
})(jQuery);
