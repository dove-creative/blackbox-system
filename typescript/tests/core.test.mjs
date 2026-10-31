import assert from "node:assert/strict";
import { afterEach, test } from "node:test";
import { execFile } from "node:child_process";
import { promisify } from "node:util";
import {
    BlackboxHandle as B, ScopeHandle, ExertHandle, TagHandle,
} from "../dist/core.js";
import { TargetTypes } from "../dist/types.js";

function session(options = {}) {
    return B.createSession({
        useBlackbox: true, maxLogCount: 100, strongReference: false, defaultRecursionDepth: 100,
        exportFormat: "text", fullExport: true, openLog: false, exceptionHandling: "none",
        tagTargetTypes: TargetTypes.Full, warningLogger: undefined, normalLogger: undefined,
        onExport: undefined, logDirectory: undefined, ...options,
    });
}

function snapshot(s, subject, options = {}) { return s.snapshot(s.getBox(subject), options); }
function logs(s, subject) { return snapshot(s, subject).nodes[0].logs; }
function tick() { return new Promise(resolve => setTimeout(resolve, 0)); }

afterEach(() => {
    B.disposeActiveSessions();
    B.restartDefaultSession();
    B.configure({ useBlackbox: true, onExport: undefined, warningLogger: undefined, logDirectory: undefined });
});

test("identity and first nonblank label are session-local and never stringify subjects", () => {
    let conversions = 0;
    const subject = { toString() { conversions++; throw new Error("must not run"); } };
    const warnings = [];
    const s = session({ warningLogger: message => warnings.push(message) });
    const a = s.of(subject);
    assert.equal(s.of(subject, { label: " " }).id, a.id);
    s.of(subject, { label: "original" });
    s.of(subject, { label: "other" });
    s.of(subject, { label: "another" });
    assert.equal(a.ownerString, "original");
    assert.equal(warnings.length, 1);
    assert.equal(session().of(subject, { label: "independent" }).ownerString, "independent");
    a.write("value %0").with(subject);
    assert.match(a.export().content, /original/);
    assert.equal(conversions, 0);
});

test("only object and function subjects are accepted; disabled lookup does not validate", () => {
    const s = session();
    for (const value of [null, undefined, 2, "string", Symbol("test"), true])
        assert.throws(() => s.of(value), TypeError);
    assert.equal(s.of(() => {}).isValid, true);
    s.configure({ useBlackbox: false });
    assert.equal(s.of(null).isValid, false);
});

test("disabled existing handles preserve strings, skip lazy messages and target registration", () => {
    const s = session();
    const subject = {};
    const b = s.of(subject);
    s.configure({ useBlackbox: false });
    let evaluations = 0;
    const message = () => { evaluations++; return "lazy"; };
    assert.equal(b.write("fallback %0").with(null), "fallback %0");
    assert.equal(b.write(message).with({}), "");
    b.scope(message).dispose();
    b.exert({}, message).dispose();
    b.construct(message).dispose();
    b.dispose(message);
    assert.equal(b.exertMessage(null, "original"), "original");
    assert.equal(evaluations, 0);
    s.configure({ useBlackbox: true });
    assert.deepEqual(logs(s, subject), []);
    assert.equal(s.of({}).id, b.id + 1);
});

test("default handles and duplicate cleanup are harmless", () => {
    const b = new B();
    assert.equal(b.isValid, false);
    assert.equal(b.write("fallback").toString(), "fallback");
    assert.equal(new TagHandle().with(null), "");
    for (const handle of [new ScopeHandle(), new ExertHandle()]) {
        handle.dispose(); handle.dispose();
        assert.equal(handle.isDisposed, true);
    }
});

test("construct and dispose are recording helpers, not the object's lifetime", () => {
    const s = session();
    const subject = {};
    const b = s.of(subject);
    b.construct("created").dispose();
    assert.equal(b.dispose("closed"), b);
    b.write("still records");
    assert.equal(b.isValid, true);
    assert.deepEqual(logs(s, subject).map(log => log.scopeType), ["open", "close", "none", "none"]);
    assert.match(b.export().content, /still records/);
    assert.equal(Object.hasOwn(B.prototype, Symbol.dispose), false);
});

test("scope ancestry is explicit, outer-first disposal closes only its own branch", () => {
    const warnings = [];
    const s = session({ warningLogger: message => warnings.push(message) });
    const subject = {};
    const b = s.of(subject);
    const outer = b.scope("outer");
    const inner = b.scope("inner");
    const branch = s.context.fork();
    const independent = branch.of(subject).scope("branch");
    outer.dispose(); outer.dispose(); inner.dispose();
    assert.equal(inner.isDisposed, true);
    assert.equal(independent.isAlive, true);
    independent.dispose();
    const entries = logs(s, subject);
    assert.equal(entries.filter(log => log.scopeType === "close").length, 3);
    assert.equal(entries[1].parentScopeId, entries[0].scopeId);
    assert.equal(entries[2].parentScopeId, entries[1].scopeId);
    assert.equal(warnings.length, 1);
});

test("scope tags attach to its open record and return the same scope handle", () => {
    const s = session();
    const a = {}, b = {};
    const scope = s.of(a).scope("scope %0");
    assert.equal(scope.with(b), scope);
    scope.dispose();
    assert.equal(logs(s, a)[0].tags.length, 1);
    assert.equal(logs(s, b)[0].taggedBy, s.of(a).id);
});

for (const closeMode of ["explicit", "outer"]) {
    test(`${closeMode}-closed scopes cannot replace tags, record peers or register targets`, () => {
        const s = session();
        const source = {}, existingTarget = {}, unseenTarget = {};
        const handle = s.of(source);
        const peer = s.of(existingTarget);
        const outer = closeMode === "outer" ? handle.scope("outer") : undefined;
        const scope = handle.scope("original %0").with(existingTarget);
        if (outer) outer.dispose();
        else scope.dispose();
        assert.equal(scope.isDisposed, true);
        const sourceBefore = logs(s, source);
        const peerBefore = logs(s, existingTarget);
        const sequenceBefore = s.currentSequence;

        assert.equal(scope.with(unseenTarget, existingTarget, TargetTypes.Full), scope);
        assert.deepEqual(logs(s, source), sourceBefore);
        assert.deepEqual(logs(s, existingTarget), peerBefore);
        assert.equal(s.currentSequence, sequenceBefore);
        assert.equal(s.of({}).id, peer.id + 1, "Ignored targets must not consume object IDs");
        assert.deepEqual(logs(s, unseenTarget), []);
    });
}

test("tags support multi-digit positions, null, unused tags, and self-tag suppression", () => {
    const s = session();
    const subject = {};
    const objects = Array.from({ length: 11 }, () => ({}));
    const b = s.of(subject);
    const text = b.write("%10 %0 %99").with(...objects);
    assert.doesNotMatch(text, /%10\b/);
    assert.match(text, /%99/);
    assert.match(text, /tag %1:/);
    assert.equal(b.write("empty").with(), "empty");
    assert.match(b.write("null").with(null), /tag %0: null/);
    const before = logs(s, subject).length;
    assert.equal(b.write("self").with(subject), "self");
    assert.equal(logs(s, subject).length, before + 1);
    assert.match(b.write("self %0").with(subject), /Object #0/);
});

test("tag target policies are resolved at record time and do not extend the export graph", () => {
    const s = session({ tagTargetTypes: TargetTypes.Name });
    const a = {}, b = {};
    s.of(a).write("secret").with(b);
    assert.equal(logs(s, b)[0].tagTargetTypes, TargetTypes.Name);
    assert.equal(snapshot(s, a).nodes.length, 1);
    s.of(a).write("second").with(b, TargetTypes.None);
    assert.equal(logs(s, b)[1].tagTargetTypes, TargetTypes.None);
});

test("Exert records paired IDs and merges only an adjacent receiver open", () => {
    const s = session();
    const source = {}, target = {};
    const exert = s.of(source).exert(target, "call");
    const scope = s.of(target).scope("receive");
    exert.dispose(); exert.dispose(); scope.dispose();
    const outgoing = logs(s, source)[0];
    const incoming = logs(s, target)[0];
    assert.equal(incoming.scopeType, "open");
    assert.equal(outgoing.interactionId, incoming.interactionId);
    assert.match(incoming.message, /receive \(from: call\)/);
    assert.equal(logs(s, target).length, 2);
    const second = s.of(source).exert(target, "other");
    s.of(target).write("intervening");
    s.of(target).scope("unmerged").dispose();
    second.dispose();
    assert.equal(logs(s, target).filter(log => log.scopeType === "none").length, 2);
});

test("Exert does not merge an unrelated context or a different session", () => {
    const s = session();
    const a = {}, b = {};
    const exert = s.of(a).exert(b, "call");
    s.context.fork().of(b).scope("other branch").dispose();
    session().of(b).scope("other session").dispose();
    exert.dispose();
    assert.equal(logs(s, b)[0].scopeType, "none");
});

test("runExert merges the direct receiver runScope callback in the same branch", async () => {
    const s = session();
    const a = {}, b = {};
    await s.of(a).runExert(b, "call", context => context.of(b).runScope("receive", async child => {
        await tick();
        child.of(b).write("work");
    }));
    const entries = logs(s, b);
    assert.equal(entries.length, 3);
    assert.equal(entries[0].scopeType, "open");
    assert.equal(entries[1].parentScopeId, entries[0].scopeId);
    assert.equal(entries[0].interactionId, logs(s, a)[0].interactionId);
});

test("callback helpers preserve values and original errors while always closing", async () => {
    const s = session();
    const subject = {};
    const b = s.of(subject);
    assert.equal(b.runScope("sync", () => 42), 42);
    assert.equal(await b.runScope("async", async () => 43), 43);
    const error = new Error("expected");
    assert.throws(() => b.runScope("throw", () => { throw error; }), value => value === error);
    await assert.rejects(b.runScope("reject", async () => { throw error; }), value => value === error);
    assert.equal(logs(s, subject).filter(log => log.scopeType === "close").length, 4);
});

test("parallel callback scopes on the same object have independent parent chains", async () => {
    const s = session();
    const subject = {};
    const b = s.of(subject);
    let resumeA;
    const aWait = new Promise(resolve => { resumeA = resolve; });
    const a = b.runScope("A", async context => {
        await aWait;
        context.of(subject).write("A work");
    });
    const c = b.runScope("B", async context => {
        await tick();
        context.of(subject).write("B work");
    });
    await c; resumeA(); await a;
    const entries = logs(s, subject);
    const aOpen = entries.find(log => log.message === "A");
    const bOpen = entries.find(log => log.message === "B");
    assert.equal(aOpen.parentScopeId, null);
    assert.equal(bOpen.parentScopeId, null);
    assert.equal(entries.find(log => log.message === "A work").parentScopeId, aOpen.scopeId);
    assert.equal(entries.find(log => log.message === "B work").parentScopeId, bOpen.scopeId);
    assert.notEqual(aOpen.contextId, bOpen.contextId);
});

test("session diagnostics return copies and exclude the default session", async () => {
    assert.equal(B.activeSessionCount, 0);
    const first = session();
    const second = session();
    const keys = B.getActiveSessionKeys();
    assert.deepEqual(keys, [first.sessionKey, second.sessionKey]);
    keys.pop();
    assert.equal(B.activeSessionCount, 2);
    first.dispose(); first.dispose();
    assert.equal(B.activeSessionCount, 1);
    B.disposeActiveSessions();
    assert.equal(second.isDisposed, true);
    await assert.rejects(B.runSession(async () => { throw new Error("reject"); }));
    assert.equal(B.activeSessionCount, 0);
});

test("explicit contexts support nested settings inheritance without browser ambient mutation", async () => {
    const originalKey = B.currentSessionKey;
    const outer = session({ maxLogCount: 7 });
    await outer.run(async context => {
        assert.equal(B.currentSessionKey, originalKey);
        assert.equal(context.sessionKey, outer.sessionKey);
        await context.runSession(async child => {
            assert.equal(child.session.settings.maxLogCount, 7);
            child.configure({ maxLogCount: 3 });
            assert.equal(outer.settings.maxLogCount, 7);
        });
    });
    assert.equal(B.activeSessionCount, 1);
});

test("retained handles do not rebind after default restart or session disposal", () => {
    const original = B.of({});
    original.write("old");
    const oldKey = B.defaultSessionKey;
    const explicit = session();
    B.restartDefaultSession();
    assert.notEqual(B.defaultSessionKey, oldKey);
    assert.equal(original.isValid, false);
    original.write("lost");
    assert.match(original.export({ format: "text" }).content, /old/);
    assert.doesNotMatch(original.export({ format: "text" }).content, /lost/);
    assert.equal(explicit.isDisposed, false);
    const b = explicit.of({});
    b.write("retained");
    explicit.dispose();
    assert.match(b.export().content, /retained/);
});

test("delayed work retains its disposed explicit context and cannot pollute the default", async () => {
    let captured;
    const subject = {};
    await B.runSession(async context => { captured = context; context.of(subject).write("before"); });
    captured.of(subject).write("after");
    assert.equal(captured.session.isDisposed, true);
    const current = B.of(subject);
    assert.doesNotMatch(current.export({ format: "text" }).content, /before|after/);
});

test("bounded ring overwrites old entries and stale tag handles cannot tag new records", () => {
    const s = session({ maxLogCount: 2 });
    const subject = {};
    const b = s.of(subject);
    const stale = b.write("first");
    b.write("second"); b.write("third");
    assert.equal(stale.with({}), "first");
    assert.deepEqual(logs(s, subject).map(log => log.message), ["second", "third"]);
    assert.equal(s.of({}).id, 1);
});

test("export graph supports incoming/full traversal, cycles, depth and a stable cutoff", () => {
    const s = session();
    const a = {}, b = {}, c = {};
    s.of(a).exertMessage(b, "a to b");
    s.of(b).exertMessage(c, "b to c");
    s.of(c).exertMessage(a, "c to a");
    assert.equal(snapshot(s, a).nodes.length, 3);
    assert.equal(snapshot(s, a, { recursionDepth: 0 }).nodes.length, 1);
    assert.equal(snapshot(s, b, { recursionDepth: 1, fullExport: false }).nodes[1].id, s.of(a).id);
    let output;
    s.configure({ onExport: artifact => { output = artifact; s.of(a).write("after snapshot"); } });
    s.of(a).export();
    assert.doesNotMatch(output.content, /after snapshot/);
    assert.match(s.of(a).export().content, /after snapshot/);
});

test("normal export failure preserves recording, crash failure preserves freeze and retry data", () => {
    const s = session({ onExport: () => { throw new Error("sink failed"); } });
    const b = s.of({});
    b.write("before");
    assert.throws(() => b.export(), /sink failed/);
    assert.equal(b.isValid, true);
    assert.throws(() => b.crashExport(new Error("crash")), /sink failed/);
    assert.equal(s.isFrozen, true);
    assert.equal(b.isValid, false);
    assert.equal(b.crashExport("second"), "second");
    s.configure({ onExport: undefined });
    const retry = b.export();
    assert.match(retry.content, /crash/);
    assert.match(retry.content, /before/);
    assert.doesNotMatch(retry.content, /second/);
});

test("writeError supports target labels and configured crash export", () => {
    const artifacts = [];
    const s = session({ onExport: artifact => artifacts.push(artifact) });
    const a = {}, b = {};
    s.of(b, { label: "peer" });
    const handle = s.of(a);
    assert.equal(handle.writeError("oops", { targets: [{ context: "worker", target: b }] }), "oops");
    assert.match(handle.export().content, /worker: .*peer/);
    assert.equal(handle.writeError("stop", { exceptionHandling: "crashExport" }), "stop");
    assert.equal(s.isFrozen, true);
    assert.equal(artifacts.length, 2);
});

test("warning callbacks can reenter the same failure without infinite recursion", () => {
    let warnings = 0;
    const s = session({ warningLogger: () => { warnings++; handle.write("reentrant"); } });
    const handle = s.of({});
    s.dispose();
    handle.write("disposed");
    handle.write("disposed again");
    assert.equal(warnings, 1);
});

test("disposed/frozen scopes and Exerts can be cleaned safely", () => {
    const s = session();
    const a = {}, b = {};
    const handle = s.of(a);
    const scope = handle.scope("open");
    const exert = handle.exert(b, "call");
    handle.crashExport("freeze");
    scope.dispose(); exert.dispose();
    assert.equal(scope.isDisposed, true);
    assert.equal(exert.isDisposed, true);
    const result = handle.export().content;
    scope.dispose(); exert.dispose();
    assert.equal(handle.export().content, result);
});

test("explicit disposal symbol exists on lifetime handles when supported", () => {
    if (typeof Symbol.dispose !== "symbol") return;
    const s = session();
    const scope = s.of({}).scope("scope");
    scope[Symbol.dispose]();
    assert.equal(scope.isDisposed, true);
    s[Symbol.dispose]();
    assert.equal(s.isDisposed, true);
});

test("invalid configuration is rejected before replacing settings", () => {
    const s = session();
    assert.throws(() => s.configure({ maxLogCount: 0 }), RangeError);
    assert.throws(() => s.configure({ maxLogCount: 1.5 }), RangeError);
    assert.throws(() => s.configure({ exportFormat: "unknown" }), TypeError);
    assert.equal(s.settings.maxLogCount, 100);
    // Ring buffers are arrays, so lengths above the array limit fail during configuration, not lookup.
    assert.throws(() => s.configure({ maxLogCount: 2 ** 32 }), RangeError);
    assert.throws(() => session({ maxLogCount: 2 ** 32 }), RangeError);
    const largest = session({ maxLogCount: 2 ** 32 - 1 });
    assert.equal(largest.of({}).isValid, true);
});

test("TargetTypes is accepted only as the final tag argument", () => {
    const s = session();
    const subject = {}, target = {};
    const b = s.of(subject);
    assert.throws(() => b.write("value %0").with(TargetTypes.Name, target), /TargetTypes must be the last/);
    assert.equal(s.of({}).id, b.id + 1, "Rejected tags must not register targets");
    assert.match(b.write("value %0").with(target, TargetTypes.Name), /value '/);
});

test("weak ownership permits collection while retained handles still export; strong ownership retains", async () => {
    const moduleUrl = new URL("../dist/core.js", import.meta.url).href;
    const script = `
        import assert from 'node:assert/strict';
        import { setImmediate } from 'node:timers/promises';
        import { BlackboxHandle as B } from ${JSON.stringify(moduleUrl)};
        assert.equal(typeof globalThis.gc, 'function');
        function prepare(strongReference) {
            const session = B.createSession({ strongReference, exportFormat: 'text' });
            const subject = { value: 'temporary owner' };
            const handle = session.of(subject, { label: strongReference ? 'strong' : 'weak' });
            handle.write('retained log');
            return { session, handle };
        }
        const weak = prepare(false);
        const strong = prepare(true);
        let collected = false;
        for (let attempt = 0; attempt < 64; attempt++) {
            // A new job ends WeakRef's keep-alive interval before each collection.
            await setImmediate();
            globalThis.gc();
            await setImmediate();
            if (weak.handle.owner === undefined) { collected = true; break; }
        }
        assert.equal(collected, true, 'weak owner should collect within bounded forced-GC attempts');
        assert.ok(strong.handle.owner);
        assert.equal(weak.handle.ownerString, 'weak');
        assert.match(weak.handle.export().content, /retained log/);
        assert.match(strong.handle.export().content, /retained log/);
        // Disposal ends recording; it does not promise immediate object collection.
        weak.session.dispose();
        strong.session.dispose();
        assert.equal(weak.handle.isValid, false);
        assert.equal(strong.handle.isValid, false);
        assert.match(weak.handle.export().content, /retained log/);
        process.stdout.write('lifetime verified');
    `;
    const { stdout } = await promisify(execFile)(process.execPath,
        ["--expose-gc", "--input-type=module", "-e", script], { timeout: 15000 });
    assert.equal(stdout, "lifetime verified");
});
