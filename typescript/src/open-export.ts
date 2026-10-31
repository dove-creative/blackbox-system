import { spawn } from "node:child_process";
import type { ChildProcess, SpawnOptions } from "node:child_process";
import { pathToFileURL } from "node:url";

/** @internal Starts the platform viewer; tests replace it to observe launches without opening files. */
export type LaunchViewer = (command: string, args: readonly string[], options: SpawnOptions) => ChildProcess;

/** @internal Opens a saved export and reports launch failures through the warning logger. */
export function openExport(
    path: string, warningLogger?: (message: string) => void,
    platform: NodeJS.Platform = process.platform, launch: LaunchViewer = spawn,
): void {
    const warn = (error: unknown) => warningLogger?.(`[Blackbox] Could not open export: ${String(error)}`);
    try {
        // Argument arrays avoid treating file names as shell commands.
        const command = platform === "win32" ? "explorer.exe" : platform === "darwin" ? "open" : "xdg-open";
        const argument = platform === "win32" ? path : pathToFileURL(path).href;
        const child = launch(command, [argument], { stdio: "ignore", windowsHide: true });
        child.once("error", warn);
        // explorer.exe returns exit code 1 even after it opens the file, so only other viewers report it.
        if (platform !== "win32")
            child.once("exit", code => { if (code) warn(`exit code ${code}`); });
        child.unref();
    } catch (error) {
        warn(error);
    }
}
