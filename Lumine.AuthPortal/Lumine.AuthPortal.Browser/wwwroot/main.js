const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

async function clearBrowserCaches() {
    const cleanupTasks = [];

    if ('serviceWorker' in navigator) {
        cleanupTasks.push(
            navigator.serviceWorker.getRegistrations()
                .then(registrations => Promise.all(registrations.map(registration => registration.unregister())))
        );
    }

    if ('caches' in globalThis) {
        cleanupTasks.push(
            caches.keys()
                .then(cacheKeys => Promise.all(cacheKeys.map(cacheKey => caches.delete(cacheKey))))
        );
    }

    if (cleanupTasks.length === 0) {
        return;
    }

    await Promise.allSettled(cleanupTasks);
}

await clearBrowserCaches();

const frameworkVersion = `${Date.now()}`;
const { dotnet } = await import(`./_framework/dotnet.js?v=${frameworkVersion}`);

const resolvedApiBaseUrl = globalThis.resolveApiBaseUrl
    ? await globalThis.resolveApiBaseUrl()
    : globalThis.location.origin;

globalThis.API_BASE_URL = resolvedApiBaseUrl;

try {
    const dotnetRuntime = await dotnet
        .withDiagnosticTracing(false)
        .withApplicationArgumentsFromQuery()
        .create();

    const config = dotnetRuntime.getConfig();

    await dotnetRuntime.runMain(config.mainAssemblyName, [`apiBase=${resolvedApiBaseUrl}`]);
} catch (error) {
    console.error('AuthPortal WebAssembly 启动失败。', error);
    throw error;
}
