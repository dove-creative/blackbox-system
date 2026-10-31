import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";
import { renderExport, renderMessage } from "../dist/render.js";
import { reportScript } from "../dist/report-script.js";
import { TargetTypes } from "../dist/types.js";

const names = { 0: "Source #0", 1: "Worker #1", 2: "Other #2" };

function log(sequence, overrides = {}) {
    return {
        sequence,
        time: "2026-10-31T12:34:56.789Z",
        contextId: 1,
        message: "message " + sequence,
        methodName: "Run",
        scopeType: "none",
        scopeId: -1,
        parentScopeId: null,
        interactionId: -1,
        tagTargetTypes: TargetTypes.Full,
        tags: [],
        ...overrides,
    };
}

function snapshot(logs, extraNodes = [], overrides = {}) {
    return {
        sessionKey: "example-session",
        rootId: 0,
        maxSequence: 100,
        names,
        nodes: [{ id: 0, name: names[0], depth: 0, parentId: null, logs }, ...extraNodes],
        ...overrides,
    };
}

function freeze(value) {
    if (value && typeof value === "object") {
        Object.freeze(value);
        for (const child of Object.values(value)) freeze(child);
    }
    return value;
}

test("messages replace complete repeated multi-digit placeholders and preserve unmatched placeholders", () => {
    const tags = Array.from({ length: 11 }, (_, index) => ({
        id: index + 20,
        targetId: index + 10,
        targetName: "Target " + index,
    }));
    const input = log(1, { message: "%10\n%1 %10 %99 %99999999999999999999", tags });
    const text = renderMessage(input, {});
    assert.equal((text.match(/'Target 10 \[30\]'/g) ?? []).length, 2);
    assert.ok(text.includes("'Target 10 [30]'\n'Target 1 [21]'"));
    assert.ok(text.includes("%99 %99999999999999999999"));
    assert.ok(text.includes("(tag %0: 'Target 0 [20]')"));
    assert.ok(!text.includes("(tag %10:"));
});

test("unused message-only targets are omitted while null and linked tags are retained", () => {
    const input = log(1, {
        message: "value",
        tags: [
            { id: -1, targetId: 0, targetName: names[0] },
            { id: -1, targetId: null, targetName: "null" },
            { id: 8, targetId: 1, targetName: names[1] },
        ],
    });
    assert.equal(renderMessage(input, names), "value (tag %1: null) (tag %2: 'Worker #1 [8]')");
    assert.equal(renderMessage(log(1, { message: "%0", tags: [input.tags[0]] }), names), "'Source #0'");
    assert.equal(renderMessage(log(1, { message: "%0", tags: [input.tags[1]] }), names), "null");
});

test("target tag policies control source details independently of placeholder rendering", () => {
    const input = log(1, { message: "payload", taggedBy: 0, interactionId: 3 });
    assert.equal(renderMessage({ ...input, tagTargetTypes: TargetTypes.None }, names), "tagged");
    assert.equal(renderMessage({ ...input, tagTargetTypes: TargetTypes.Name }, names), "tagged by 'Source #0'");
    assert.equal(renderMessage({ ...input, tagTargetTypes: TargetTypes.InteractionId }, names), "tagged [3]");
    assert.equal(renderMessage({ ...input, tagTargetTypes: TargetTypes.Message }, names), "tagged: payload");
    assert.equal(renderMessage(input, names), "tagged by 'Source #0': payload");
    assert.equal(renderMessage({ ...input, tagTargetTypes: TargetTypes.InteractionId }, names, false), "tagged");
});

test("empty scopes flatten within their context even when unrelated flows interleave", () => {
    const data = snapshot([
        log(1, { message: "start", scopeType: "open", scopeId: 1 }),
        log(2, { message: "parallel", contextId: 2 }),
        log(3, { message: "end", scopeType: "close", scopeId: 1 }),
    ]);
    const text = renderExport(data, "text");
    assert.ok(text.includes("<Run /> start > end"));
    assert.ok(text.includes("[Run] parallel"));
    assert.ok(!text.includes("</Run>"));
});

test("descendant callback records prevent flattening the parent scope", () => {
    const data = snapshot([
        log(1, { message: "outer", scopeType: "open", scopeId: 1 }),
        log(2, { message: "nested", scopeType: "open", scopeId: 2, parentScopeId: 1, contextId: 2 }),
        log(3, { message: "", scopeType: "close", scopeId: 2, parentScopeId: 1, contextId: 2 }),
        log(4, { message: "", scopeType: "close", scopeId: 1 }),
    ]);
    const text = renderExport(data, "text");
    assert.ok(text.includes("<Run> outer"));
    assert.ok(text.includes("|   <Run /> nested"));
    assert.ok(text.includes("</Run>"));
});

test("export sorts and cuts off a frozen snapshot without rewriting source logs", () => {
    const data = freeze(snapshot([
        log(5, { message: "outside snapshot" }),
        log(4, { message: "fourth", scopeType: "close", scopeId: 20 }),
        log(2, { message: "second", parentScopeId: 10 }),
        log(3, { message: "third", scopeType: "close", scopeId: 10 }),
    ], [], { maxSequence: 4 }));
    const before = JSON.stringify(data);
    const text = renderExport(data, "text");
    const html = renderExport(data, "html");
    assert.ok(text.indexOf("second") < text.indexOf("third"));
    assert.ok(text.indexOf("third") < text.indexOf("fourth"));
    assert.ok(!text.includes("outside snapshot"));
    assert.ok(!html.includes("outside snapshot"));
    assert.ok(!html.includes('data-depth="-'));
    assert.equal(JSON.stringify(data), before);
});

test("HTML escapes all user content including crash details, names, messages and attributes", () => {
    const payload = '</script><script>alert("x")</script><img src=x onerror=alert(1)>';
    const data = snapshot([
        log(1, {
            message: payload,
            methodName: "<img>",
            tags: [{ id: 3, targetId: 1, targetName: payload }],
        }),
    ], [], { names: { 0: payload, 1: payload }, crash: { message: payload, stack: payload } });
    data.nodes[0].name = payload;
    const html = renderExport(data, "html");
    assert.equal((html.match(/<script>/g) ?? []).length, 1);
    assert.equal((html.match(/<\/script>/g) ?? []).length, 1);
    assert.ok(!html.includes(payload));
    assert.ok(html.includes("&lt;/script&gt;"));
    assert.ok(html.includes("&quot;x&quot;"));
    assert.ok(html.includes("&lt;img&gt;"));
    assert.ok(html.includes('aria-label="Crash details"'));
});

test("HTML gives interactions and tags retained anchors with separate tag badges", () => {
    const data = snapshot([
        log(1, {
            message: "hello %0",
            interactionId: 4,
            exertingTo: 1,
            tags: [{ id: 5, targetId: 1, targetName: names[1] }],
        }),
    ], [{
        id: 1, name: names[1], depth: 1, parentId: 0,
        logs: [
            log(2, { interactionId: 4, exertedBy: 0 }),
            log(3, { interactionId: 5, taggedBy: 0 }),
        ],
    }]);
    const html = renderExport(data, "html");
    assert.ok(html.includes('href="#log_1_4" class="interaction"'));
    assert.ok(html.includes('href="#log_1_5" class="interaction tag-interaction"'));
    assert.ok(html.includes('id="log_0_5"'));
    assert.ok(html.includes('href="#b0"'));
    assert.ok(html.includes('class="message">hello &#39;Worker #1&#39;</span>'));
    assert.ok(!html.includes("Worker #1 [5]"));
    for (const id of ["log_0_4", "log_0_5", "log_1_4", "log_1_5"])
        assert.equal(html.split('id="' + id + '"').length - 1, 1);
});

test("HTML disables links when their exact destination has left the ring buffer", () => {
    const data = snapshot([
        log(1, { interactionId: 4, exertingTo: 1, tags: [{ id: 5, targetId: 1, targetName: names[1] }] }),
    ], [{ id: 1, name: names[1], parentId: 0, depth: 1, logs: [log(8)] }]);
    const html = renderExport(data, "html");
    assert.ok(!html.includes('href="#log_1_4"'));
    assert.ok(!html.includes('href="#log_1_5"'));
    assert.ok(html.includes('class="interaction disabled"'));
    assert.ok(html.includes('class="interaction tag-interaction disabled"'));
});

test("parallel scopes have independent collapse memberships, including closing rows", () => {
    const data = snapshot([
        log(1, { scopeType: "open", scopeId: 10, contextId: 10 }),
        log(2, { scopeType: "open", scopeId: 20, contextId: 20 }),
        log(3, { parentScopeId: 10, contextId: 10 }),
        log(4, { parentScopeId: 20, contextId: 20 }),
        log(5, { scopeType: "close", scopeId: 10, contextId: 10 }),
        log(6, { scopeType: "close", scopeId: 20, contextId: 20 }),
    ]);
    const html = renderExport(data, "html");
    assert.match(html, /id="row_0_3"[^>]*data-scope-ancestors="0:10"/);
    assert.match(html, /id="row_0_4"[^>]*data-scope-ancestors="0:20"/);
    assert.match(html, /id="row_0_5"[^>]*data-scope-ancestors="0:10"/);
    assert.match(html, /id="row_0_6"[^>]*data-scope-ancestors="0:20"/);
    assert.ok(html.includes("prefers-reduced-motion"));
    assert.ok(html.includes('data-action="expand"'));
    assert.ok(html.includes('aria-expanded="true"'));
});

test("text reports missing graph peers and preserves multiline messages and crash data", () => {
    const data = snapshot([
        log(1, { message: "line 1\nline 2", interactionId: 3, exertingTo: 2 }),
    ], [], { crash: { message: "failure", stack: "call stack" } });
    const text = renderExport(data, "text");
    assert.ok(text.startsWith("[Error] failure\n[STACK TRACE]\ncall stack\n"));
    assert.ok(text.includes("line 1\nline 2"));
    assert.ok(text.includes("-[3]-> Other #2"));
    assert.ok(text.includes("(not included)"));
});

test("HTML report script ships as a string literal that consumer transforms cannot rewrite", () => {
    const module = readFileSync(new URL("../dist/report-script.js", import.meta.url), "utf8");
    assert.match(module, /^export const reportScript = "\(function initializeReport\(\)/);
    assert.equal(module.split("\n").filter(Boolean).length, 1);
    assert.ok(renderExport(snapshot([log(1)]), "html").includes(`<script>${reportScript}</script></body></html>`));
});

test("HTML inline fragments do not introduce layout whitespace while preserving message newlines", () => {
    const html = renderExport(snapshot([
        log(1, { message: "open", scopeType: "open", scopeId: 1 }),
        log(2, { message: "line one\nline two", parentScopeId: 1 }),
        log(3, { message: "", scopeType: "close", scopeId: 1 }),
    ]), "html");
    assert.ok(html.includes('padding-left:0px"><button'));
    assert.ok(html.includes('</button><span class="method">'));
    assert.ok(html.includes('</span> <span class="message">open</span>'));
    assert.ok(html.includes('<span class="message">line one\nline two</span>'));
});
