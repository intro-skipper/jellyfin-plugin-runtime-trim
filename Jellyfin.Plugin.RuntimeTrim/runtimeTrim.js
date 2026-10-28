// Ends direct play at the item's runtime. The plugin trims the runtime of files whose audio
// runs past the video, but the browser still plays the file to its container end. Seeking to
// the end fires the element's native `ended`, so jellyfin-web finishes playback as usual. An
// untrimmed file reaches its runtime only at its end, where the seek changes nothing.
(() => {
    const runtimes = new Map();

    // Runtime in seconds for a direct play URL, or undefined for anything else (HLS uses a blob: URL).
    async function fetchRuntime(src) {
        const url = new URL(src, location.href);
        const mediaSourceId = url.searchParams.get('mediaSourceId');
        if (url.searchParams.get('Static') !== 'true' || !mediaSourceId) return undefined;

        const apiClient = window.ApiClient;
        const item = await apiClient.getItem(apiClient.getCurrentUserId(), mediaSourceId);
        return item.RunTimeTicks / 1e7;
    }

    // Caches the lookup per URL. A failed lookup is forgotten so the next timeupdate retries.
    function runtimeOf(src) {
        if (!runtimes.has(src)) {
            runtimes.set(src, fetchRuntime(src).catch(() => {
                runtimes.delete(src);
            }));
        }
        return runtimes.get(src);
    }

    // Media events do not bubble, but a capturing listener on document still sees them.
    document.addEventListener('timeupdate', async ({ target: video }) => {
        if (!(video instanceof HTMLVideoElement) || !video.currentSrc || video.seeking || video.ended
            || !Number.isFinite(video.duration)) return;

        // jellyfin-web reuses one element for the whole queue, so the item may change during the lookup.
        const src = video.currentSrc;
        const runtime = await runtimeOf(src);
        if (video.currentSrc === src && runtime && video.currentTime >= runtime) {
            video.currentTime = video.duration;
        }
    }, true);
})();
