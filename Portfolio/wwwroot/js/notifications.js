// Client: connect to hub and show incoming notifications using the site's toast helper.
// Requires: signalr.js loaded before this script and window.toasts available.
(function () {
    if (typeof signalR === 'undefined') return;
    if (typeof window.toasts === 'undefined') {
        // fallback minimal toast if site.toasts not available
        window.toasts = {
            show: function (title, body, type) {
                const containerId = 'toast-stack';
                let container = document.getElementById(containerId);
                if (!container) {
                    container = document.createElement('div');
                    container.id = containerId;
                    container.className = 'toast-container position-fixed bottom-0 end-0 p-3';
                    container.style.zIndex = '12000';
                    document.body.appendChild(container);
                }
                const wrapper = document.createElement('div');
                wrapper.className = 'toast align-items-center border-0 shadow p-2 mb-2';
                wrapper.innerHTML = '<div><strong>' + (title || '') + '</strong><div class="small">' + (body || '') + '</div></div>';
                container.appendChild(wrapper);
                setTimeout(() => wrapper.remove(), 5000);
            }
        };
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/notifications')
        .withAutomaticReconnect()
        .build();

    function showToastFromPayload(payload) {
        try {
            const title = payload?.title ?? 'Notification';
            const message = payload?.message ?? (typeof payload === 'string' ? payload : JSON.stringify(payload));
            // Prefer existing toast helper (site.js -> window.toasts.show)
            try {
                window.toasts.show(title, message, 'info');
            } catch (e) {
                // Last-resort DOM toast
                const el = document.createElement('div');
                el.className = 'alert alert-info small';
                el.style.position = 'fixed';
                el.style.right = '1rem';
                el.style.bottom = (document.querySelectorAll('.alert[data-notif]').length * 3 + 1) + 'rem';
                el.setAttribute('data-notif', '1');
                el.textContent = title + ': ' + message;
                document.body.appendChild(el);
                setTimeout(() => el.remove(), 8000);
            }
        } catch (e) {
            console.warn('Notification render failed', e);
        }
    }

    connection.on('ReceiveNotification', (payload) => showToastFromPayload(payload));
    // backward compatible handler used elsewhere
    connection.on('notify', (msg) => showToastFromPayload({ title: 'Notice', message: msg }));

    connection.onreconnecting((err) => {
        console.warn('SignalR reconnecting', err);
        window.toasts.show('Connection', 'Reconnecting to notifications...', 'info');
    });
    connection.onreconnected(() => {
        console.info('SignalR reconnected');
        window.toasts.show('Connection', 'Notifications reconnected', 'success');
    });
    connection.onclose(() => {
        console.warn('SignalR connection closed');
        window.toasts.show('Connection', 'Notifications disconnected', 'warning');
    });

    (async function start() {
        try {
            await connection.start();
            console.info('SignalR connected');
        } catch (err) {
            console.warn('SignalR failed to start', err);
        }
    })();
})();