import assert from "node:assert/strict";
import { afterEach, test } from "node:test";
import { BlackboxHandle as B } from "../dist/node.js";

afterEach(() => {
    B.disposeActiveSessions();
    B.restartDefaultSession();
});

test("ordinary handles follow sibling ambient contexts within their original session", async () => {
    await B.runSession(async root => {
        const subject = {};
        let stored;
        await root.run(async () => { stored = B.of(subject); });
        await root.run(async () => {
            await stored.runScope("sibling scope", async () => {
                await Promise.resolve();
                stored.write("inside sibling");
            });
        });
        const snapshot = root.session.snapshot(root.session.getBox(subject), {});
        const entries = snapshot.nodes[0].logs;
        assert.equal(entries[1].parentScopeId, entries[0].scopeId);
        assert.equal(entries[1].contextId, entries[0].contextId);
    });
});

test("explicit context handles remain bound even when another ambient branch is active", async () => {
    await B.runSession(async root => {
        const subject = {};
        const explicit = root.of(subject);
        await root.run(async other => {
            other.of(subject).scope("other branch").dispose();
            explicit.write("explicit root");
        });
        const entries = root.session.snapshot(root.session.getBox(subject), {}).nodes[0].logs;
        assert.equal(entries.at(-1).contextId, root.id);
        assert.equal(entries.at(-1).parentScopeId, null);
    });
});

test("ordinary retained handles never migrate into a different ambient session", async () => {
    const subject = {};
    const defaultHandle = B.of(subject);
    const defaultKey = defaultHandle.sessionKey;
    await B.runSession(async () => {
        defaultHandle.write("default only");
        const explicit = B.of(subject);
        assert.notEqual(explicit.sessionKey, defaultKey);
        assert.doesNotMatch(explicit.export({ format: "text" }).content, /default only/);
    });
    assert.match(defaultHandle.export({ format: "text" }).content, /default only/);
});

test("callback helpers on a handle from another session run in that session's ambient branch", async () => {
    const service = {}, repository = {};
    const cached = B.of(service);
    await B.runSession(async root => {
        await cached.runScope("handle", async () => {
            await Promise.resolve();
            assert.equal(B.currentSessionKey, B.defaultSessionKey);
            assert.equal(B.of(repository).sessionKey, B.defaultSessionKey);
        });
        await cached.runExert(repository, "query", async () => {
            assert.equal(B.currentSessionKey, B.defaultSessionKey);
        });
        assert.equal(B.currentSessionKey, root.sessionKey);
    });
});

test("default restart never inherits optional settings from an explicit ambient session", async () => {
    let calls = 0;
    B.configure({ onExport: undefined, logDirectory: undefined, normalLogger: undefined, warningLogger: undefined });
    await B.runSession(async () => {
        B.restartDefaultSession();
    }, { onExport: () => calls++, logDirectory: "not-a-default-directory",
        normalLogger: () => calls++, warningLogger: () => calls++ });
    B.of({}).export();
    assert.equal(calls, 0);
    assert.equal(B.logDirectory, undefined);
    assert.equal(B.normalLogger, undefined);
    assert.equal(B.warningLogger, undefined);
});

test("explicit parent settings override unrelated ambient optional callbacks", async () => {
    const detached = B.createSession({ onExport: undefined, logDirectory: undefined });
    let calls = 0;
    await B.runSession(async () => {
        const child = detached.context.createSession();
        child.of({}).export();
        child.dispose();
    }, { onExport: () => calls++ });
    assert.equal(calls, 0);
});
