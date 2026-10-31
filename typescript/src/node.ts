import { AsyncLocalStorage } from "node:async_hooks";
import { closeSync, mkdirSync, openSync, writeFileSync } from "node:fs";
import { basename, extname, resolve } from "node:path";
import { openExport } from "./open-export.js";
import { setupRuntime } from "./runtime.js";
import type { BlackboxContext } from "./context.js";
import type { BlackboxOptions, ExportArtifact, ExportOptions } from "./types.js";

export * from "./public.js";

export interface SaveExportOptions {
    directory: string;
    openLog?: boolean;
    warningLogger?: (message: string) => void;
}

const ambient = new AsyncLocalStorage<BlackboxContext>();

/** Saves without overwriting an existing file and returns its absolute path. */
export function saveExport(artifact: ExportArtifact, options: SaveExportOptions): string {
    if (!options.directory.trim()) throw new Error("[Blackbox] Log directory is empty.");
    mkdirSync(options.directory, { recursive: true });
    const name = basename(artifact.fileName).replace(/[<>:"/\\|?*\x00-\x1f]/g, "_");
    const extension = extname(name);
    const stem = name.slice(0, name.length - extension.length);
    let path = "";
    let descriptor: number;
    for (let ordinal = 0; ; ordinal++) {
        const candidate = ordinal === 0 ? name : `${stem}-${ordinal}${extension}`;
        path = resolve(options.directory, candidate);
        try {
            descriptor = openSync(path, "wx");
            artifact.fileName = candidate;
            break;
        } catch (error) {
            if ((error as NodeJS.ErrnoException).code !== "EEXIST") throw error;
        }
    }
    try {
        writeFileSync(descriptor, artifact.content, { encoding: "utf8" });
    } finally {
        closeSync(descriptor);
    }
    if (options.openLog) openExport(path, options.warningLogger);
    return path;
}

function exportArtifact(artifact: ExportArtifact, settings: Readonly<BlackboxOptions>, options: ExportOptions): void {
    if (!settings.logDirectory) return;
    const path = saveExport(artifact, {
        directory: settings.logDirectory,
        openLog: options.openLog ?? settings.openLog ?? false,
        warningLogger: settings.warningLogger,
    });
    settings.normalLogger?.(`[Blackbox] Exported ${path}`);
}

setupRuntime({
    contextCarrier: {
        get: () => ambient.getStore(),
        run: (context, callback) => ambient.run(context, callback),
    },
    exportArtifact,
});
