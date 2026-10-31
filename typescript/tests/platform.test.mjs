import { test } from "node:test";
import assert from "node:assert/strict";
import { EventEmitter } from "node:events";
import { mkdtempSync, readFileSync, readdirSync, writeFileSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { join } from "node:path";
import * as api from "../dist/node.js";
import { openExport } from "../dist/open-export.js";
import { runScenarios } from "./scenarios.mjs";

test("common behavior runs in the Node entry with ambient async isolation", async () => {
    assert.ok(await runScenarios(api, true) >= 15);
});

test("file export never overwrites and reports the final filename", () => {
    const directory = mkdtempSync(join(tmpdir(), "blackbox-export-"));
    try {
        const artifact = { fileName: "same.txt", content: "first", mimeType: "text/plain" };
        api.saveExport(artifact, { directory });
        const second = { ...artifact, content: "second" };
        const path = api.saveExport(second, { directory });
        assert.equal(readFileSync(join(directory, "same.txt"), "utf8"), "first");
        assert.equal(readFileSync(path, "utf8"), "second");
        assert.equal(second.fileName, "same-1.txt");
    } finally { rmSync(directory, { recursive: true, force: true }); }
});

test("export opener ignores explorer exit codes and reports other viewer failures", () => {
    for (const [platform, command, reportsExitCode] of [
        ["win32", "explorer.exe", false], ["darwin", "open", true], ["linux", "xdg-open", true],
    ]) {
        const warnings = [];
        let launched;
        const child = Object.assign(new EventEmitter(), { unref() { } });
        openExport(join(tmpdir(), "report.html"), message => warnings.push(message), platform, (name, args) => {
            launched = { name, args };
            return child;
        });
        assert.equal(launched.name, command);
        child.emit("exit", 1);
        assert.equal(warnings.length, reportsExitCode ? 1 : 0, `${platform} exit code handling`);
        child.emit("error", new Error("viewer missing"));
        assert.match(warnings.at(-1), /viewer missing/, `${platform} launch failure`);
    }
});

test("explicit output callback overrides the directory sink", () => {
    const directory = mkdtempSync(join(tmpdir(), "blackbox-sink-"));
    const results = [];
    const session = api.BlackboxHandle.createSession({ logDirectory: directory, onExport: artifact => results.push(artifact) });
    try {
        const handle = session.of({});
        handle.write("hello");
        assert.equal(handle.export(), results[0]);
        assert.equal(results.length, 1);
        assert.deepEqual(readdirSync(directory), []);
    } finally { session.dispose(); rmSync(directory, { recursive: true, force: true }); }
});

test("failed crash output freezes the session and regular export retries", () => {
    const directory = mkdtempSync(join(tmpdir(), "blackbox-retry-"));
    const file = join(directory, "not-a-directory");
    writeFileSync(file, "keep");
    const session = api.BlackboxHandle.createSession({ logDirectory: file });
    try {
        const handle = session.of({});
        handle.write("before-crash");
        assert.throws(() => handle.crashExport(new Error("crash-marker")));
        handle.write("after-crash-must-not-write");
        rmSync(file);
        const output = handle.export({ format: "text" });
        assert.match(output.content, /crash-marker/);
        assert.doesNotMatch(output.content, /after-crash-must-not-write/);
        assert.equal(readFileSync(join(file, output.fileName), "utf8"), output.content);
    } finally { session.dispose(); rmSync(directory, { recursive: true, force: true }); }
});
