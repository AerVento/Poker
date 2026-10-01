// Browser storage for remote content (see RemoteBundleStore.cs). Bundles go through Unity's own cachedFetch so a
// later UnityWebRequest for the same URL is served from Unity's cache; this depends on Unity's WebGL loader internals
// (Module.cachedFetch, Module.UnityCache) and must be re-checked when upgrading Unity.
var PokerContentLibrary = {
    $PokerContent: {
        next: 1,
        downloads: {},
        cache: function () {
            return Module.UnityCache && Module.UnityCache.getInstance ? Module.UnityCache.getInstance() : null;
        },
        resolve: function (url) {
            var link = document.createElement("a");
            link.href = url;
            return link.href;
        }
    },

    PokerContent_ReadRecords: function (key) {
        var value = "";
        try { value = window.localStorage.getItem(UTF8ToString(key)) || ""; } catch (e) { }
        var size = lengthBytesUTF8(value) + 1;
        var buffer = _malloc(size);
        stringToUTF8(value, buffer, size);
        return buffer;
    },

    PokerContent_WriteRecords: function (key, json) {
        try { window.localStorage.setItem(UTF8ToString(key), UTF8ToString(json)); }
        catch (e) { console.warn("[PokerContent] Cannot save content records: " + e); }
    },

    // Progress: 0..1 while downloading, 2 when stored, -1 on failure.
    PokerContent_Download: function (url) {
        var id = PokerContent.next++;
        var state = { progress: 0, controller: window.AbortController ? new AbortController() : null };
        PokerContent.downloads[id] = state;
        var target = PokerContent.resolve(UTF8ToString(url));
        var init = {
            method: "GET",
            signal: state.controller ? state.controller.signal : undefined,
            onProgress: function (e) {
                if (e.total > 0) state.progress = Math.min(0.99, e.loaded / e.total);
            }
        };
        var request;
        if (Module.cachedFetch && Module.companyName && Module.productName && Module.cacheControl) {
            init.companyName = Module.companyName;
            init.productName = Module.productName;
            init.productVersion = Module.productVersion;
            init.control = Module.cacheControl(target);
            request = Module.cachedFetch(target, init);
        } else {
            request = fetch(target, init);
        }
        request.then(function (response) {
            if (!response.ok && response.status !== 304) throw new Error("HTTP " + response.status);
            // cachedFetch stores the body asynchronously after resolving; wait until the entry is visible.
            var unityCache = PokerContent.cache();
            if (!unityCache) return;
            var attempts = 0;
            function stored() {
                return unityCache.isConnected.then(function () {
                    return unityCache.cache.match(target);
                }).then(function (match) {
                    if (match || ++attempts >= 50) return;
                    return new Promise(function (resolve) { setTimeout(resolve, 100); }).then(stored);
                });
            }
            return stored();
        }).then(function () {
            state.progress = 2;
        }).catch(function (error) {
            console.warn("[PokerContent] Download failed: " + target + " / " + error);
            state.progress = -1;
        });
        return id;
    },

    PokerContent_Progress: function (id) {
        var state = PokerContent.downloads[id];
        return state ? state.progress : -1;
    },

    PokerContent_Release: function (id) {
        var state = PokerContent.downloads[id];
        if (state && state.progress >= 0 && state.progress <= 1 && state.controller) state.controller.abort();
        delete PokerContent.downloads[id];
    },

    PokerContent_Delete: function (url) {
        var unityCache = PokerContent.cache();
        if (!unityCache) return;
        var target = PokerContent.resolve(UTF8ToString(url));
        unityCache.isConnected.then(function () {
            return Promise.all([
                unityCache.cache.delete(target),
                unityCache.execute("RequestMetaDataStore", "delete", [target])
            ]);
        }).catch(function (error) {
            console.warn("[PokerContent] Cannot delete cached bundle: " + target + " / " + error);
        });
    }
};

autoAddDeps(PokerContentLibrary, "$PokerContent");
mergeInto(LibraryManager.library, PokerContentLibrary);
