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

        host.querySelectorAll("#lease-caret").forEach(function (marker) { marker.remove(); });
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

    if (command === "pageBreak") {
        insertPageBreak(host);
        host.classList.add("is-edited");
        scheduleReflow(host);
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
    scheduleReflow(host);
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
    reflow(host);
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
    host.querySelectorAll("section.docx").forEach(enablePage);
    reflow(host);

    if (host.dataset.bound === "1") return;
    host.dataset.bound = "1";
    host.addEventListener("input", function () {
        host.classList.add("is-edited");
        if (host._composing || host._suppress) return;
        scheduleReflow(host);
    });
    host.addEventListener("compositionstart", function () { host._composing = true; });
    host.addEventListener("compositionend", function () {
        host._composing = false;
        scheduleReflow(host);
    });
    host.addEventListener("keydown", function () {
        host._keptTop = host.scrollTop;
        host._keptLeft = host.scrollLeft;
        host._freezeScroll = true;
        window.clearTimeout(host._freezeTimer);
        host._freezeTimer = window.setTimeout(function () { host._freezeScroll = false; }, 180);
    }, true);
    host.addEventListener("scroll", function () {
        if (host._holdingScroll) return;
        if (host._freezeScroll) {
            holdScroll(host, host._keptTop || 0, host._keptLeft || 0);
            return;
        }
        host._keptTop = host.scrollTop;
        host._keptLeft = host.scrollLeft;
    });
    host.addEventListener("keydown", onPageKeyDown);
    host.addEventListener("keyup", saveSelection);
    host.addEventListener("mouseup", saveSelection);
}

function enablePage(page) {
    page.setAttribute("contenteditable", "true");
    page.setAttribute("spellcheck", "true");
}

function scheduleReflow(host) {
    window.clearTimeout(host._reflowTimer);
    host._reflowTimer = window.setTimeout(function () { reflow(host); }, 40);
}

function onPageKeyDown(event) {
    const host = event.currentTarget;
    const page = event.target.closest && event.target.closest("section.docx");
    if (!page || !host.contains(page)) return;

    if (event.key === "Enter" && !event.shiftKey && !event.isComposing) {
        if (insideTable()) return;
        event.preventDefault();
        insertParagraph();
        host.classList.add("is-edited");
        scheduleReflow(host);
        return;
    }

    if (event.key !== "Backspace" && event.key !== "Delete") return;
    const pages = pageList(host);
    const index = pages.indexOf(page);
    if (event.key === "Backspace" && index > 0 && caretAt(page, "start")) {
        event.preventDefault();
        host.classList.add("is-edited");
        pullAcrossPages(host, page, "backward");
    } else if (event.key === "Delete" && index >= 0 && index < pages.length - 1 && caretAt(page, "end")) {
        event.preventDefault();
        host.classList.add("is-edited");
        pullAcrossPages(host, pages[index + 1], "backward");
    }
}

function insideTable() {
    const selection = window.getSelection();
    const node = selection && selection.anchorNode;
    const element = node && (node.nodeType === 1 ? node : node.parentElement);
    return !!(element && element.closest && element.closest("table"));
}

function currentBlock() {
    const selection = window.getSelection();
    if (!selection || selection.rangeCount === 0) return null;
    let node = selection.anchorNode;
    if (!node) return null;
    if (node.nodeType === 3) node = node.parentElement;
    return node && node.closest ? node.closest("p, h1, h2, h3, li") : null;
}

function placeCaret(node, offset) {
    const host = editingHost(node);
    const top = host ? host.scrollTop : 0;
    const left = host ? host.scrollLeft : 0;
    const range = document.createRange();
    range.setStart(node, Math.min(offset, node.nodeType === 3 ? node.length : node.childNodes.length));
    range.collapse(true);
    const selection = window.getSelection();
    selection.removeAllRanges();
    selection.addRange(range);
    if (host) holdScroll(host, top, left);
}

function insertParagraph() {
    const selection = window.getSelection();
    let block = currentBlock();
    if (!block && selection && selection.anchorNode) {
        const element = selection.anchorNode.nodeType === 1 ? selection.anchorNode : selection.anchorNode.parentElement;
        const section = element && element.closest && element.closest("section.docx");
        if (section) block = articleOf(section).lastElementChild;
    }
    if (!selection || !block) return;

    const fresh = document.createElement(block.tagName === "LI" ? "li" : "p");
    fresh.appendChild(document.createElement("br"));
    if (selection.isCollapsed && block.contains(selection.anchorNode)) {
        const tail = document.createRange();
        const range = selection.getRangeAt(0);
        tail.setStart(range.startContainer, range.startOffset);
        tail.setEnd(block, block.childNodes.length);
        const extracted = tail.extractContents();
        if (extracted.textContent.replace(/\u200b/g, "").length || extracted.querySelector("img, table, br"))
            fresh.replaceChildren(extracted);
    }
    if (!fresh.firstChild) fresh.appendChild(document.createElement("br"));
    block.after(fresh);
    placeCaret(fresh, 0);
}

function insertPageBreak(host) {
    restoreSelection(host);
    const block = currentBlock();
    const marker = document.createElement("p");
    marker.dataset.pageBreak = "1";
    marker.appendChild(document.createElement("br"));
    if (block) block.after(marker);
    else {
        const article = articleOf(pageList(host).at(-1) || host.querySelector("section.docx"));
        article.appendChild(marker);
    }
    placeCaret(marker, 0);
}

function pullAcrossPages(host, page, direction) {
    markCaret();
    const block = articleOf(page).firstElementChild;
    reflow(host);
    if (!block || !block.isConnected) return;
    const owner = block.closest("section.docx");
    const pages = pageList(host);
    if (!owner || pages.indexOf(owner) <= 0) return;
    if (articleOf(owner).firstElementChild !== block) return;
    if (direction === "backward") joinWithPrevious(block);
    reflow(host);
}

function joinWithPrevious(block) {
    const page = block.closest("section.docx");
    const host = page && page.closest(".studio-pages");
    if (!host) return;
    const pages = pageList(host);
    const index = pages.indexOf(page);
    if (index <= 0) return;
    const last = articleOf(pages[index - 1]).lastElementChild;
    if (last && last.tagName === "P" && block.tagName === "P") {
        while (block.firstChild) last.appendChild(block.firstChild);
        block.remove();
        return;
    }
    articleOf(pages[index - 1]).appendChild(block);
}

function reflow(host) {
    if (!host || host._reflowing || !host.querySelector("section.docx")) return;
    const top = host._freezeScroll && host._keptTop != null ? host._keptTop : host.scrollTop;
    const left = host._freezeScroll && host._keptLeft != null ? host._keptLeft : host.scrollLeft;
    host._reflowing = true;
    host._suppress = true;
    markCaret();
    try {
        layoutPages(host);
    } finally {
        restoreCaret();
        host._reflowing = false;
        fitPages(host);
        holdScroll(host, top, left);
        window.setTimeout(function () { host._suppress = false; }, 0);
    }
}

function editingHost(node) {
    const element = node && (node.nodeType === 1 ? node : node.parentElement);
    return element && element.closest ? element.closest(".studio-pages") : null;
}

function holdScroll(host, top, left) {
    const apply = function () {
        host._holdingScroll = true;
        host.scrollTop = top;
        host.scrollLeft = left;
        const selection = window.getSelection();
        if (selection && selection.rangeCount > 0 && selection.anchorNode && host.contains(selection.anchorNode)) {
            const rect = selection.getRangeAt(0).getBoundingClientRect();
            const box = host.getBoundingClientRect();
            if (rect && (rect.height !== 0 || rect.width !== 0)) {
                if (rect.bottom > box.bottom - 16)
                    host.scrollTop += rect.bottom - (box.bottom - 16);
                else if (rect.top < box.top + 16)
                    host.scrollTop -= (box.top + 16) - rect.top;
            }
        }
        host._keptTop = host.scrollTop;
        host._keptLeft = host.scrollLeft;
        host._holdingScroll = false;
    };
    apply();
    requestAnimationFrame(function () {
        apply();
        requestAnimationFrame(apply);
    });
}

function layoutPages(host) {
    const wrapper = host.querySelector(".docx-wrapper") || host;
    const existing = pageList(host);
    if (!existing.length) return;
    existing.forEach(function (page) { page.style.zoom = ""; });
    const proto = existing[0];
    const blocks = [];
    existing.forEach(function (page) {
        adoptLoose(page);
        normalizeArticle(articleOf(page));
        articleOf(page).querySelectorAll(":scope > *").forEach(function (block) {
            blocks.push(block);
        });
    });

    existing.slice(1).forEach(function (page) { page.remove(); });
    const first = existing[0];
    articleOf(first).replaceChildren();
    let page = first;
    let article = articleOf(first);
    const queue = blocks.slice();

    while (queue.length && pageList(host).length < blocks.length + 8) {
        const block = queue.shift();
        article.appendChild(block);
        if (block.dataset && block.dataset.pageBreak === "1") {
            if (article.childElementCount > 1) {
                article.removeChild(block);
                page = addPage(proto, wrapper);
                article = articleOf(page);
                article.appendChild(block);
            }
            continue;
        }
        if (article.scrollHeight <= contentLimit(page)) continue;

        if (article.childElementCount > 1) {
            article.removeChild(block);
            const tail = splitToFit(article, block, contentLimit(page));
            if (tail && plainText(block)) article.appendChild(block);
            page = addPage(proto, wrapper);
            article = articleOf(page);
            queue.unshift(tail || block);
            continue;
        }

        const tail = splitToFit(article, block, contentLimit(page));
        if (!tail) continue;
        page = addPage(proto, wrapper);
        article = articleOf(page);
        queue.unshift(tail);
    }

    pageList(host).forEach(function (sheet) {
        if (sheet !== first && articleOf(sheet).childElementCount === 0) sheet.remove();
    });
}

function splitToFit(article, block, limit) {
    const text = plainText(block);
    if (!text || block.tagName === "TABLE" || block.tagName === "UL" || block.tagName === "OL")
        return null;

    let low = 0;
    let high = text.length;
    let best = 0;
    while (low <= high) {
        const mid = (low + high) >> 1;
        if (mid === 0 || measure(article, block, mid) <= limit) {
            best = mid;
            low = mid + 1;
        } else {
            high = mid - 1;
        }
    }

    best = snapToWord(text, best);
    if (best < 1 || best >= text.length) return null;

    const point = pointAt(block, best);
    if (!point) return null;
    const range = document.createRange();
    range.setStart(point.node, point.offset);
    range.setEnd(block, block.childNodes.length);
    const tail = block.cloneNode(false);
    tail.appendChild(range.extractContents());
    if (!plainText(tail)) return null;
    return tail;
}

function measure(article, block, count) {
    const parked = block.parentElement === article;
    if (parked) block.remove();
    const clone = block.cloneNode(true);
    clone.querySelectorAll("#lease-caret").forEach(function (node) { node.remove(); });
    truncate(clone, count);
    article.appendChild(clone);
    const height = article.scrollHeight;
    clone.remove();
    if (parked) article.appendChild(block);
    return height;
}

function truncate(root, count) {
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, textFilter);
    let seen = 0;
    const extra = [];
    let node;
    while ((node = walker.nextNode())) {
        if (seen >= count) {
            extra.push(node);
            continue;
        }
        if (seen + node.length > count) {
            node.textContent = node.textContent.slice(0, count - seen);
            seen = count;
        } else {
            seen += node.length;
        }
    }
    extra.forEach(function (item) { item.textContent = ""; });
}

function snapToWord(text, index) {
    if (index <= 0 || index >= text.length) return index;
    const previous = text.slice(0, index);
    const space = Math.max(previous.lastIndexOf(" "), previous.lastIndexOf("\n"), previous.lastIndexOf("\u00a0"));
    if (space > 0 && index - space < 48) return space + 1;
    return index;
}

function pointAt(root, count) {
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_TEXT, textFilter);
    let left = count;
    let node;
    while ((node = walker.nextNode())) {
        if (left <= node.length) return { node: node, offset: left };
        left -= node.length;
    }
    return null;
}

function textFilter(node) {
    if (node.parentElement && node.parentElement.id === "lease-caret")
        return NodeFilter.FILTER_REJECT;
    return NodeFilter.FILTER_ACCEPT;
}

function plainText(node) {
    return (node.textContent || "").replace(/\u200b/g, "");
}

function pageList(host) {
    return [...host.querySelectorAll("section.docx")];
}

function articleOf(page) {
    let article = page.querySelector(":scope > article");
    if (!article) {
        article = document.createElement("article");
        page.appendChild(article);
    }
    return article;
}

function contentLimit(page) {
    const style = getComputedStyle(page);
    const padding = parseFloat(style.paddingTop) + parseFloat(style.paddingBottom);
    return Math.max(page.clientHeight - padding - 4, 120);
}

function addPage(proto, wrapper) {
    const page = proto.cloneNode(false);
    page.style.zoom = "";
    page.appendChild(document.createElement("article"));
    wrapper.appendChild(page);
    enablePage(page);
    return page;
}

function caretAt(page, edge) {
    const selection = window.getSelection();
    if (!selection || selection.rangeCount === 0 || !selection.isCollapsed) return false;
    const article = articleOf(page);
    if (!article.contains(selection.anchorNode)) return false;
    const probe = selection.getRangeAt(0).cloneRange();
    probe.selectNodeContents(article);
    const range = selection.getRangeAt(0);
    if (edge === "start") probe.setEnd(range.startContainer, range.startOffset);
    else probe.setStart(range.startContainer, range.startOffset);
    return probe.toString().replace(/\u200b/g, "").length === 0;
}

function markCaret() {
    const host = document.querySelector(".studio-pages");
    const selection = window.getSelection();
    if (!host || !selection || selection.rangeCount === 0 || !host.contains(selection.anchorNode)) return;
    if (document.getElementById("lease-caret")) return;
    const marker = document.createElement("span");
    marker.id = "lease-caret";
    marker.textContent = "\u200b";
    selection.getRangeAt(0).insertNode(marker);
}

function restoreCaret() {
    const marker = document.getElementById("lease-caret");
    if (!marker || !marker.parentNode) return;
    const previous = marker.previousSibling;
    const parent = marker.parentNode;
    const index = [...parent.childNodes].indexOf(marker);
    marker.remove();
    const range = document.createRange();
    if (previous && previous.nodeType === Node.TEXT_NODE && previous.isConnected)
        range.setStart(previous, previous.length);
    else if (parent.isConnected)
        range.setStart(parent, Math.min(index, parent.childNodes.length));
    else
        return;
    range.collapse(true);
    const selection = window.getSelection();
    selection.removeAllRanges();
    selection.addRange(range);
}

function adoptLoose(page) {
    const article = articleOf(page);
    [...page.childNodes].forEach(function (node) {
        if (node === article) return;
        if (node.nodeType === 3 && !(node.textContent || "").trim()) {
            node.remove();
            return;
        }
        if (node.nodeType === 1 && node.tagName === "STYLE") return;
        article.appendChild(node);
    });
}

function normalizeArticle(article) {
    [...article.querySelectorAll(":scope > div")].forEach(function (div) {
        const paragraph = document.createElement("p");
        while (div.firstChild) paragraph.appendChild(div.firstChild);
        if (!paragraph.firstChild) paragraph.appendChild(document.createElement("br"));
        div.replaceWith(paragraph);
    });
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
