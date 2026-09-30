window.leaseDeskPreview = {
    render: async function (hostId, pdfUrl, wordUrl) {
        const host = document.getElementById(hostId);
        if (!host) return;
        host.replaceChildren();
        host.classList.add("is-loading");

        try {
            const pdf = await fetch(pdfUrl);
            if (pdf.ok) {
                const blob = await pdf.blob();
                const frame = document.createElement("iframe");
                frame.className = "studio-frame";
                frame.title = "Contract PDF";
                frame.src = URL.createObjectURL(blob);
                host.appendChild(frame);
                return;
            }

            const word = await fetch(wordUrl);
            if (!word.ok) {
                host.appendChild(message(await word.text()));
                return;
            }

            const buffer = await word.arrayBuffer();
            await docx.renderAsync(buffer, host, null, {
                className: "docx",
                inWrapper: true,
                breakPages: true,
                ignoreLastRenderedPageBreak: false
            });
        } catch (error) {
            host.appendChild(message(error && error.message ? error.message : "The contract could not be shown."));
        } finally {
            host.classList.remove("is-loading");
        }
    }
};

function message(text) {
    const note = document.createElement("p");
    note.className = "studio-note";
    note.textContent = text;
    return note;
}
