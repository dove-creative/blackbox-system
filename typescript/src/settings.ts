import { TargetTypes } from "./types.js";
import type { BlackboxOptions } from "./types.js";

type OptionalSetting = "normalLogger" | "warningLogger" | "onExport" | "logDirectory";

/** Fully resolved session settings, as exposed by BlackboxSession.settings. */
export type Settings = Readonly<Required<Omit<BlackboxOptions, OptionalSetting>> & Pick<BlackboxOptions, OptionalSetting>>;

/** @internal */
export const defaults: Settings = Object.freeze({
    useBlackbox: true, maxLogCount: 100, strongReference: false,
    defaultRecursionDepth: 100, exportFormat: "html", fullExport: true,
    openLog: false, exceptionHandling: "none", tagTargetTypes: TargetTypes.Full,
    normalLogger: undefined, warningLogger: undefined, onExport: undefined, logDirectory: undefined,
});

// Each object store preallocates its ring as a JavaScript array.
const maxArrayLength = 2 ** 32 - 1;

/** @internal */
export function resolveSettings(previous: Settings, options: BlackboxOptions): Settings {
    const next = { ...previous, ...options };
    // Undefined callbacks explicitly clear them; undefined scalar options inherit.
    for (const name of ["useBlackbox", "maxLogCount", "strongReference", "defaultRecursionDepth",
        "exportFormat", "fullExport", "openLog", "exceptionHandling", "tagTargetTypes"] as const)
        if (next[name] === undefined) (next as Record<string, unknown>)[name] = previous[name];
    if (!Number.isInteger(next.maxLogCount) || next.maxLogCount < 1 || next.maxLogCount > maxArrayLength)
        throw new RangeError(`maxLogCount must be an integer from 1 to ${maxArrayLength}.`);
    if (!Number.isSafeInteger(next.defaultRecursionDepth))
        throw new RangeError("defaultRecursionDepth must be a safe integer.");
    if (next.exportFormat !== "html" && next.exportFormat !== "text")
        throw new TypeError("exportFormat must be html or text.");
    if (next.exceptionHandling !== "none" && next.exceptionHandling !== "crashExport")
        throw new TypeError("exceptionHandling must be none or crashExport.");
    if (!Number.isInteger(next.tagTargetTypes) || next.tagTargetTypes < 0 || next.tagTargetTypes > 15)
        throw new RangeError("tagTargetTypes must contain valid TargetTypes flags.");
    if ((next.tagTargetTypes & TargetTypes.BasedOnSettings) !== 0)
        next.tagTargetTypes = previous.tagTargetTypes | (next.tagTargetTypes & ~TargetTypes.BasedOnSettings);
    return Object.freeze(next);
}
