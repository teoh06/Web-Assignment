// Personalization settings (keyboard sound) - system-wide (per authenticated session)
const keyboardSounds = {
    classic: '/sounds/keyboard-click.mp3',
    magic: '/sounds/magic-click.mp3',
    electronic: '/sounds/electronic-click.mp3'
};
// Namespacing helpers
function nsKey(base) {
    if (typeof window !== 'undefined' && window.userEmail && isAuth()) {
        return `${base}:${window.userEmail}`;
    }
    // No authenticated user => no namespaced key
    return null;
}

function setCookie(name, value, days) {
    let expires = '';
    if (days) {
        const date = new Date();
        date.setTime(date.getTime() + (days*24*60*60*1000));
        expires = '; expires=' + date.toUTCString();
    }
    document.cookie = name + '=' + (value || '')  + expires + '; path=/';
}
function getCookie(name) {
    const nameEQ = name + '=';
    const ca = document.cookie.split(';');
    for(let i=0;i < ca.length;i++) {
        let c = ca[i];
        while (c.charAt(0)==' ') c = c.substring(1,c.length);
        if (c.indexOf(nameEQ) == 0) return c.substring(nameEQ.length,c.length);
    }
    return null;
}

// Per-user preference helpers (no global migration to avoid cross-account leakage)
function getPref(name) {
    const nk = nsKey(name);
    if (!nk) return null; // not authenticated or no email => default off
    let v = getCookie(nk);
    if (v === null) v = localStorage.getItem(nk);
    // Ignore legacy global keys completely to avoid cross-account leakage
    return v;
}
function setPref(name, value) {
    const nk = nsKey(name);
    if (!nk) return; // do not write when no authenticated user
    setCookie(nk, value, 365);
    try { localStorage.setItem(nk, value); } catch (_) {}
}

let selectedSoundKey = getPref('keyboardSoundType') || 'classic';
let keyboardAudio = null;
let keyboardSoundEnabled = false;
function isAuth() {
    if (typeof window === 'undefined') return false;
    const v = window.isAuthenticated;
    if (typeof v === 'boolean') return v;
    if (typeof v === 'string' && v.toLowerCase() === 'true') return true;
    // Fallbacks in case inline flag didn't render yet
    try {
        // Body data attribute set by _Layout.cshtml
        const bodyAuth = document.body && document.body.getAttribute('data-auth');
        if (typeof bodyAuth === 'string' && bodyAuth.toLowerCase() === 'true') return true;
        // DOM heuristic: presence of Logout button/form
        if (document.querySelector('form[action*="/Account/Logout"], form[asp-action="Logout"]')) return true;
        // Cookie heuristic: presence of ASP.NET Core auth cookie
        if (document.cookie && document.cookie.indexOf('.AspNetCore.Cookies=') !== -1) return true;
    } catch (_) {}
    return false;
}

// (setCookie/getCookie moved above)

function playKeyboardSound() {
    const soundUrl = keyboardSounds[selectedSoundKey] || keyboardSounds['classic'];
    if (!keyboardAudio || keyboardAudio.src !== location.origin + soundUrl) {
        keyboardAudio = new Audio(soundUrl);
    } else {
        keyboardAudio.currentTime = 0;
    }
    keyboardAudio.play();
}
function handleKeydownSound(e) {
    if (isAuth() && keyboardSoundEnabled && (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA')) {
        playKeyboardSound();
    }
}

function attachKeyboardListener() {
    // Ensure singleton listener
    document.removeEventListener('keydown', handleKeydownSound, true);
    if (isAuth() && keyboardSoundEnabled) {
        document.addEventListener('keydown', handleKeydownSound, true);
        window.__kbListenerAttached = true;
    } else {
        window.__kbListenerAttached = false;
    }
}

function setKeyboardSoundType(type) {
    if (keyboardSounds[type]) {
        selectedSoundKey = type;
        setPref('keyboardSoundType', type);
    }
}

// System-wide initialization
(function() {
    // Remove legacy global keys to prevent cross-account leakage
    try {
        document.cookie = 'keyboardSoundEnabled=; Max-Age=0; path=/';
        document.cookie = 'keyboardSoundType=; Max-Age=0; path=/';
        localStorage.removeItem('keyboardSoundEnabled');
        localStorage.removeItem('keyboardSoundType');
    } catch(_) {}

    // Priority: cookie > localStorage (per-user only)
    let setting = getPref('keyboardSoundEnabled');
    keyboardSoundEnabled = setting === 'true';
    if (setting === null || typeof setting === 'undefined') {
        keyboardSoundEnabled = false; // default off when unauthenticated or new user
    }
    // Hard detach any lingering listeners on fresh page load if not authenticated
    if (!isAuth() && window.__kbListenerAttached) {
        document.removeEventListener('keydown', handleKeydownSound, true);
        window.__kbListenerAttached = false;
    }
    // Only attach listeners for authenticated users
    attachKeyboardListener();

    // If the toggle exists on this page, sync it
    const keyboardToggle = document.getElementById('keyboardSoundToggle');
    if (keyboardToggle) {
        keyboardToggle.checked = isAuth() && keyboardSoundEnabled;
        keyboardToggle.addEventListener('change', function() {
            if (!isAuth()) {
                // Block enabling when logged out, but keep preference stored for next login
                keyboardToggle.checked = false;
                if (typeof showFeedback === 'function') {
                    showFeedback('Please login to enable keyboard sound.', 'error');
                }
                return;
            }
            keyboardSoundEnabled = this.checked;
            setPref('keyboardSoundEnabled', keyboardSoundEnabled);
            attachKeyboardListener();
            if (typeof showFeedback === 'function') {
                showFeedback(`Keyboard sound ${keyboardSoundEnabled ? 'enabled' : 'disabled'}.`);
            }
        });
    }

    // If the sound type select exists, sync it
    const soundTypeSelect = document.getElementById('keyboardSoundType');
    if (soundTypeSelect) {
        soundTypeSelect.value = selectedSoundKey;
        soundTypeSelect.addEventListener('change', function() {
            setKeyboardSoundType(this.value);
        });
    }
})();
