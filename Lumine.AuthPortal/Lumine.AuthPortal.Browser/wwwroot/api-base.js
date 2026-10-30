function normalizeApiBaseUrl(value) {
    return (value || '').trim().replace(/\/+$/, '');
}

const sessionStorageKey = 'lumine.authportal.session';
const themeStorageKey = 'lumine.authportal.theme';

globalThis.lumineAuthPortalSession = {
    load() {
        try {
            return globalThis.localStorage.getItem(sessionStorageKey);
        } catch {
            return null;
        }
    },
    save(payload) {
        try {
            globalThis.localStorage.setItem(sessionStorageKey, payload);
        } catch {
        }
    },
    clear() {
        try {
            globalThis.localStorage.removeItem(sessionStorageKey);
        } catch {
        }
    }
};

globalThis.lumineAuthPortalTheme = {
    load() {
        try {
            return globalThis.localStorage.getItem(themeStorageKey);
        } catch {
            return null;
        }
    },
    save(payload) {
        try {
            globalThis.localStorage.setItem(themeStorageKey, payload);
        } catch {
        }
    }
};

globalThis.lumineAuthPortalClipboard = {
    writeText(text) {
        const normalizedText = String(text ?? '');
        if (!normalizedText) {
            return Promise.resolve(false);
        }

        // Keep the synchronous fallback inside the original click gesture.
        try {
            const textarea = globalThis.document.createElement('textarea');
            textarea.value = normalizedText;
            textarea.setAttribute('readonly', '');
            textarea.style.position = 'fixed';
            textarea.style.left = '-10000px';
            textarea.style.top = '0';
            textarea.style.opacity = '0';
            globalThis.document.body.appendChild(textarea);
            textarea.focus();
            textarea.select();
            const copied = globalThis.document.execCommand('copy');
            textarea.remove();
            if (copied) {
                return Promise.resolve(true);
            }
        } catch {
        }

        try {
            if (globalThis.navigator?.clipboard?.writeText) {
                return globalThis.navigator.clipboard.writeText(normalizedText)
                    .then(() => true)
                    .catch(() => false);
            }
        } catch {
        }

        return Promise.resolve(false);
    }
};

globalThis.resolveApiBaseUrl = async function resolveApiBaseUrl() {
    const params = new URLSearchParams(globalThis.location.search);
    const storageKey = 'lumine.authportal.apiBaseUrl';

    const queryApiBase = normalizeApiBaseUrl(params.get('apiBase'));
    if (queryApiBase) {
        try {
            globalThis.localStorage.setItem(storageKey, queryApiBase);
        } catch {
        }

        globalThis.API_BASE_URL = queryApiBase;
        return queryApiBase;
    }

    try {
        const storedApiBase = normalizeApiBaseUrl(globalThis.localStorage.getItem(storageKey));
        if (storedApiBase) {
            globalThis.API_BASE_URL = storedApiBase;
            return storedApiBase;
        }
    } catch {
    }

    try {
        const response = await fetch('/config', { cache: 'no-store' });
        if (response.ok) {
            const config = await response.json();
            const configuredApiBase = normalizeApiBaseUrl(config.apiBase);
            if (configuredApiBase) {
                globalThis.API_BASE_URL = configuredApiBase;
                globalThis.API_PORT = config.port || '';
                return configuredApiBase;
            }
        }
    } catch {
    }

    const fallbackApiBase = normalizeApiBaseUrl(globalThis.location.origin) || 'http://localhost:14255';
    globalThis.API_BASE_URL = fallbackApiBase;
    return fallbackApiBase;
};
