import assert from "node:assert/strict";
import { after, before, test } from "node:test";
import { spawn } from "node:child_process";
import { cpSync, mkdirSync, mkdtempSync, readFileSync, readdirSync, rmSync } from "node:fs";
import { tmpdir } from "node:os";
import { basename, dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const packageRoot = fileURLToPath(new URL("../", import.meta.url));
let fixture;
let sample;
let outputDirectory;

before(() => {
    fixture = mkdtempSync(join(tmpdir(), "blackbox-sample-"));
    cpSync(join(packageRoot, "package.json"), join(fixture, "package.json"));
    cpSync(join(packageRoot, "dist"), join(fixture, "dist"), { recursive: true });
    mkdirSync(join(fixture, "samples"));
    sample = join(fixture, "samples", "node.mjs");
    cpSync(join(packageRoot, "samples", "node.mjs"), sample);
    outputDirectory = join(fixture, "samples", "output");
});

after(() => {
    if (!fixture) return;
    const target = resolve(fixture);
    if (dirname(target) !== resolve(tmpdir()) || !basename(target).startsWith("blackbox-sample-"))
        throw new Error("Sample fixture cleanup escaped its temporary directory.");
    rmSync(target, { recursive: true, force: true });
});

function exports() {
    try { return readdirSync(outputDirectory).filter(name => name.endsWith(".html")); }
    catch (error) {
        if (error.code === "ENOENT") return [];
        throw error;
    }
}

function runSample(args, input) {
    return new Promise((resolveResult, reject) => {
        const child = spawn(process.execPath, [sample, ...args], {
            cwd: fixture,
            stdio: ["pipe", "pipe", "pipe"],
            windowsHide: true,
        });
        let stdout = "";
        let stderr = "";
        child.stdout.setEncoding("utf8");
        child.stderr.setEncoding("utf8");
        child.stdout.on("data", data => { stdout += data; });
        child.stderr.on("data", data => { stderr += data; });
        const timeout = setTimeout(() => {
            child.kill();
            reject(new Error("Sample did not finish within 10 seconds.\n" + stdout + stderr));
        }, 10000);
        child.once("error", error => { clearTimeout(timeout); reject(error); });
        child.once("close", code => {
            clearTimeout(timeout);
            resolveResult({ code, stdout, stderr });
        });
        // One-shot commands must finish even while standard input stays open.
        if (input !== undefined) child.stdin.end(input);
    });
}

test("sample help exits with stdin open and does not produce output", async () => {
    const before = exports();
    const result = await runSample(["help"]);
    assert.equal(result.code, 0);
    assert.match(result.stdout, /Commands: write, exert, tag, exception, async, all, help, exit/);
    assert.doesNotMatch(result.stdout, /sample>/);
    assert.equal(result.stderr, "");
    assert.deepEqual(exports(), before);
});

test("all sample scenarios finish once and produce readable exports below the sample", async () => {
    const before = new Set(exports());
    const result = await runSample(["all"]);
    assert.equal(result.code, 0, result.stderr);
    for (const scenario of ["write", "exert", "tag", "exception", "async"])
        assert.match(result.stdout, new RegExp("Completed: " + scenario));
    assert.doesNotMatch(result.stdout, /sample>/);
    assert.doesNotMatch(result.stderr, /Sample failed/);
    assert.match(result.stdout, /Expected failure recorded/);
    const created = exports().filter(name => !before.has(name));
    assert.equal(created.length, 6);
    assert.equal(created.filter(name => name.startsWith("[CRASH]")).length, 1);
    for (const name of created)
        assert.match(readFileSync(join(outputDirectory, name), "utf8"), /<html/i);
    assert.deepEqual(readdirSync(fixture).sort(), ["dist", "package.json", "samples"]);
});

test("invalid commands and extra arguments return usage exit code 2 without exporting", async () => {
    const before = exports();
    for (const args of [["unknown"], ["write", "extra"]]) {
        const result = await runSample(args);
        assert.equal(result.code, 2);
        assert.notEqual(result.stderr, "");
        assert.doesNotMatch(result.stdout, /Completed:/);
    }
    assert.deepEqual(exports(), before);
});

test("interactive input preserves queued commands while asynchronous scenarios run", async () => {
    const before = new Set(exports());
    const result = await runSample([], "\nunknown\nhelp\nwrite\ntag\nexit\n");
    assert.equal(result.code, 0, result.stderr);
    assert.match(result.stdout, /sample>/);
    assert.match(result.stdout, /Completed: write/);
    assert.match(result.stdout, /Completed: tag/);
    assert.match(result.stderr, /Unknown command: unknown/);
    assert.doesNotMatch(result.stderr, /readline was closed|Sample failed/);
    assert.equal(exports().filter(name => !before.has(name)).length, 2);
});

test("interactive EOF after an asynchronous command finishes cleanly", async () => {
    const result = await runSample([], "write\n");
    assert.equal(result.code, 0, result.stderr);
    assert.match(result.stdout, /Completed: write/);
    assert.doesNotMatch(result.stderr, /readline was closed|Sample failed/);
});

test("interactive empty EOF exits without generating a report", async () => {
    const before = exports();
    const result = await runSample([], "");
    assert.equal(result.code, 0);
    assert.match(result.stdout, /sample>/);
    assert.equal(result.stderr, "");
    assert.deepEqual(exports(), before);
});
