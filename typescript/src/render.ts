import type { ExportLog, ExportNode, ExportSnapshot, ExportTag } from "./export-model.js";
import { TargetTypes, type ExportFormat } from "./types.js";
import { htmlDocumentEnd, htmlDocumentStart } from "./html-template.js";

interface DisplayLog extends ExportLog {
    depth: number;
    ancestors: readonly number[];
}

interface DisplayNode extends Omit<ExportNode, "logs"> {
    logs: readonly DisplayLog[];
}

type Names = Readonly<Record<number, string>>;

// Message formatting
export function renderMessage(log: ExportLog, names: Names, includeIds = true): string {
    const used = new Set<number>();
    let message = log.message.replace(/%(\d+)/g, (placeholder, digits: string) => {
        const index = Number(digits);
        const tag = Number.isSafeInteger(index) ? log.tags[index] : undefined;
        if (!tag) return placeholder;
        used.add(index);
        return renderTag(tag, names, includeIds);
    });
    log.tags.forEach((tag, index) => {
        if (used.has(index) || (tag.targetId !== null && tag.id < 0)) return;
        if (message.length > 0) message += " ";
        message += `(tag %${index}: ${renderTag(tag, names, includeIds)})`;
    });
    if (log.taggedBy === undefined) return message;

    let tagged = "tagged";
    if ((log.tagTargetTypes & TargetTypes.Name) !== 0)
        tagged += ` by '${objectName(log.taggedBy, names)}'`;
    else if (includeIds && (log.tagTargetTypes & TargetTypes.InteractionId) !== 0 && log.interactionId >= 0)
        tagged += ` [${log.interactionId}]`;
    if ((log.tagTargetTypes & TargetTypes.Message) !== 0) tagged += `: ${message}`;
    return tagged;
}

function renderTag(tag: ExportTag, names: Names, includeIds: boolean): string {
    if (tag.targetId === null) return "null";
    const name = names[tag.targetId] ?? tag.targetName;
    const suffix = includeIds && tag.id >= 0 ? ` [${tag.id}]` : "";
    return `'${name}${suffix}'`;
}

function objectName(id: number, names: Names): string {
    return names[id] ?? `Object #${id}`;
}

function methodLabel(log: ExportLog): string {
    if (!log.methodName) return "";
    switch (log.scopeType) {
        case "open": return `<${log.methodName}>`;
        case "close": return `</${log.methodName}>`;
        case "step": return `<${log.methodName} />`;
        default: return `[${log.methodName}]`;
    }
}

function arrow(right: boolean, interactionId: number): string {
    if (interactionId < 0) return right ? "->" : "<-";
    return right ? `-[${interactionId}]->` : `<-[${interactionId}]-`;
}

// Snapshot-only scope conversion
function scopeAncestors(scopeId: number | null, parents: ReadonlyMap<number, number | null>): number[] {
    const result: number[] = [];
    const visited = new Set<number>();
    while (scopeId !== null && !visited.has(scopeId)) {
        visited.add(scopeId);
        result.push(scopeId);
        scopeId = parents.get(scopeId) ?? null;
    }
    return result.reverse();
}

function prepareLogs(logs: readonly ExportLog[], maxSequence: number): DisplayLog[] {
    const source = logs
        .filter(log => maxSequence < 0 || log.sequence <= maxSequence)
        .map(log => ({ ...log, tags: [...log.tags] }))
        .sort((a, b) => a.sequence - b.sequence);
    const parents = new Map<number, number | null>();
    for (const log of source)
        if (log.scopeType === "open" || log.scopeType === "close" || log.scopeType === "step")
            parents.set(log.scopeId, log.parentScopeId);

    const sourceAncestors = source.map(log => scopeAncestors(log.parentScopeId, parents));
    const skipped = new Set<number>();
    const result: DisplayLog[] = [];
    for (let index = 0; index < source.length; index++) {
        if (skipped.has(index)) continue;
        const log = source[index]!;
        if (log.scopeType === "open") {
            for (let next = index + 1; next < source.length; next++) {
                const candidate = source[next]!;
                // Unrelated interleaved flows do not make a scope nonempty.
                if (candidate.contextId !== log.contextId && !sourceAncestors[next]!.includes(log.scopeId))
                    continue;
                if (candidate.scopeType === "close" && candidate.scopeId === log.scopeId) {
                    log.scopeType = "step";
                    log.message = !log.message ? candidate.message
                        : !candidate.message ? log.message : `${log.message} > ${candidate.message}`;
                    skipped.add(next);
                }
                break;
            }
        }
        const ancestors = sourceAncestors[index]!;
        result.push({ ...log, depth: ancestors.length, ancestors });
    }
    return result;
}

// Document rendering
export function renderExport(snapshot: ExportSnapshot, format: ExportFormat): string {
    const nodes: DisplayNode[] = snapshot.nodes.map(node => ({
        ...node,
        logs: prepareLogs(node.logs, snapshot.maxSequence),
    }));
    return format === "text" ? renderText(snapshot, nodes) : renderHtml(snapshot, nodes);
}

function renderText(snapshot: ExportSnapshot, nodes: readonly DisplayNode[]): string {
    const included = new Set(nodes.map(node => node.id));
    const lines: string[] = [];
    if (snapshot.crash)
        lines.push(`[Error] ${snapshot.crash.message}`, "[STACK TRACE]", snapshot.crash.stack, "");
    for (const node of nodes) {
        const parent = node.parentId === null ? "" : ` | From = ${objectName(node.parentId, snapshot.names)}`;
        lines.push(`========= ${node.name} (Depth = ${node.depth}${parent}) =========`, "-".repeat(40));
        for (const log of node.logs) {
            const method = methodLabel(log);
            let line = `[${log.sequence}] [C${log.contextId}] [${log.time}] ${"|   ".repeat(log.depth)}`
                + `${method ? `${method} ` : ""}${renderMessage(log, snapshot.names)}`;
            if (log.exertedBy !== undefined || log.exertingTo !== undefined) {
                const left = log.exertedBy === undefined ? ""
                    : `${objectName(log.exertedBy, snapshot.names)} ${arrow(true, log.interactionId)} `;
                const right = log.exertingTo === undefined ? ""
                    : ` ${arrow(true, log.interactionId)} ${objectName(log.exertingTo, snapshot.names)}`;
                line += ` (${left}#${node.id}: this${right})`;
                const peers = [log.exertedBy, log.exertingTo].filter((id): id is number => id !== undefined);
                const missing = [...new Set(peers.filter(id => !included.has(id)))];
                if (missing.length === 1 && peers.length === 1) line += " (not included)";
                else for (const id of missing) line += ` (#${id} not included)`;
            }
            lines.push(line);
        }
        lines.push("", "");
    }
    return lines.join("\n");
}

function htmlEncode(value: string): string {
    return value.replace(/[&<>"']/g, character => ({
        "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;",
    })[character]!);
}

function interactionKey(ownerId: number, interactionId: number): string {
    return `log_${ownerId}_${interactionId}`;
}

function renderHtml(snapshot: ExportSnapshot, nodes: readonly DisplayNode[]): string {
    const anchors = new Set<string>();
    for (const node of nodes) {
        anchors.add(`b${node.id}`);
        for (const log of node.logs) {
            if (log.interactionId >= 0) anchors.add(interactionKey(node.id, log.interactionId));
            for (const tag of log.tags)
                if (tag.id >= 0 && tag.targetId !== null)
                    anchors.add(interactionKey(node.id, tag.id));
        }
    }

    const parts = [htmlDocumentStart,
        `<header class="report-header"><h1>Blackbox Log Report</h1><p>Session <code>${htmlEncode(snapshot.sessionKey)}`
        + `</code> · through sequence ${snapshot.maxSequence}</p>`
        + '<nav aria-label="Scope controls"><button type="button" data-action="expand">Expand all</button>'
        + '<button type="button" data-action="collapse">Collapse all</button></nav></header>',
    ];
    if (snapshot.crash) {
        const crash = `[Error] ${snapshot.crash.message}\n[STACK TRACE]\n${snapshot.crash.stack}`;
        parts.push(`<section class="crash" aria-label="Crash details"><h2>Crash</h2><pre>${htmlEncode(crash)}</pre></section>`);
    }

    for (const node of nodes) {
        const parent = node.parentId === null ? "" : ` <span class="parent">&larr; From <a href="#b${node.parentId}">`
            + `${htmlEncode(objectName(node.parentId, snapshot.names))}</a></span>`;
        parts.push(`<section class="box" id="b${node.id}"><h2 class="header"><span class="id-badge">#${node.id}</span> `
            + `${htmlEncode(node.name)}${parent}</h2><div class="log-container">`);
        const emittedAnchors = new Set<string>();
        for (const log of node.logs) {
            const scopeKey = `${node.id}:${log.scopeId}`;
            const memberships = log.ancestors.map(id => `${node.id}:${id}`);
            if (log.scopeType === "close") memberships.push(scopeKey);
            const scopeAttribute = log.scopeType === "open" ? ` data-scope-key="${scopeKey}"` : "";
            parts.push(`<div class="log-row" id="row_${node.id}_${log.sequence}" data-context="${log.contextId}"`
                + ` data-sequence="${log.sequence}" data-depth="${log.depth}" data-type="${log.scopeType}"`
                + ` data-scope-ancestors="${memberships.join(" ")}"${scopeAttribute}>`);
            parts.push(`<div class="time">#${log.sequence} C${log.contextId} ${htmlEncode(log.time)}</div>`
                + `<div class="content" style="padding-left:${log.depth * 20}px">`);
            const rowAnchors = [
                ...(log.interactionId < 0 ? [] : [interactionKey(node.id, log.interactionId)]),
                ...log.tags.filter(tag => tag.id >= 0 && tag.targetId !== null)
                    .map(tag => interactionKey(node.id, tag.id)),
            ];
            for (const id of rowAnchors) {
                if (emittedAnchors.has(id)) continue;
                emittedAnchors.add(id);
                parts.push(`<span class="anchor" id="${id}"></span>`);
            }
            if (log.scopeType === "open")
                parts.push('<button type="button" class="scope-toggle" aria-expanded="true" aria-label="Collapse scope">▾</button>');
            const method = methodLabel(log);
            if (method) parts.push(`<span class="method">${htmlEncode(method)}</span> `);
            parts.push(`<span class="message">${htmlEncode(renderMessage(log, snapshot.names, false))}</span>`);
            if (log.exertedBy !== undefined)
                parts.push(renderInteraction(log.exertedBy, false, log.interactionId, snapshot.names, anchors));
            if (log.exertingTo !== undefined)
                parts.push(renderInteraction(log.exertingTo, true, log.interactionId, snapshot.names, anchors));
            if (log.taggedBy !== undefined && log.interactionId >= 0)
                parts.push(renderTagInteraction(log.taggedBy, log.interactionId, snapshot.names, anchors));
            for (const tag of log.tags)
                if (tag.targetId !== null && tag.targetId !== node.id && tag.id >= 0)
                    parts.push(renderTagInteraction(tag.targetId, tag.id, snapshot.names, anchors));
            parts.push("</div></div>");
        }
        parts.push("</div></section>");
    }
    parts.push(htmlDocumentEnd);
    // Newlines between inline fragments would become visible inside pre-wrapped messages.
    return parts.join("");
}

function renderInteraction(
    peerId: number, right: boolean, id: number, names: Names, anchors: ReadonlySet<string>,
): string {
    const target = id < 0 ? `b${peerId}` : interactionKey(peerId, id);
    const label = htmlEncode(`${arrow(right, id)} #${peerId}: ${objectName(peerId, names)}`);
    return renderLink(target, label, "interaction",
        `Interaction #${id}`, "Interaction target not included in this export", anchors);
}

function renderTagInteraction(peerId: number, id: number, names: Names, anchors: ReadonlySet<string>): string {
    return renderLink(interactionKey(peerId, id), `[${id}]`, "interaction tag-interaction",
        `Tag #${id}: ${objectName(peerId, names)}`,
        `Tag target not included in this export: ${objectName(peerId, names)}`, anchors);
}

function renderLink(
    target: string, label: string, className: string, title: string, missingTitle: string, anchors: ReadonlySet<string>,
): string {
    if (anchors.has(target))
        return ` <a href="#${target}" class="${className}" title="${htmlEncode(title)}">${label}</a>`;
    return ` <span class="${className} disabled" title="${htmlEncode(missingTitle)}">${label}</span>`;
}
