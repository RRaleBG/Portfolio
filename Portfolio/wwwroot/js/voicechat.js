(function () {
    // Defensive lookup in case script is loaded on pages without chat UI
    const chatBox = document.getElementById('chat-box');
    const input = document.getElementById('user-input');
    const sendBtn = document.getElementById('send-btn');
    if (!chatBox || !input || !sendBtn) return;

    function appendMessageEl(text, fromUser = false) {
        const div = document.createElement('div');
        div.className = fromUser ? 'text-end text-primary mb-2' : 'text-start text-success mb-2';
        div.textContent = text;
        chatBox.appendChild(div);
        chatBox.scrollTop = chatBox.scrollHeight;
        return div; // return element so it can be updated
    }

    // Minimal in-memory session to avoid losing recent turns on reload
    const SESSION_KEY = 'chat:recent';
    function saveRecent() {
        try {
            const items = Array.from(chatBox.querySelectorAll('div')).slice(-50).map(d => ({ text: d.textContent }));
            sessionStorage.setItem(SESSION_KEY, JSON.stringify(items));
        } catch { }
    }
    function restoreRecent() {
        try {
            const raw = sessionStorage.getItem(SESSION_KEY);
            if (!raw) return;
            const items = JSON.parse(raw);
            items.forEach(i => appendMessageEl(i.text, false));
        } catch { }
    }

    restoreRecent();

    async function sendMessage(msg) {
        if (!msg) return;
        appendMessageEl(msg, true);
        input.value = '';

        const thinkingEl = appendMessageEl('Thinking…');

        // disable send while awaiting
        sendBtn.disabled = true;
        const prevLabel = sendBtn.innerHTML;
        try {
            sendBtn.innerHTML = '<span class="spinner-border spinner-border-sm" role="status" aria-hidden="true"></span> Sending...';
            const response = await fetch('/api/chat', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({ message: msg })
            });

            let data;
            try {
                data = await response.json();
            } catch {
                throw new Error('Invalid JSON response');
            }

            if (!response.ok) {
                const err = (data && (data.error || data.message)) || `HTTP ${response.status}`;
                thinkingEl.textContent = `Error: ${err}`;
                thinkingEl.className = 'text-start text-danger mb-2';
                saveRecent();
                return;
            }

            const reply = (data && (data.response ?? data.reply)) || "I'm not sure how to answer that.";
            thinkingEl.textContent = reply;
            thinkingEl.className = 'text-start text-success mb-2';
            saveRecent();
        } catch (e) {
            thinkingEl.textContent = `Error: ${e.message || e}`;
            thinkingEl.className = 'text-start text-danger mb-2';
            saveRecent();
        } finally {
            sendBtn.disabled = false;
            sendBtn.innerHTML = prevLabel;
        }
    }

    // Handle send button
    sendBtn.addEventListener('click', function () {
        const msg = input.value.trim();
        if (msg) sendMessage(msg);
    });

    // Enter to send
    input.addEventListener('keydown', (e) => {
        if (e.key === 'Enter') {
            e.preventDefault();
            const msg = input.value.trim();
            if (msg) sendMessage(msg);
        }
    });
})();
