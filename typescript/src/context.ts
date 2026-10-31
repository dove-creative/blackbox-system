import type { BlackboxOptions, OfOptions, RecordOptions, Subject } from "./types.js";
import type { Box, ScopeFrame } from "./box.js";
import { BlackboxHandle, ScopeHandle, TagHandle } from "./handles.js";
import { finish } from "./lifetime.js";
import { carrier } from "./runtime.js";
import { BlackboxSession } from "./session.js";
import { resolveSettings } from "./settings.js";

/** A recording branch. Pass it explicitly in browser asynchronous work. */
export class BlackboxContext {
    // Front
    readonly id: number;
    readonly session: BlackboxSession;
    /** @internal */
    readonly scopeParent: BlackboxContext | undefined;
    get sessionKey(): string { return this.session.sessionKey; }

    // Internal
    private readonly _inherited = new Map<Box, ScopeFrame>();
    private readonly _scopes = new Map<Box, ScopeFrame[]>();


    // Content --------------------
    // Construction and branches
    constructor(session: BlackboxSession);
    /** @internal */
    constructor(session: BlackboxSession, parent: BlackboxContext | undefined, scopeBranch?: boolean);
    constructor(session: BlackboxSession, parent?: BlackboxContext, scopeBranch = false) {
        this.session = session;
        this.id = session.nextContextId();
        this.scopeParent = scopeBranch ? parent : undefined;
        if (parent) {
            for (const [box, frame] of parent._inherited) this._inherited.set(box, frame);
            for (const [box] of parent._scopes) {
                const frame = parent.head(box);
                if (frame) this._inherited.set(box, frame);
            }
        }
    }

    of(subject: Subject, options: OfOptions = {}): BlackboxHandle {
        const box = this.session.getBox(subject);
        box?.setLabel(options.label);
        return new BlackboxHandle(box, this);
    }
    configure(options: BlackboxOptions): void { this.session.configure(options); }
    fork(): BlackboxContext { return new BlackboxContext(this.session, this); }
    createSession(options: BlackboxOptions = {}): BlackboxSession {
        return new BlackboxSession(resolveSettings(this.session.settings, options));
    }
    runSession<T>(callback: (context: BlackboxContext) => T, options: BlackboxOptions = {}): T {
        const session = this.createSession(options);
        return finish(() => session.run(callback), () => session.dispose());
    }
    run<T>(callback: (context: BlackboxContext) => T): T {
        const context = this.fork();
        return carrier.run(context, () => callback(context));
    }

    // Scope stack
    /** @internal */
    head(box: Box): ScopeFrame | undefined {
        const scopes = this._scopes.get(box);
        if (scopes) {
            while (scopes.length > 0 && scopes[scopes.length - 1].closed) scopes.pop();
            if (scopes.length > 0) return scopes[scopes.length - 1];
            this._scopes.delete(box);
        }
        let inherited = this._inherited.get(box);
        while (inherited?.closed) inherited = inherited.parent;
        return inherited;
    }

    /** @internal */
    open(box: Box, message: string, options: RecordOptions): ScopeHandle {
        if (!this.session.checkWrite()) return new ScopeHandle();
        const parent = this.head(box);
        const frame: ScopeFrame = {
            box, id: box.nextScopeId(), parent, context: this,
            methodName: options.methodName ?? "", closed: false,
        };
        const log = box.append(this, message, options,
            { scopeType: "open", scopeId: frame.id, parentScopeId: parent?.id ?? null });
        if (!log) return new ScopeHandle();
        let scopes = this._scopes.get(box);
        if (!scopes) {
            scopes = [];
            this._scopes.set(box, scopes);
        }
        scopes.push(frame);
        return new ScopeHandle(frame, new TagHandle(box, log.index, message));
    }

    /** @internal */
    close(frame: ScopeFrame): void {
        if (frame.closed) return;
        const scopes = this._scopes.get(frame.box) ?? [];
        const position = scopes.indexOf(frame);
        if (position < 0) {
            frame.closed = true;
            return;
        }
        const canWrite = this.session.checkWrite();
        let autoClosed = false;
        for (let index = scopes.length - 1; index >= position; index--) {
            const closing = scopes[index];
            if (closing.closed) continue;
            closing.closed = true;
            if (canWrite) frame.box.append(this,
                index === position ? "" : "Closed automatically because an outer scope closed first.",
                { methodName: closing.methodName },
                { scopeType: "close", scopeId: closing.id, parentScopeId: closing.parent?.id ?? null });
            if (index !== position) autoClosed = true;
        }
        scopes.splice(position);
        if (scopes.length === 0) this._scopes.delete(frame.box);
        if (autoClosed && canWrite)
            this.session.warnOnce("outerScope", "[Blackbox] Newer scopes found. They will be closed automatically.");
    }
}
