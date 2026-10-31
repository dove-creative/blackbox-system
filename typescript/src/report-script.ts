// Browser code embedded into HTML exports. After tsc, scripts/embed-report-script.mjs rewrites the
// compiled module as a string literal, so bundler or coverage transforms in consumer builds cannot
// inject helpers into the embedded body. The function must therefore stay free of module dependencies.
function initializeReport(): void {
    const rows = Array.from(document.querySelectorAll<HTMLElement>(".log-row"));
    const collapsed = new Set<string>();
    const highlightTimers = new WeakMap<Element, ReturnType<typeof setTimeout>>();
    const reducedMotion = window.matchMedia("(prefers-reduced-motion: reduce)");
    const ancestors = (row: HTMLElement): string[] => (row.dataset.scopeAncestors ?? "").split(" ").filter(Boolean);

    function refresh(): void {
        for (const row of rows) {
            row.hidden = ancestors(row).some(key => collapsed.has(key));
            const key = row.dataset.scopeKey;
            const folded = key !== undefined && collapsed.has(key);
            row.classList.toggle("folded", folded);
            const button = row.querySelector<HTMLButtonElement>(".scope-toggle");
            if (button) {
                button.setAttribute("aria-expanded", String(!folded));
                button.setAttribute("aria-label", folded ? "Expand scope" : "Collapse scope");
                button.textContent = folded ? "▸" : "▾";
            }
        }
    }

    document.addEventListener("click", event => {
        if (!(event.target instanceof Element)) return;
        const scopeButton = event.target.closest(".scope-toggle");
        const scopeRow = scopeButton?.closest<HTMLElement>(".log-row");
        if (scopeRow?.dataset.scopeKey) {
            const key = scopeRow.dataset.scopeKey;
            if (collapsed.has(key)) collapsed.delete(key);
            else collapsed.add(key);
            refresh();
            return;
        }

        const action = event.target.closest<HTMLElement>("[data-action]")?.dataset.action;
        if (action === "expand" || action === "collapse") {
            collapsed.clear();
            if (action === "collapse")
                for (const row of rows)
                    if (row.dataset.scopeKey) collapsed.add(row.dataset.scopeKey);
            refresh();
            return;
        }

        const link = event.target.closest<HTMLAnchorElement>('a[href^="#"]');
        if (!link) return;
        const destination = document.getElementById(link.getAttribute("href")!.slice(1));
        if (!destination) return;
        event.preventDefault();
        const row = destination.closest<HTMLElement>(".log-row");
        if (row) {
            for (const key of ancestors(row)) collapsed.delete(key);
            refresh();
        }
        const target = row ?? destination.querySelector(".header") ?? destination;
        target.scrollIntoView({ behavior: reducedMotion.matches ? "instant" : "smooth", block: "center", inline: "nearest" });
        clearTimeout(highlightTimers.get(target));
        target.classList.remove("highlight");
        // Restart the highlight on repeated jumps to the same retained log.
        if (target instanceof HTMLElement) void target.offsetWidth;
        target.classList.add("highlight");
        highlightTimers.set(target, setTimeout(() => target.classList.remove("highlight"), 1600));
    });
}

export const reportScript = `(${initializeReport.toString()})();`;
