// Platform-neutral recording engine. The entry points import it through public.ts.
import { setDefaultSession } from "./runtime.js";
import { BlackboxSession } from "./session.js";
import { defaults } from "./settings.js";

export { BlackboxSession } from "./session.js";
export { BlackboxContext } from "./context.js";
export { BlackboxHandle, TagHandle, ScopeHandle, ExertHandle } from "./handles.js";

// Every module is evaluated by now, so the shared default session can be created.
setDefaultSession(new BlackboxSession(defaults));
