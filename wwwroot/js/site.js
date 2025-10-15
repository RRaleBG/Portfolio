// Moved from _Layout.cshtml

// Add 'js' class to <html>
document.documentElement.classList.add('js');

// Theme preference logic
(function () {
    const key = 'theme-preference';
    const html = document.documentElement;
    const btn = document.getElementById('theme-toggle');
    function prefersDark() { return window.matchMedia && window.matchMedia('(prefers-color-scheme: dark)').matches; }
    function getTheme() {
        let t = null;
        try { t = localStorage.getItem(key); } catch { }
        if (!t) { t = prefersDark() ? 'dark' : 'light'; }
        return t;
    }
    function setTheme(t) {
        html.setAttribute('data-bs-theme', t);
        html.setAttribute('data-theme', t);
        try { localStorage.setItem(key, t); } catch { }
    }
    function updateIcon() { if (btn) btn.textContent = getTheme() === 'dark' ? '??' : '??'; }
    // Initial theme set
    setTheme(getTheme());
    updateIcon();
    if (btn) {
        btn.addEventListener('click', function () {
            const newTheme = getTheme() === 'light' ? 'dark' : 'light';
            setTheme(newTheme);
            updateIcon();
        });
    }
})();

// Page-load reveal animations (staggered)
(function () {
    function revealOnLoad() {
        const ordered = [];
        const mainChildren = Array.from(document.querySelectorAll('main > *:not(script):not(style)'));
        if (mainChildren.length) ordered.push(...mainChildren);
        const extras = Array.from(document.querySelectorAll('main .card, main .list-group, main .table'));
        extras.forEach(el => { if (!ordered.includes(el)) ordered.push(el); });
        if (ordered.length === 0) {
            const main = document.querySelector('main');
            if (main) ordered.push(main);
        }
        ordered.forEach(el => el.classList.add('reveal'));
        ordered.forEach((el, i) => {
            const delay = Math.min(80 * i, 700);
            setTimeout(() => el.classList.add('visible'), delay);
        });
        var mainReveal = document.querySelector('main.reveal-inside');
        if (mainReveal) {
            mainReveal.classList.add('reveal-inside');
            setTimeout(function () {
                mainReveal.classList.add('visible');
            }, 120);
        }
    }
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', revealOnLoad);
    } else {
        revealOnLoad();
    }
})();

// SignalR Toastr notification
if (window.signalR) {
    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/notificationsHub")
        .build();
    connection.on("ReceiveNotification", function(message) {
        if (window.toastr) toastr.info(message);
    });
    connection.start().catch(function (err) {
        return console.error(err.toString());
    });
}
