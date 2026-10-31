import type { BlackboxContext } from "./context.js";
import type { BlackboxSession } from "./session.js";
import type { BlackboxOptions, ExportArtifact, ExportOptions } from "./types.js";

/** @internal Host integration, not exported from the package entry points. */
export interface ContextCarrier {
    get(): BlackboxContext | undefined;
    run<T>(context: BlackboxContext, callback: () => T): T;
}

/** @internal Host integration; callbacks must finish synchronously. */
export interface RuntimeHooks {
    contextCarrier?: ContextCarrier;
    exportArtifact?: (artifact: ExportArtifact, settings: Readonly<BlackboxOptions>, options: ExportOptions) => void;
}

// Host hooks
/** @internal */
export let carrier: ContextCarrier = { get: () => undefined, run: (_context, callback) => callback() };
/** @internal */
export let exportArtifact: RuntimeHooks["exportArtifact"];

/** @internal */
export function setupRuntime(hooks: RuntimeHooks): void {
    if (hooks.contextCarrier) carrier = hooks.contextCarrier;
    if (hooks.exportArtifact) exportArtifact = hooks.exportArtifact;
}

// Session registry
/** @internal Undisposed explicit sessions in start order. */
export const activeSessions = new Set<BlackboxSession>();
/** @internal Assigned when the core module finishes loading. */
export let defaultSession: BlackboxSession;

/** @internal Replaces the shared default session and keeps it out of the explicit-session registry. */
export function setDefaultSession(session: BlackboxSession): void {
    activeSessions.delete(session);
    defaultSession = session;
}

/** @internal */
export function currentContext(): BlackboxContext {
    return carrier.get() ?? defaultSession.context;
}
