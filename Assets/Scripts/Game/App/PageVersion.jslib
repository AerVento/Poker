mergeInto(LibraryManager.library, {
    // Reloads with ?<parameter>=<version>, so the page can tell that it already refreshed for that version.
    PokerPage_Reload: function (parameter, version) {
        var url = new URL(window.location.href);
        url.searchParams.set(UTF8ToString(parameter), UTF8ToString(version));
        window.location.replace(url.toString());
    }
});
