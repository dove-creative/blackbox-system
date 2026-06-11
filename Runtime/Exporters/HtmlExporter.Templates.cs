using System.Text;

namespace com.BlackThunder.BlackboxSystem.Exporters
{
    internal static partial class HtmlExporter
    {
        private static readonly string[] _rainbowColors =
        {
            "#ffd700",
            "#da70d6",
            "#179fff",
            "#32cd32",
            "#ff4500"
        };

        private static readonly string[] _lightRainbowColors =
        {
            "#e6dec3",
            "#dec3e6",
            "#c3dee6",
            "#c3e6c6",
            "#e6c3c3"
        };

        private const string DocumentStart =
@"<!DOCTYPE html><html><head><meta charset='utf-8'>
<title>Blackbox Log Report</title>
<style>
:root { --bg: #1e1e1e; --fg: #d4d4d4; --acc: #3794ff; --border: #333; }
* { box-sizing: border-box; }
body { font-family: 'Consolas', 'Monaco', monospace; background: var(--bg); color: var(--fg); font-size: 13px; line-height: 1.5; margin: 0; padding: 20px; }
.box { display: grid; grid-template-columns: minmax(100%, max-content); border: 1px solid var(--border); margin-bottom: 30px; background: #252526; border-radius: 6px; overflow: hidden; box-shadow: 0 4px 6px rgba(0,0,0,0.3); }
.header { width: 100%; background: #333; padding: 10px 15px; font-weight: bold; color: #fff; display: flex; justify-content: space-between; align-items: center; border-bottom: 1px solid var(--border); position: sticky; top: 0; z-index: 10; }
.header .id-badge { background: #007acc; padding: 2px 6px; border-radius: 4px; font-size: 0.9em; margin-right: 10px; }
@keyframes highlight-pulse {
    0% { background-color: rgba(255, 215, 0, 0.50); }
    100% { background-color: transparent; }
}
.log-row.highlight { animation: highlight-pulse var(--highlight-duration, 1.4s) ease-out forwards; transition: none !important; z-index: 1; position: relative; }
@keyframes highlight-header {
    0% { background-color: rgba(255, 215, 0, 0.50); }
    100% { background-color: #333; }
}
.header.highlight { animation: highlight-header var(--highlight-duration, 1.4s) ease-out forwards; transition: none !important; }
.log-container { width: 100%; padding: 10px 0; }
.log-row { display: flex; width: 100%; padding: 2px 0; cursor: pointer; transition: background 0.1s; align-items: flex-start; }
.log-row:hover { background: #2a2d2e; }
.log-row.hidden { display: none; }
.log-row.folded .content::after { content: ' ... '; background: #444; color: #fff; padding: 0 6px; border-radius: 4px; margin-left: 10px; font-size: 0.8em; display: inline-block; }
.time { color: #666; width: 210px; min-width: 210px; padding-left: 15px; text-align: left; user-select: none; }
.content { flex-grow: 1; padding-right: 15px; white-space: pre-wrap; word-break: break-all; position: relative; }
.method { font-weight: bold; }
.interaction { color: #4ec9b0; background: #1e3a3a; padding: 1px 4px; border-radius: 3px; text-decoration: none; border: 1px solid #2b5656; font-size: 0.9em; margin-left: 8px; cursor: pointer; display: inline-block; }
.interaction:hover { border-color: #4ec9b0; background: #254444; }
.interaction.disabled { color: #888; background: #2b2b2b; border-color: #444; cursor: default; opacity: 0.75; }
.interaction.disabled:hover { border-color: #444; background: #2b2b2b; }
.interaction.tag-interaction { color: #f6a04d; background: #3b2818; border-color: #704316; }
.interaction.tag-interaction:hover { border-color: #f6a04d; background: #4a311d; }
.interaction.tag-interaction.disabled { color: #b98954; background: #30261d; border-color: #5f4021; }
.interaction.tag-interaction.disabled:hover { border-color: #5f4021; background: #30261d; }
</style></head><body>
";

        private const string DocumentEnd =
@"<script>
    const highlightTimers = new WeakMap();
    const baseHighlightMs = 1400;
    const maxExtraHighlightMs = 3600;
    const pixelsPerExtraMs = 2;
    const minScrollMs = 250;
    const maxScrollMs = 900;
    const pixelsPerScrollMs = 4;

    function setFold(openRow, fold) {
        if (!openRow) return;

        const startDepth = parseInt(openRow.getAttribute('data-depth')) || 0;

        if (fold) openRow.classList.add('folded');
        else openRow.classList.remove('folded');

        let next = openRow.nextElementSibling;
        while (next) {
            const d = parseInt(next.getAttribute('data-depth')) || 0;
            const t = next.getAttribute('data-type');

            if (d > startDepth) {
                if (fold) next.classList.add('hidden');
                else {
                    next.classList.remove('hidden');
                    next.classList.remove('folded');
                }
            }
            else if (d === startDepth && t === 'Close') {
                if (fold) next.classList.add('hidden');
                else next.classList.remove('hidden');
                break;
            }
            else break;

            next = next.nextElementSibling;
        }
    }

    function ensureVisible(row) {
        if (!row) return;

        if (row.classList.contains('folded') && row.getAttribute('data-type') === 'Open') {
            setFold(row, false);
        }

        let depth = parseInt(row.getAttribute('data-depth')) || 0;
        let cursor = row.previousElementSibling;
        let safety = 0;

        while (cursor && safety++ < 50000) {
            const cDepth = parseInt(cursor.getAttribute('data-depth')) || 0;
            const cType = cursor.getAttribute('data-type');

            if (cType === 'Open' && cDepth < depth) {
                if (cursor.classList.contains('folded')) {
                    setFold(cursor, false);
                }
                depth = cDepth;
                if (depth <= 0) break;
            }

            cursor = cursor.previousElementSibling;
        }

        row.classList.remove('hidden');
    }

    function prefersReducedMotion() {
        return window.matchMedia
            && window.matchMedia('(prefers-reduced-motion: reduce)').matches;
    }

    function canUseNativeSmoothScroll() {
        return !prefersReducedMotion()
            && document.documentElement
            && 'scrollBehavior' in document.documentElement.style;
    }

    function getElementDistance(el) {
        if (!el) return 0;

        const rect = el.getBoundingClientRect();
        const elementCenter = rect.top + rect.height / 2;
        const viewportCenter = window.innerHeight / 2;

        return Math.abs(elementCenter - viewportCenter);
    }

    function getHighlightDuration(el) {
        if (!el) return baseHighlightMs;

        const distance = getElementDistance(el);
        const extra = Math.min(maxExtraHighlightMs, Math.floor(distance / pixelsPerExtraMs));

        return baseHighlightMs + extra;
    }

    function getScrollDuration(el) {
        const distance = getElementDistance(el);
        const extra = Math.min(maxScrollMs - minScrollMs, Math.floor(distance / pixelsPerScrollMs));

        return minScrollMs + extra;
    }

    function getTargetScrollY(el, block) {
        const rect = el.getBoundingClientRect();
        const currentY = window.pageYOffset || document.documentElement.scrollTop || 0;
        let targetY = currentY + rect.top;

        if (block === 'center')
            targetY -= (window.innerHeight - rect.height) / 2;

        const maxY = Math.max(0, document.documentElement.scrollHeight - window.innerHeight);
        return Math.max(0, Math.min(maxY, targetY));
    }

    function easeInOutCubic(t) {
        return t < 0.5
            ? 4 * t * t * t
            : 1 - Math.pow(-2 * t + 2, 3) / 2;
    }

    function smoothScrollFallback(el, block) {
        const startY = window.pageYOffset || document.documentElement.scrollTop || 0;
        const targetY = getTargetScrollY(el, block);
        const deltaY = targetY - startY;

        if (Math.abs(deltaY) < 1) {
            window.scrollTo(0, targetY);
            return;
        }

        const durationMs = getScrollDuration(el);
        const startTime = performance.now();

        function tick(now) {
            const progress = Math.min(1, (now - startTime) / durationMs);
            window.scrollTo(0, startY + deltaY * easeInOutCubic(progress));

            if (progress < 1)
                requestAnimationFrame(tick);
        }

        requestAnimationFrame(tick);
    }

    function scrollToElement(el, block) {
        if (prefersReducedMotion()) {
            el.scrollIntoView({ behavior: 'auto', block: block });
            return false;
        }

        if (canUseNativeSmoothScroll()) {
            el.scrollIntoView({ behavior: 'smooth', block: block });
            return true;
        }

        smoothScrollFallback(el, block);
        return true;
    }

    function flash(el, cls, durationMs) {
        if (!el) return;

        const prev = highlightTimers.get(el);
        if (prev) clearTimeout(prev);

        if (el.classList && el.classList.contains('log-row')) {
            ensureVisible(el);
        }

        el.classList.remove(cls);
        el.style.setProperty('--highlight-duration', durationMs + 'ms');
        void el.offsetWidth;
        el.classList.add(cls);

        highlightTimers.set(el, setTimeout(() => {
            el.classList.remove(cls);
            el.style.removeProperty('--highlight-duration');
            highlightTimers.delete(el);
        }, durationMs));
    }

    function resolveHighlightTarget(el) {
        if (!el || !el.classList)
            return el;

        if (el.classList.contains('log-row') || el.classList.contains('header'))
            return el;

        const row = el.closest ? el.closest('.log-row') : null;
        return row || el;
    }

    document.addEventListener('click', function(e) {
        const link = e.target.closest('a.interaction');
        if (link) {
            e.preventDefault();

            const href = link.getAttribute('href');
            if (href && href.startsWith('#')) {
                const targetId = href.substring(1);
                const targetElement = document.getElementById(targetId);

                if (targetElement) {
                    const highlightTarget = resolveHighlightTarget(targetElement);
                    ensureVisible(highlightTarget);
                    const useSmooth = scrollToElement(highlightTarget, 'center');
                    const durationMs = useSmooth ? getHighlightDuration(highlightTarget) : baseHighlightMs;
                    flash(highlightTarget, 'highlight', durationMs);
                }
                else {
                    const parts = targetId.split('_');
                    let boxId = null;

                    if (parts.length >= 3 && parts[0] === 'log') {
                        boxId = 'b' + parts[1];
                    } else if (targetId.startsWith('b')) {
                        boxId = targetId;
                    }

                    if (boxId) {
                        const boxElem = document.getElementById(boxId);
                        if (boxElem) {
                            const header = boxElem.querySelector('.header');
                            if (header) {
                                const useSmooth = scrollToElement(header, 'center');
                                const durationMs = useSmooth ? getHighlightDuration(header) : baseHighlightMs;
                                flash(header, 'highlight', durationMs);
                            } else {
                                scrollToElement(boxElem, 'start');
                            }
                        } else {
                            console.warn('Target Box not found: ' + boxId);
                        }
                    }
                }
            }
            return;
        }

        let row = e.target.closest('.log-row');
        if (!row) return;

        let type = row.getAttribute('data-type');
        const startDepth = parseInt(row.getAttribute('data-depth')) || 0;

        if (type === 'Close') {
            let prev = row.previousElementSibling;
            while (prev) {
                const prevDepth = parseInt(prev.getAttribute('data-depth')) || 0;
                const prevType = prev.getAttribute('data-type');

                if (prevDepth === startDepth && prevType === 'Open') {
                    row = prev;
                    type = 'Open';
                    break;
                }
                if (prevDepth < startDepth) break;

                prev = prev.previousElementSibling;
            }
        }

        if (type !== 'Open') return;

        const nextSibling = row.nextElementSibling;
        if (!nextSibling) return;

        const nextDepth = parseInt(nextSibling.getAttribute('data-depth')) || 0;
        if (nextDepth <= startDepth) return;

        const isFolding = !row.classList.contains('folded');
        setFold(row, isFolding);
    });
</script></body></html>
";

        private static void AppendDocumentStart(StringBuilder sb) => sb.Append(DocumentStart);
        private static void AppendDocumentEnd(StringBuilder sb) => sb.Append(DocumentEnd);
    }
}
