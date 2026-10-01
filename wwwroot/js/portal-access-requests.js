(() => {
    'use strict';

    const dialog = document.getElementById('access-requests');
    if (!dialog) return;

    const open = () => {
        if (dialog.open) return;
        dialog.showModal();
        document.documentElement.classList.add('access-requests-open');
    };

    document.querySelectorAll('a[href="#access-requests"]').forEach(link => {
        link.setAttribute('aria-haspopup', 'dialog');
        link.setAttribute('aria-controls', dialog.id);
        link.addEventListener('click', event => {
            event.preventDefault();
            open();
        });
    });

    dialog.querySelector('.access-requests-close').addEventListener('click', () => dialog.close());
    dialog.addEventListener('click', event => {
        const bounds = dialog.getBoundingClientRect();
        if (event.target === dialog && (event.clientX < bounds.left || event.clientX > bounds.right ||
            event.clientY < bounds.top || event.clientY > bounds.bottom)) dialog.close();
    });
    dialog.addEventListener('close', () => {
        document.documentElement.classList.remove('access-requests-open');
        if (location.hash === '#access-requests') {
            history.replaceState(history.state, '', location.pathname + location.search);
        }
    });

    const openFromLocation = () => {
        if (location.hash === '#access-requests') open();
    };
    window.addEventListener('hashchange', openFromLocation);
    // Keep feedback and pagination visible after a full-page form submission.
    if (dialog.querySelector('.request-error, .request-feedback')) open();
    else openFromLocation();
})();
