import { createServer } from "node:http";
import { mkdir, readFile } from "node:fs/promises";
import { resolve, sep, extname } from "node:path";
import { fileURLToPath } from "node:url";
import assert from "node:assert/strict";
import { chromium, firefox, webkit } from "playwright";

const root = fileURLToPath(new URL("../", import.meta.url));
const server = createServer(async (request, response) => {
    const pathname = decodeURIComponent(new URL(request.url, "http://localhost").pathname);
    // resolve() drops the trailing separator for "/", so serve the blank page before the containment check.
    if (pathname === "/") {
        response.setHeader("Content-Type", "text/html");
        response.end("<!doctype html><html><body></body></html>");
        return;
    }
    const path = resolve(root, `.${pathname}`);
    if (!path.startsWith(root.endsWith(sep) ? root : root + sep)) { response.writeHead(403).end(); return; }
    try {
        const content = await readFile(path);
        response.setHeader("Content-Type", extname(path) === ".js" || extname(path) === ".mjs" ? "text/javascript" : "text/html");
        response.end(content);
    } catch { response.writeHead(404).end(); }
});
await new Promise(resolve => server.listen(0, "127.0.0.1", resolve));
const origin = `http://127.0.0.1:${server.address().port}`;
const failures = [];
try {
    for (const [name, engine] of Object.entries({ chromium, firefox, webkit })) {
        if (process.env.BLACKBOX_BROWSERS && !process.env.BLACKBOX_BROWSERS.split(",").includes(name)) continue;
        let browser;
        try {
            browser = await engine.launch({ headless: true });
            const page = await browser.newPage({ acceptDownloads: true });
            const errors = [];
            page.on("pageerror", error => errors.push(error.message));
            await page.goto(origin);
            const checks = await page.evaluate(async () => {
                const api = await import("/dist/browser.js");
                const { runScenarios } = await import("/tests/scenarios.mjs");
                return runScenarios(api, false);
            });
            assert.ok(checks >= 15);
            const downloadPromise = page.waitForEvent("download");
            await page.evaluate(async () => {
                const { downloadExport } = await import("/dist/browser.js");
                downloadExport({ content: "download-marker", fileName: "trace.txt", mimeType: "text/plain" });
            });
            const download = await downloadPromise;
            assert.equal(download.suggestedFilename(), "trace.txt");
            assert.equal(await readFile(await download.path(), "utf8"), "download-marker");
            const html = await page.evaluate(async () => {
                const { BlackboxHandle } = await import("/dist/browser.js");
                const session = BlackboxHandle.createSession();
                const handle = session.of({}, { label: "browser-report" });
                const scope = handle.scope("fold-marker", { methodName: "load" });
                handle.write("inner-marker");
                scope.dispose();
                const target = {};
                const receiver = session.of(target, { label: "receiver" });
                const exert = handle.exert(target, "call-marker");
                const received = receiver.scope("receiver-scope");
                receiver.write("receiver-marker");
                received.dispose();
                exert.dispose();
                handle.write("tag %0").with(target);
                const artifact = handle.export();
                session.dispose();
                return artifact.content;
            });
            await page.setContent(html);
            assert.ok((await page.locator("body").innerText()).includes("inner-marker"));
            const inner = page.locator(".log-row").filter({ hasText: "inner-marker" });
            await page.locator(".scope-toggle").first().click();
            assert.equal(await inner.isVisible(), false);
            await page.locator(".scope-toggle").first().click();
            assert.equal(await inner.isVisible(), true);
            await page.locator("a.interaction").first().click();
            assert.ok(await page.locator(".highlight").count() > 0);
            if (name === "chromium") {
                await mkdir(resolve(root, "../artifacts"), { recursive: true });
                await page.screenshot({ path: resolve(root, "../artifacts/typescript-report.png"), fullPage: true });
            }
            await page.goto(`${origin}/samples/browser.html`);
            for (const scenario of ["write", "exert", "tag", "exception", "async"]) {
                await page.locator("#scenario").selectOption(scenario);
                await page.locator("#run").click();
                await page.locator("#status").filter({ hasText: `Completed: ${scenario}.` }).waitFor();
                assert.equal(await page.locator("#downloads button").count(), scenario === "async" ? 2 : 1);
            }
            const sampleDownload = page.waitForEvent("download");
            await page.locator("#downloads button").first().click();
            assert.match(await readFile(await (await sampleDownload).path(), "utf8"), /Blackbox Log Report/);
            assert.deepEqual(errors, []);
            console.log(`${name}: ${checks} shared checks, download, HTML controls and all samples verified`);
        } catch (error) {
            failures.push(new Error(`${name}: ${error.message}`));
            console.error(`${name}: ${error.message}`);
        } finally { await browser?.close(); }
    }
} finally { await new Promise(resolve => server.close(resolve)); }
if (failures.length) throw new AggregateError(failures, "Browser verification failed");
