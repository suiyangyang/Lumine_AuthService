function normalizeApiBaseUrl(value) {
    return (value || '').trim().replace(/\/+$/, '');
}

const sessionStorageKey = 'lumine.authportal.session';

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

    const fallbackApiBase = normalizeApiBaseUrl(globalThis.location.origin) || 'http://localhost:5115';
    globalThis.API_BASE_URL = fallbackApiBase;
    return fallbackApiBase;
};
