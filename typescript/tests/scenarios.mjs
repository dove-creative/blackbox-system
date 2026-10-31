// Runtime-neutral scenarios, executed in Node and in real browsers.
export async function runScenarios(api, ambient) {
    const { BlackboxHandle: B, TargetTypes } = api;
    let checks = 0;
    const check = (condition, message) => {
        if (!condition) throw new Error(message);
        checks++;
    };
    const session = B.createSession({ exportFormat: "text", maxLogCount: 40 });
    try {
        const source = { toString() { throw new Error("Subject formatting must not execute"); } };
        const target = {};
        const first = session.of(source, { label: "source-label" });
        const second = session.of(target, { label: "target-label" });
        check(first.id === session.of(source).id, "Same subject must retain identity");
        first.write("marker:start");
        const tagged = first.write("tag %0 and %1").with(target, null, TargetTypes.Full);
        check(tagged.includes("target-label") && tagged.includes("null"), "Tag placeholders must resolve");
        first.exertMessage(target, "marker:interaction");
        second.write("marker:target");
        const full = first.export({ format: "text", fullExport: true });
        check(full.content.includes("marker:target"), "Full graph must follow outgoing interactions");
        const focused = first.export({ format: "text", fullExport: false });
        check(!focused.content.includes("marker:target"), "Focused graph must not follow outgoing interactions");
        const parent = first.scope("marker:parent");
        const child = first.scope("marker:child");
        parent.dispose();
        child.dispose();
        check(parent.isDisposed && child.isDisposed, "Out-of-order close must finish nested scopes");
        const scopeOutput = first.export().content;
        const targetOutput = second.export().content;
        const scopeSequence = session.currentSequence;
        const ignoredTarget = {};
        check(parent.with(ignoredTarget) === parent && child.with(target) === child,
            "Closed scope tagging must preserve fluent return values");
        check(session.currentSequence === scopeSequence && first.export().content === scopeOutput
            && second.export().content === targetOutput, "Closed scopes must not change records or tags");
        check(session.of({}).id === second.id + 1, "Closed scopes must not register tag targets");
        await first.runScope("marker:async", async context => {
            await Promise.resolve();
            context.of(source).write("marker:after-await");
        });
        check(first.export().content.includes("marker:after-await"), "Bound async context must record after await");
        first.write("<script>globalThis.injected=true</script>");
        const html = first.export({ format: "html" });
        check(html.content.includes("&lt;script&gt;"), "HTML must escape user messages");
        check(!html.content.includes("<script>globalThis.injected=true</script>"), "HTML must not inject scripts");
        session.dispose();
        first.write("marker:must-not-write");
        check(!first.export().content.includes("marker:must-not-write"), "Disposed session rejects writes but preserves exports");
    } finally {
        session.dispose();
    }

    const subject = {};
    const keys = await Promise.all(["left", "right"].map(label => B.runSession(async context => {
        const handle = context.of(subject, { label });
        await Promise.resolve();
        check(ambient ? B.of(subject).id === handle.id && B.currentSessionKey === handle.sessionKey
            : B.currentSessionKey !== handle.sessionKey, "Ambient session behavior must match runtime");
        context.of(subject).write(`isolated:${label}`);
        const output = handle.export({ format: "text" }).content;
        check(output.includes(`isolated:${label}`), "Own session record missing");
        check(!output.includes(`isolated:${label === "left" ? "right" : "left"}`), "Parallel sessions must be isolated");
        return handle.sessionKey;
    })));
    check(keys[0] !== keys[1], "Parallel sessions must have different keys");
    check(B.activeSessionCount === 0, "All explicit sessions must be disposed");

    const bounded = B.createSession({ maxLogCount: 3 });
    try {
        const handle = bounded.of({});
        for (let i = 0; i < 8; i++) handle.write(`bounded:${i}`);
        const output = handle.export({ format: "text" }).content;
        check(!output.includes("bounded:4") && output.includes("bounded:5") && output.includes("bounded:7"), "Buffer must retain only the newest records");
    } finally { bounded.dispose(); }
    return checks;
}
