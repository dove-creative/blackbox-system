import { TargetTypes } from "./types.js";
import type { RecordOptions, Subject } from "./types.js";
import type { ExportLog, ExportTag } from "./export-model.js";
import type { BlackboxContext } from "./context.js";
import type { BlackboxSession } from "./session.js";

/** @internal */
export interface Tag {
    id: number;
    target: Box | null;
}

/** @internal */
export interface Log {
    index: number;
    valid: boolean;
    sequence: number;
    time: string;
    context: BlackboxContext;
    message: string;
    methodName: string;
    scopeType: ExportLog["scopeType"];
    scopeId: number;
    parentScopeId: number | null;
    interactionId: number;
    exertingTo?: Box;
    exertedBy?: Box;
    taggedBy?: Box;
    tagTargetTypes: TargetTypes;
    tags: readonly Tag[];
}

/** @internal */
export interface ScopeFrame {
    box: Box;
    id: number;
    parent: ScopeFrame | undefined;
    context: BlackboxContext;
    methodName: string;
    closed: boolean;
}

/** @internal The recording store of one Subject within one session. */
export class Box {
    // Property
    readonly id: number;
    readonly typeName: string;
    readonly session: BlackboxSession;
    label: string | undefined;
    get owner(): Subject | undefined { return this._strongOwner ?? this._weakOwner?.deref(); }
    get ownerString(): string { return this.label ?? this.typeName; }
    get displayName(): string {
        const identity = `${this.typeName} #${this.id}`;
        return this.label === undefined ? identity : `${this.label} (${identity})`;
    }

    // Internal
    private readonly _strongOwner: Subject | undefined;
    private readonly _weakOwner: WeakRef<Subject> | undefined;
    private readonly _buffer: (Log | undefined)[];
    private _nextIndex = 0;
    private _scopeId = 0;


    // Content --------------------
    // Construction and identity
    constructor(subject: Subject, session: BlackboxSession) {
        this.session = session;
        this.id = session.nextBoxId();
        this.typeName = typeName(subject);
        if (session.settings.strongReference) this._strongOwner = subject;
        else this._weakOwner = new WeakRef(subject);
        this._buffer = new Array<Log | undefined>(session.settings.maxLogCount);
    }

    setLabel(label: string | undefined): void {
        if (!label?.trim() || !this.session.checkWrite()) return;
        if (this.label === undefined) this.label = label;
        else if (this.label !== label)
            this.session.warnOnce("label", "[Blackbox] Label was already assigned; keeping the first label.");
    }

    nextScopeId(): number { return this._scopeId++; }

    // Ring buffer
    append(context: BlackboxContext, message: string, options: RecordOptions = {}, fields: Partial<Log> = {}): Log | undefined {
        if (!this.session.checkWrite()) return undefined;
        const index = this._nextIndex++;
        const log: Log = {
            index, valid: true, sequence: this.session.nextSequence(), time: new Date().toISOString(),
            context, message, methodName: options.methodName ?? "", scopeType: "none", scopeId: -1,
            parentScopeId: context.head(this)?.id ?? null, interactionId: -1,
            tagTargetTypes: TargetTypes.Full, tags: [], ...fields,
        };
        this._buffer[index % this._buffer.length] = log;
        return log;
    }

    read(index: number): Log | undefined {
        if (index < Math.max(0, this._nextIndex - this._buffer.length) || index >= this._nextIndex) return undefined;
        const log = this._buffer[index % this._buffer.length];
        return log?.index === index && log.valid ? log : undefined;
    }

    replace(log: Log, change: Partial<Log>): void {
        if (this.read(log.index) === log) this._buffer[log.index % this._buffer.length] = { ...log, ...change };
    }

    logs(maxSequence = Number.MAX_SAFE_INTEGER): Log[] {
        const logs: Log[] = [];
        for (let index = Math.max(0, this._nextIndex - this._buffer.length); index < this._nextIndex; index++) {
            const log = this.read(index);
            if (log && log.sequence <= maxSequence) logs.push(log);
        }
        return logs;
    }

    // Interaction merge
    merge(interactionIndex: number, context: BlackboxContext): void {
        if (!this.session.checkWrite()) return;
        const from = this.read(interactionIndex);
        if (!from || from.scopeType !== "none" || from.interactionId < 0) return;
        const into = this.logs().find(log => log.index > from.index
            && (log.context === context || log.context.scopeParent === context));
        if (!into || into.scopeType !== "open") return;
        this.replace(into, {
            message: mergeMessage(into.message, from.message), interactionId: from.interactionId,
            exertedBy: from.exertedBy ?? into.exertedBy, exertingTo: from.exertingTo ?? into.exertingTo,
        });
        this.replace(from, { valid: false });
    }
}

function typeName(subject: Subject): string {
    if (typeof subject === "function") return "Function";
    const prototype: object | null = Object.getPrototypeOf(subject);
    const constructor = prototype && Object.getOwnPropertyDescriptor(prototype, "constructor")?.value;
    const name: unknown = typeof constructor === "function"
        ? Object.getOwnPropertyDescriptor(constructor, "name")?.value : undefined;
    return typeof name === "string" && name.length > 0 ? name : "Object";
}

function mergeMessage(scope: string, interaction: string): string {
    if (!interaction) return scope;
    if (!scope || scope === interaction) return interaction;
    return `${scope} (from: ${interaction})`;
}

/** @internal Copies a log into export input and records the display names it references. */
export function logSnapshot(log: Log, names: Record<number, string>): ExportLog {
    const remember = (box: Box | undefined): number | undefined => {
        if (!box) return undefined;
        names[box.id] ??= box.displayName;
        return box.id;
    };
    const tags: ExportTag[] = log.tags.map(tag => {
        const id = remember(tag.target ?? undefined);
        return { id: tag.id, targetId: id ?? null, targetName: id === undefined ? "null" : names[id] };
    });
    return {
        sequence: log.sequence, time: log.time, contextId: log.context.id, message: log.message,
        methodName: log.methodName, scopeType: log.scopeType, scopeId: log.scopeId,
        parentScopeId: log.parentScopeId, interactionId: log.interactionId,
        exertingTo: remember(log.exertingTo), exertedBy: remember(log.exertedBy),
        taggedBy: remember(log.taggedBy), tagTargetTypes: log.tagTargetTypes, tags,
    };
}
