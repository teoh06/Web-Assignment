// Personalization settings (keyboard sound) - system-wide
const keyboardSounds = {
    classic: '/sounds/keyboard-click.mp3',
    magic: '/sounds/magic-click.mp3',
    electronic: '/sounds/electronic-click.mp3'
};
let selectedSoundKey = getCookie('keyboardSoundType') || localStorage.getItem('keyboardSoundType') || 'classic';
let keyboardAudio = null;
let keyboardSoundEnabled = false;

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
    if (keyboardSoundEnabled && (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA')) {
        playKeyboardSound();
    }
}

function setKeyboardSoundType(type) {
    if (keyboardSounds[type]) {
        selectedSoundKey = type;
        setCookie('keyboardSoundType', type, 365);
        localStorage.setItem('keyboardSoundType', type);
    }
}

// System-wide initialization
(function() {
    // Priority: cookie > localStorage
    let setting = getCookie('keyboardSoundEnabled');
    if (setting === null && localStorage.getItem('keyboardSoundEnabled') !== null) {
        setting = localStorage.getItem('keyboardSoundEnabled');
    }
    keyboardSoundEnabled = setting === 'true';
    document.addEventListener('keydown', handleKeydownSound, true);

    // If the toggle exists on this page, sync it
    const keyboardToggle = document.getElementById('keyboardSoundToggle');
    if (keyboardToggle) {
        keyboardToggle.checked = keyboardSoundEnabled;
        keyboardToggle.addEventListener('change', function() {
            keyboardSoundEnabled = this.checked;
            setCookie('keyboardSoundEnabled', keyboardSoundEnabled, 365);
            localStorage.setItem('keyboardSoundEnabled', keyboardSoundEnabled);
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
