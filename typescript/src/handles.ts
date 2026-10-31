/// <reference lib="esnext.disposable" preserve="true" />

import { TargetTypes } from "./types.js";
import type {
    BlackboxOptions, ErrorOptions, ExportArtifact, ExportOptions, Message,
    OfOptions, RecordOptions, Subject,
} from "./types.js";
import { logSnapshot, type Box, type ScopeFrame, type Tag } from "./box.js";
import { BlackboxContext } from "./context.js";
import { finish, supportDispose } from "./lifetime.js";
import { renderMessage } from "./render.js";
import { activeSessions, carrier, currentContext, defaultSession, setDefaultSession } from "./runtime.js";
import { BlackboxSession } from "./session.js";

// Tag targets, optionally followed by one TargetTypes value.
type TagArguments = (Subject | null)[] | [...targets: (Subject | null)[], types: TargetTypes];

const emptyKey = "00000000-0000-0000-0000-000000000000";

/** The ordinary object-oriented entry point, bound to the object's original session. */
export class BlackboxHandle {
    // Front
    get owner(): Subject | undefined { return this._box?.owner; }
    get ownerString(): string { return this._box?.ownerString ?? "[BlackboxHandle] Invalid Handle"; }
    get id(): number { return this._box?.id ?? -1; }
    get isValid(): boolean { return this._box?.session.canWrite ?? false; }
    get sessionKey(): string { return this.isValid ? this._box!.session.sessionKey : emptyKey; }

    static get useBlackbox(): boolean { return currentContext().session.settings.useBlackbox; }
    static set useBlackbox(value: boolean) { this.configure({ useBlackbox: value }); }
    static get maxLogCount(): number { return currentContext().session.settings.maxLogCount; }
    static set maxLogCount(value: number) { this.configure({ maxLogCount: value }); }
    static get strongReference(): boolean { return currentContext().session.settings.strongReference; }
    static set strongReference(value: boolean) { this.configure({ strongReference: value }); }
    static get defaultRecursionDepth(): number { return currentContext().session.settings.defaultRecursionDepth; }
    static set defaultRecursionDepth(value: number) { this.configure({ defaultRecursionDepth: value }); }
    static get exportFormat(): "html" | "text" { return currentContext().session.settings.exportFormat; }
    static set exportFormat(value: "html" | "text") { this.configure({ exportFormat: value }); }
    static get fullExport(): boolean { return currentContext().session.settings.fullExport; }
    static set fullExport(value: boolean) { this.configure({ fullExport: value }); }
    static get openLog(): boolean { return currentContext().session.settings.openLog; }
    static set openLog(value: boolean) { this.configure({ openLog: value }); }
    static get tagTargetTypes(): TargetTypes { return currentContext().session.settings.tagTargetTypes; }
    static set tagTargetTypes(value: TargetTypes) { this.configure({ tagTargetTypes: value }); }
    static get logDirectory(): string | undefined { return currentContext().session.settings.logDirectory; }
    static set logDirectory(value: string | undefined) { this.configure({ logDirectory: value }); }
    static get normalLogger(): ((message: string) => void) | undefined { return currentContext().session.settings.normalLogger; }
    static set normalLogger(value: ((message: string) => void) | undefined) { this.configure({ normalLogger: value }); }
    static get warningLogger(): ((message: string) => void) | undefined { return currentContext().session.settings.warningLogger; }
    static set warningLogger(value: ((message: string) => void) | undefined) { this.configure({ warningLogger: value }); }

    static get currentSessionKey(): string { return currentContext().session.sessionKey; }
    static get defaultSessionKey(): string { return defaultSession.sessionKey; }
    static get activeSessionCount(): number { return activeSessions.size; }

    // Internal
    private readonly _box: Box | undefined;
    private readonly _context: BlackboxContext | undefined;
    private readonly _ambientContext: boolean;


    // Content --------------------
    // Construction and session lifetimes
    constructor();
    /** @internal */
    constructor(box: Box | undefined, context: BlackboxContext | undefined, ambientContext?: boolean);
    constructor(box?: Box, context?: BlackboxContext, ambientContext = false) {
        this._box = box;
        this._context = context;
        this._ambientContext = ambientContext;
    }

    static configure(options: BlackboxOptions): void { currentContext().session.configure(options); }
    static of(subject: Subject, options: OfOptions = {}): BlackboxHandle {
        const context = currentContext();
        const box = context.session.getBox(subject);
        box?.setLabel(options.label);
        return new BlackboxHandle(box, context, true);
    }
    static createSession(options: BlackboxOptions = {}): BlackboxSession { return new BlackboxSession(options); }
    static runSession<T>(callback: (context: BlackboxContext) => T, options: BlackboxOptions = {}): T {
        const session = this.createSession(options);
        return finish(() => session.run(callback), () => session.dispose());
    }
    static getActiveSessionKeys(): string[] { return [...activeSessions].map(session => session.sessionKey); }
    static disposeActiveSessions(): void {
        for (const session of [...activeSessions].reverse()) session.dispose();
    }
    static restartDefaultSession(): void {
        const old = defaultSession;
        setDefaultSession(new BlackboxSession(old.settings));
        old.dispose();
    }

    private context(): BlackboxContext {
        const bound = this._context ?? currentContext();
        const ambient = carrier.get();
        return this._ambientContext && ambient?.session === bound.session ? ambient : bound;
    }

    // Object lifetime records
    construct(message: Message = "", options: RecordOptions = {}): ScopeHandle {
        if (!this._box?.session.checkWrite()) return new ScopeHandle();
        const text = messageValue(message);
        return this.scope(`[Ctor: ${this._box.displayName}]${text.trim() ? ` ${text}` : ""}`, options);
    }
    dispose(message: Message = "", options: RecordOptions = {}): BlackboxHandle {
        if (!this._box?.session.checkWrite()) return new BlackboxHandle(undefined, this._context);
        const text = messageValue(message);
        this.write(`[Disposed: ${this._box.displayName}]${text.trim() ? ` ${text}` : ""}`,
            { methodName: options.methodName ?? "dispose" });
        return this;
    }
    when(condition: boolean | (() => boolean)): BlackboxHandle {
        if (typeof condition !== "boolean" && typeof condition !== "function")
            throw new TypeError("condition must be a boolean or predicate.");
        const enabled = typeof condition === "function" ? condition() : condition;
        return enabled ? this : new BlackboxHandle(undefined, this._context, this._ambientContext);
    }

    // Recording
    write(message: Message, options: RecordOptions = {}): TagHandle {
        const box = this._box;
        if (!box?.session.checkWrite()) return new TagHandle(undefined, -1, fallbackMessage(message));
        const text = messageValue(message);
        const log = box.append(this.context(), text, options);
        return new TagHandle(box, log?.index ?? -1, text);
    }
    scope(message: Message, options: RecordOptions = {}): ScopeHandle {
        if (!this._box?.session.checkWrite()) return new ScopeHandle();
        return this.context().open(this._box, messageValue(message), options);
    }
    runScope<T>(message: Message, callback: (context: BlackboxContext) => T, options: RecordOptions = {}): T {
        // The branch belongs to the handle's session, so Node ambient of() calls in the callback follow it.
        const parent = this.context();
        const context = new BlackboxContext(parent.session, parent, true);
        const handle = new BlackboxHandle(this._box, context);
        return carrier.run(context, () => {
            const scope = handle.scope(message, options);
            return finish(() => callback(context), () => scope.dispose());
        });
    }

    private interaction(other: Subject, message: string, options: RecordOptions): ExertHandle {
        const box = this._box;
        if (!box?.session.checkWrite()) return new ExertHandle();
        const target = box.session.getBox(other);
        if (!target) return new ExertHandle();
        const context = this.context();
        const interactionId = box.session.nextInteractionId();
        const targetLog = target.append(context, message, options,
            { interactionId, exertedBy: box, ...(box === target ? { exertingTo: box } : {}) });
        if (box !== target) box.append(context, message, options, { interactionId, exertingTo: target });
        return new ExertHandle(target, targetLog?.index ?? -1, context);
    }
    exertMessage(other: Subject, message: Message, options: RecordOptions = {}): string {
        if (!this._box?.session.checkWrite()) return fallbackMessage(message);
        const text = messageValue(message);
        this.interaction(other, text, options);
        return text;
    }
    exert(other: Subject, message: Message, options: RecordOptions = {}): ExertHandle {
        if (!this._box?.session.checkWrite()) return new ExertHandle();
        return this.interaction(other, messageValue(message), options);
    }
    runExert<T>(other: Subject, message: Message, callback: (context: BlackboxContext) => T, options: RecordOptions = {}): T {
        // Like runScope, the callback runs in a branch of the handle's session.
        const context = this.context().fork();
        const handle = new BlackboxHandle(this._box, context);
        return carrier.run(context, () => {
            const exert = handle.exert(other, message, options);
            return finish(() => callback(context), () => exert.dispose());
        });
    }

    // Errors and export
    writeError(message: unknown, options: ErrorOptions = {}): string {
        const text = String(message ?? "");
        const settings = this._box?.session.settings ?? this.context().session.settings;
        if ((options.exceptionHandling ?? settings.exceptionHandling) === "crashExport")
            return this.crashExport(message, options);
        const targets = options.targets ?? [];
        const context = targets.map((target, index) => `${target.context.trim() || "target"}: %${index}`).join(", ");
        const handle = this.write(`[Error] ${text}${context ? ` (${context})` : ""}`, options);
        if (targets.length)
            handle.with(...targets.map(target => target.target), TargetTypes.Name | TargetTypes.InteractionId);
        return text;
    }
    crashExport(message: unknown, options: ErrorOptions = {}): string {
        const text = String(message ?? "");
        const box = this._box;
        if (!box?.session.checkWrite()) return text;
        let crashMessage = text;
        for (const target of options.targets ?? []) {
            if (target.target === null) continue;
            const peer = box.session.getBox(target.target);
            if (peer) crashMessage += ` (${target.context}: ${peer.displayName})`;
        }
        const stack = message instanceof Error ? message.stack ?? "" : new Error(text).stack ?? "";
        if (!box.session.freeze(crashMessage, stack)) return text;
        box.session.settings.warningLogger?.(`[Blackbox] CRASH: ${text}`);
        box.session.export(box, options, true);
        return text;
    }
    export(options: ExportOptions = {}): ExportArtifact | undefined {
        if (!this._box) {
            this.context().session.warnOnce("invalidExport", "Cannot export because the handle is invalid.");
            return undefined;
        }
        return this._box.session.export(this._box, options);
    }
}

/** Attach related subjects immediately after recording a log. */
export class TagHandle {
    // Internal
    private readonly _box: Box | undefined;
    private readonly _index: number;
    private readonly _fallback: string;


    // Content
    constructor();
    /** @internal */
    constructor(box: Box | undefined, index: number, fallback?: string);
    constructor(box?: Box, index = -1, fallback = "") {
        this._box = box;
        this._index = index;
        this._fallback = fallback;
    }
    with(...args: TagArguments): string {
        const box = this._box;
        if (!box?.session.checkWrite()) return this._fallback;
        const original = box.read(this._index);
        if (!original) return this._fallback;
        const targets: (Subject | null | TargetTypes)[] = [...args];
        let types = TargetTypes.BasedOnSettings;
        if (typeof targets[targets.length - 1] === "number") types = targets.pop() as TargetTypes;
        if (targets.length === 0) return this.toString();
        if (targets.some(target => typeof target === "number"))
            throw new TypeError("TargetTypes must be the last with() argument.");
        if ((types & TargetTypes.BasedOnSettings) !== 0)
            types = box.session.settings.tagTargetTypes | (types & ~TargetTypes.BasedOnSettings);
        const tags: Tag[] = targets.map(target => {
            const peer = target === null ? null : box.session.getBox(target as Subject) ?? null;
            return { id: peer && peer !== box ? box.session.nextInteractionId() : -1, target: peer };
        });
        if (!box.session.canWrite || box.read(this._index) !== original) return this.toString();
        box.replace(original, { tags });
        for (const tag of tags)
            if (tag.target && tag.target !== box)
                tag.target.append(original.context, original.message, { methodName: original.methodName },
                    { tags, taggedBy: box, interactionId: tag.id, tagTargetTypes: types });
        return this.toString();
    }
    toString(): string {
        const log = this._box?.read(this._index);
        if (!log) return this._fallback;
        const names: Record<number, string> = Object.create(null) as Record<number, string>;
        return renderMessage(logSnapshot(log, names), names);
    }
}

/** Closes an execution scope exactly once. */
export class ScopeHandle {
    // Front
    get isAlive(): boolean { return !!this._frame && !this._frame.closed && !this._frame.box.session.isDisposed; }
    get isDisposed(): boolean { return !this.isAlive; }
    get scopeId(): number { return this.isAlive ? this._frame!.id : -1; }

    // Internal
    private readonly _frame: ScopeFrame | undefined;
    private readonly _tag: TagHandle;


    // Content
    constructor();
    /** @internal */
    constructor(frame: ScopeFrame | undefined, tag?: TagHandle);
    constructor(frame?: ScopeFrame, tag = new TagHandle()) {
        this._frame = frame;
        this._tag = tag;
    }
    with(...args: TagArguments): this {
        if (!this.isAlive) return this;
        this._tag.with(...args);
        return this;
    }
    dispose(): void { if (this._frame) this._frame.context.close(this._frame); }
}

/** Merges an immediately adjacent receiver scope when disposed. */
export class ExertHandle {
    // Front
    get isAlive(): boolean { return !this._disposed && this._index >= 0 && !!this._target && !this._target.session.isDisposed; }
    get isDisposed(): boolean { return !this.isAlive; }

    // Internal
    private readonly _target: Box | undefined;
    private readonly _index: number;
    private readonly _context: BlackboxContext | undefined;
    private _disposed = false;


    // Content
    constructor();
    /** @internal */
    constructor(target: Box | undefined, index: number, context?: BlackboxContext);
    constructor(target?: Box, index = -1, context?: BlackboxContext) {
        this._target = target;
        this._index = index;
        this._context = context;
    }
    dispose(): void {
        if (this._disposed) return;
        this._disposed = true;
        if (this._target && this._context) this._target.merge(this._index, this._context);
    }
}

export interface ScopeHandle extends Disposable { }
export interface ExertHandle extends Disposable { }
supportDispose(ScopeHandle);
supportDispose(ExertHandle);

function messageValue(message: Message): string {
    return typeof message === "function" ? message() : message ?? "";
}

function fallbackMessage(message: Message): string {
    return typeof message === "function" ? "" : message ?? "";
}
