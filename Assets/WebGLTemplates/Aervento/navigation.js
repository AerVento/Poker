(function ()
{
    "use strict";

    var header = document.getElementById("aervento-navigation");
    var links = document.getElementById("aervento-navigation-links");
    var toggle = header && header.querySelector(".aervento-navigation__toggle");
    var warningPrefix = "[Aervento navigation] ";

    if (!header || !links || !toggle)
    {
        console.warn(warningPrefix + "Navigation markup is missing; the game can still load.");
        if (header)
        {
            header.hidden = true;
        }
        return;
    }

    var mobile = window.matchMedia("(max-width: 767px)");

    function closeMenu(restoreFocus)
    {
        header.classList.remove("aervento-navigation--open");
        toggle.setAttribute("aria-expanded", "false");
        if (restoreFocus)
        {
            toggle.focus();
        }
    }

    function updateLayout()
    {
        closeMenu(false);
        toggle.hidden = !mobile.matches;
    }

    function createItem(item, index)
    {
        if (!item || typeof item !== "object" || Array.isArray(item) ||
            typeof item.label !== "string" || !item.label.trim() ||
            typeof item.url !== "string" || !item.url.trim() ||
            (Object.prototype.hasOwnProperty.call(item, "newTab") && typeof item.newTab !== "boolean"))
        {
            console.warn(warningPrefix + "Skipped items[" + index + "]: expected label, url and optional boolean newTab.");
            return null;
        }

        var url;
        try
        {
            url = new URL(item.url.trim(), document.baseURI);
            if (url.protocol !== "http:" && url.protocol !== "https:")
            {
                throw new Error("Only HTTP/HTTPS links and relative paths are supported.");
            }
        }
        catch (error)
        {
            console.warn(warningPrefix + "Skipped items[" + index + "]: invalid or unsupported URL.");
            return null;
        }

        var listItem = document.createElement("li");
        var anchor = document.createElement("a");
        listItem.className = "aervento-navigation__item";
        anchor.className = "aervento-navigation__link";
        anchor.textContent = item.label.trim();
        anchor.href = url.href;
        if (item.newTab !== false)
        {
            anchor.target = "_blank";
            anchor.rel = "noopener noreferrer";
        }
        listItem.appendChild(anchor);
        return listItem;
    }

    async function loadNavigation()
    {
        var controller = new AbortController();
        var timeout = window.setTimeout(function ()
        {
            controller.abort();
        }, 5000);

        try
        {
            var response = await fetch(new URL("./navigation.json", document.baseURI),
            {
                cache: "no-store",
                signal: controller.signal
            });
            if (!response.ok)
            {
                throw new Error("navigation.json returned HTTP " + response.status + ".");
            }

            var config = await response.json();
            if (!config || typeof config !== "object" || Array.isArray(config) || !Array.isArray(config.items))
            {
                throw new Error("navigation.json must contain an items array.");
            }

            var fragment = document.createDocumentFragment();
            config.items.forEach(function (item, index)
            {
                var listItem = createItem(item, index);
                if (listItem)
                {
                    fragment.appendChild(listItem);
                }
            });
            links.appendChild(fragment);
            if (!links.children.length)
            {
                header.hidden = true;
                return;
            }

            toggle.addEventListener("click", function ()
            {
                var expanded = toggle.getAttribute("aria-expanded") === "true";
                header.classList.toggle("aervento-navigation--open", !expanded);
                toggle.setAttribute("aria-expanded", String(!expanded));
            });
            header.addEventListener("keydown", function (event)
            {
                if (event.key === "Escape" && toggle.getAttribute("aria-expanded") === "true")
                {
                    event.preventDefault();
                    closeMenu(true);
                }
            });
            links.addEventListener("click", function (event)
            {
                if (mobile.matches && event.target.closest("a"))
                {
                    closeMenu(true);
                }
            });
            mobile.addEventListener("change", updateLayout);
            updateLayout();
        }
        catch (error)
        {
            header.hidden = true;
            var message = error.name === "AbortError" ? "navigation.json timed out after 5 seconds." : error.message;
            console.warn(warningPrefix + message + " Navigation is hidden; the game can still load.");
        }
        finally
        {
            window.clearTimeout(timeout);
        }
    }

    loadNavigation();
}());
