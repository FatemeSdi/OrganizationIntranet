// The admin shell uses a single vertical menu, independent of template demo settings.
(function () {
    "use strict";
    const html = document.documentElement;
    const toggle = document.getElementById("mainSidebarToggle");
    const sidebar = document.getElementById("sidebar");
    const overlay = document.getElementById("responsive-overlay");
    if (!toggle || !sidebar || !overlay) return;

    const mobile = window.matchMedia("(max-width: 991.98px)");
    let expanded = !mobile.matches;
    html.setAttribute("data-nav-layout", "vertical");
    html.setAttribute("data-vertical-style", "closed");
    html.removeAttribute("data-nav-style");
    html.removeAttribute("data-icon-overlay");

    function render() {
        if (mobile.matches) html.setAttribute("data-toggled", expanded ? "open" : "close");
        else if (expanded) html.removeAttribute("data-toggled");
        else html.setAttribute("data-toggled", "close-menu-close");
        toggle.setAttribute("aria-expanded", String(expanded));
        overlay.classList.toggle("active", mobile.matches && expanded);
        sidebar.inert = !expanded;
    }

    toggle.addEventListener("click", function () {
        expanded = !expanded;
        render();
    });
    overlay.addEventListener("click", function () {
        expanded = false;
        render();
        toggle.focus();
    });
    document.addEventListener("keydown", function (event) {
        if (event.key === "Escape" && mobile.matches && expanded) {
            expanded = false;
            render();
            toggle.focus();
        }
    });
    mobile.addEventListener("change", function () {
        expanded = !mobile.matches;
        render();
    });
    // Preserve the server's section matching on edit/assign pages.
    sidebar.querySelectorAll(".side-menu__item.active").forEach(function (link) {
        link.setAttribute("aria-current", "page");
    });
    // Opening support should also dismiss the account dropdown behind the modal.
    document.getElementById("supportModal")?.addEventListener("show.bs.modal", function () {
        const profile = document.getElementById("mainHeaderProfile");
        if (profile) bootstrap.Dropdown.getInstance(profile)?.hide();
    });
    render();
})();
