/// <reference lib="esnext.disposable" preserve="true" />

import type {
    BlackboxOptions, ExportArtifact, ExportOptions, OfOptions, Subject,
} from "./types.js";
import type { ExportNode, ExportSnapshot } from "./export-model.js";
import { Box, logSnapshot } from "./box.js";
import { BlackboxContext } from "./context.js";
import type { BlackboxHandle } from "./handles.js";
import { supportDispose } from "./lifetime.js";
import { renderExport } from "./render.js";
import { activeSessions, carrier, defaultSession, exportArtifact } from "./runtime.js";
import { defaults, resolveSettings, type Settings } from "./settings.js";

/** A detached recording lifetime. Use runSession for automatic cleanup. */
export class BlackboxSession {
    // Front
    readonly sessionKey = createSessionKey();
    readonly context: BlackboxContext;
    get isDisposed(): boolean { return !this._active; }
    get isFrozen(): boolean { return this._crash !== undefined; }
    get settings(): Settings { return this._settings; }
    get currentSequence(): number { return this._sequence; }

    // Internal
    private _settings: Settings;
    private _active = true;
    private _crash: { message: string; stack: string } | undefined;
    private readonly _subjects = new WeakMap<Subject, Box>();
    private readonly _strongBoxes = new Set<Box>();
    private readonly _warnings = new Set<string>();
    private _boxId = 0;
    private _contextId = 0;
    private _interactionId = 0;
    private _sequence = -1;
    private _exportOrdinal = 0;


    // Content --------------------
    // Construction and configuration
    constructor(options: BlackboxOptions = {}) {
        const inherited = carrier.get()?.session.settings ?? defaultSession?.settings ?? defaults;
        this._settings = resolveSettings(inherited, options);
        this.context = new BlackboxContext(this);
        activeSessions.add(this);
    }

    configure(options: BlackboxOptions): void {
        if (!this._active) { this.checkWrite(); return; }
        this._settings = resolveSettings(this._settings, options);
    }

    of(subject: Subject, options: OfOptions = {}): BlackboxHandle { return this.context.of(subject, options); }

    run<T>(callback: (context: BlackboxContext) => T): T { return this.context.run(callback); }

    // Recording state
    /** @internal */
    get canWrite(): boolean { return this._active && !this._crash && this._settings.useBlackbox; }
    /** @internal */
    checkWrite(): boolean {
        if (!this._active) this.warnOnce("disposed", "[Blackbox] Session has been disposed; recording rejected.");
        return this.canWrite;
    }
    /** @internal */
    warnOnce(reason: string, message: string): void {
        if (this._warnings.has(reason)) return;
        this._warnings.add(reason);
        this._settings.warningLogger?.(message);
    }
    /** @internal */
    nextBoxId(): number { return this._boxId++; }
    /** @internal */
    nextContextId(): number { return this._contextId++; }
    /** @internal */
    nextSequence(): number { return ++this._sequence; }
    /** @internal */
    nextInteractionId(): number { return this._interactionId++; }

    /** @internal */
    getBox(subject: Subject): Box | undefined {
        if (!this.checkWrite()) return undefined;
        if (!isSubject(subject)) throw new TypeError("subject must be an object or function.");
        let box = this._subjects.get(subject);
        if (!box) {
            box = new Box(subject, this);
            this._subjects.set(subject, box);
            if (this._settings.strongReference) this._strongBoxes.add(box);
        }
        return box;
    }

    /** @internal */
    freeze(message: string, stack: string): boolean {
        if (!this.canWrite) return false;
        this._crash = { message, stack };
        return true;
    }

    // Export
    /** @internal */
    snapshot(box: Box, options: ExportOptions): ExportSnapshot {
        const maxSequence = this._sequence;
        const recursionDepth = options.recursionDepth ?? this._settings.defaultRecursionDepth;
        if (!Number.isSafeInteger(recursionDepth)) throw new RangeError("recursionDepth must be a safe integer.");
        const full = options.fullExport ?? this._settings.fullExport;
        const nodes: ExportNode[] = [];
        const names: Record<number, string> = Object.create(null) as Record<number, string>;
        const visited = new Set<Box>();
        const pending: { box: Box; parentId: number | null; depth: number }[] = [{ box, parentId: null, depth: 0 }];
        while (pending.length > 0) {
            const item = pending.pop()!;
            if (visited.has(item.box)) continue;
            visited.add(item.box);
            names[item.box.id] ??= item.box.displayName;
            const logs = item.box.logs(maxSequence);
            nodes.push({
                id: item.box.id, name: names[item.box.id], parentId: item.parentId,
                depth: item.depth, logs: logs.map(log => logSnapshot(log, names)),
            });
            if (recursionDepth >= 0 && item.depth >= recursionDepth) continue;
            const peers: Box[] = [];
            for (const log of logs) {
                if (log.exertedBy) peers.push(log.exertedBy);
                if (full && log.exertingTo) peers.push(log.exertingTo);
            }
            // Reverse pushes so peers are visited in the order they first appeared.
            for (let index = peers.length - 1; index >= 0; index--)
                pending.push({ box: peers[index], parentId: item.box.id, depth: item.depth + 1 });
        }
        return {
            sessionKey: this.sessionKey, rootId: box.id, maxSequence, nodes, names,
            ...(this._crash ? { crash: { ...this._crash } } : {}),
        };
    }

    /** @internal */
    export(box: Box, options: ExportOptions = {}, crash = false): ExportArtifact {
        const settings = this._settings;
        const format = options.format ?? settings.exportFormat;
        if (format !== "html" && format !== "text") throw new TypeError("format must be html or text.");
        const snapshot = this.snapshot(box, options);
        const artifact: ExportArtifact = {
            content: renderExport(snapshot, format),
            fileName: this.exportFileName(box, format, crash),
            mimeType: format === "html" ? "text/html;charset=utf-8" : "text/plain;charset=utf-8",
        };
        if (settings.onExport) settings.onExport(artifact);
        else exportArtifact?.(artifact, settings, options);
        return artifact;
    }

    private exportFileName(box: Box, format: "html" | "text", crash: boolean): string {
        const prefix = crash ? "[CRASH] " : "";
        const safeName = box.typeName.replace(/[<>:"/\\|?*\u0000-\u001f]/g, "_").slice(0, 80);
        const session = this.sessionKey.replaceAll("-", "").slice(0, 12);
        const extension = format === "html" ? "html" : "txt";
        return `${prefix}Blackbox ${safeName} (${box.id})-${session}-${++this._exportOrdinal}.${extension}`;
    }

    // Disposal
    dispose(): void {
        if (!this._active) return;
        this._active = false;
        activeSessions.delete(this);
    }
}

export interface BlackboxSession extends Disposable { }
supportDispose(BlackboxSession);

function createSessionKey(): string {
    const bytes = new Uint8Array(16);
    globalThis.crypto.getRandomValues(bytes);
    bytes[6] = (bytes[6] & 15) | 64;
    bytes[8] = (bytes[8] & 63) | 128;
    const hex = Array.from(bytes, byte => byte.toString(16).padStart(2, "0")).join("");
    return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

function isSubject(subject: unknown): subject is Subject {
    return subject !== null && (typeof subject === "object" || typeof subject === "function");
}
