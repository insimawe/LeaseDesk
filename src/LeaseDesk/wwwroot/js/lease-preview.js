window.leaseDeskPreview = {
    render: async function (hostId, wordUrl, pageHtml) {
        const host = document.getElementById(hostId);
        if (!host) return;

        const token = (host._leaseToken || 0) + 1;
        host._leaseToken = token;
        host.replaceChildren();
        host.classList.add("is-loading");
        host.classList.remove("is-edited");

        try {
            if (pageHtml) {
                host.innerHTML = sanitize(pageHtml);
            } else {
                const word = await fetch(wordUrl);
                if (token !== host._leaseToken) return;
                if (!word.ok) {
                    host.appendChild(message(await word.text()));
                    return;
                }

                const buffer = await word.arrayBuffer();
                if (token !== host._leaseToken) return;
                await docx.renderAsync(buffer, host, null, {
                    className: "docx",
                    inWrapper: true,
                    breakPages: true,
                    ignoreLastRenderedPageBreak: true
                });
            }

            if (token !== host._leaseToken) return;
            prepare(host);
            fitPages(host);
        } catch (error) {
            if (token !== host._leaseToken) return;
            host.appendChild(message(error && error.message ? error.message : "The contract could not be shown."));
        } finally {
            if (token === host._leaseToken)
                host.classList.remove("is-loading");
        }
    },

    wasEdited: function (hostId) {
        const host = document.getElementById(hostId);
        return !!(host && host.classList.contains("is-edited"));
    },

    markClean: function (hostId) {
        const host = document.getElementById(hostId);
        if (host) host.classList.remove("is-edited");
    },

    read: function (hostId) {
        const host = document.getElementById(hostId);
        if (!host) return "";

        let html = host.innerHTML;
        const styles = [...document.head.querySelectorAll("style")]
            .map(function (style) { return style.outerHTML; })
            .filter(function (text) { return text.indexOf("docx") >= 0; })
            .join("");
        if (styles && html.indexOf("<style") < 0)
            html = styles + html;
        return sanitize(html);
    }
};

document.addEventListener("mousedown", function (event) {
    const tools = event.target.closest && event.target.closest(".page-tools");
    if (!tools) return;
    saveSelection();
    if (event.target.closest("button[data-command]"))
        event.preventDefault();
}, true);

document.addEventListener("click", function (event) {
    const button = event.target.closest && event.target.closest(".page-tools button[data-command]");
    if (!button) return;
    event.preventDefault();
    apply(button.dataset.command, button.dataset.value || null);
});

document.addEventListener("change", function (event) {
    const control = event.target.closest && event.target.closest(".page-tools [data-command]");
    if (!control || control.tagName === "BUTTON") return;
    apply(control.dataset.command, control.value);
});

let savedRange = null;

function saveSelection() {
    const host = document.querySelector(".studio-pages");
    const selection = window.getSelection();
    if (!host || !selection || selection.rangeCount === 0) return;
    if (!host.contains(selection.anchorNode)) return;
    savedRange = selection.getRangeAt(0).cloneRange();
}

function restoreSelection(host) {
    const page = host.querySelector("section.docx") || host;
    if (page && page.focus) page.focus({ preventScroll: true });
    if (!savedRange) return;
    const selection = window.getSelection();
    selection.removeAllRanges();
    try {
        selection.addRange(savedRange);
    } catch (error) {
        savedRange = null;
    }
}

function apply(command, value) {
    const host = document.querySelector(".studio-pages");
    if (!host) return;

    if (command === "paper" || command === "margins") {
        applyLayout(host, command, value);
        host.classList.add("is-edited");
        return;
    }

    if (command === "lineHeight") {
        restoreSelection(host);
        const block = selectedBlock();
        if (block) block.style.lineHeight = value;
        host.classList.add("is-edited");
        saveSelection();
        return;
    }

    restoreSelection(host);
    document.execCommand("styleWithCSS", false, true);

    if (command === "fontSize") {
        document.execCommand("fontSize", false, "7");
        host.querySelectorAll("font[size='7']").forEach(function (node) {
            node.removeAttribute("size");
            node.style.fontSize = value;
        });
        host.querySelectorAll("span").forEach(function (node) {
            const size = (node.style.fontSize || "").toLowerCase();
            if (size === "xxx-large" || size === "-webkit-xxx-large")
                node.style.fontSize = value;
        });
    } else if (command === "formatBlock") {
        if (!document.execCommand("formatBlock", false, value))
            document.execCommand("formatBlock", false, "<" + value + ">");
    } else if (value) {
        document.execCommand(command, false, value);
    } else {
        document.execCommand(command, false, false);
    }

    host.classList.add("is-edited");
    saveSelection();
}

function applyLayout(host, command, value) {
    const pages = host.querySelectorAll("section.docx");
    pages.forEach(function (page) {
        if (command === "paper" && value === "letter") {
            page.style.width = "816px";
            page.style.minHeight = "1056px";
        } else if (command === "paper" && value === "a4") {
            page.style.width = "794px";
            page.style.minHeight = "1123px";
        } else if (command === "margins" && value === "narrow") {
            page.style.padding = "48px";
        } else if (command === "margins" && value === "normal") {
            page.style.padding = "96px";
        } else if (command === "margins" && value === "wide") {
            page.style.padding = "72px 120px";
        }
    });
    fitPages(host);
}

function selectedBlock() {
    const selection = window.getSelection();
    if (!selection || selection.rangeCount === 0) return null;
    let node = selection.anchorNode;
    if (!node) return null;
    if (node.nodeType === 3) node = node.parentElement;
    return node && node.closest ? node.closest("p, h1, h2, h3, li, td") : null;
}

function fitPages(host) {
    const pages = host.querySelectorAll("section.docx");
    const available = host.clientWidth - 32;
    pages.forEach(function (page) {
        page.style.zoom = "";
        const width = page.getBoundingClientRect().width;
        if (available > 80 && width > available)
            page.style.zoom = String(Math.max(available / width, 0.42));
    });
}

let fitTimer = 0;
window.addEventListener("resize", function () {
    window.clearTimeout(fitTimer);
    fitTimer = window.setTimeout(function () {
        document.querySelectorAll(".studio-pages").forEach(fitPages);
    }, 120);
});

function prepare(host) {
    host.querySelectorAll("section.docx").forEach(function (page) {
        page.setAttribute("contenteditable", "true");
        page.setAttribute("spellcheck", "true");
    });

    if (host.dataset.bound === "1") return;
    host.dataset.bound = "1";
    host.addEventListener("input", function () {
        host.classList.add("is-edited");
    });
    host.addEventListener("keyup", saveSelection);
    host.addEventListener("mouseup", saveSelection);
}

function sanitize(html) {
    const template = document.createElement("template");
    template.innerHTML = html;
    template.content.querySelectorAll("script, iframe, object, embed, link, meta").forEach(function (node) {
        node.remove();
    });
    template.content.querySelectorAll("*").forEach(function (element) {
        [...element.attributes].forEach(function (attribute) {
            const name = attribute.name.toLowerCase();
            const value = attribute.value.trim().toLowerCase();
            if (name.startsWith("on") || value.startsWith("javascript:"))
                element.removeAttribute(attribute.name);
        });
    });
    return template.innerHTML;
}

function message(text) {
    const note = document.createElement("p");
    note.className = "studio-note";
    note.textContent = text;
    return note;
}
