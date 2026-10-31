import { createInterface } from "node:readline";
import { setTimeout as delay } from "node:timers/promises";
import { fileURLToPath } from "node:url";
import { BlackboxHandle, TargetTypes } from "../dist/node.js";

const outputDirectory = fileURLToPath(new URL("./output/", import.meta.url));
const sessionOptions = {
    logDirectory: outputDirectory,
    openLog: false,
    normalLogger: message => console.log(message),
    warningLogger: message => console.error(message),
};

const scenarios = {
    write: async () => {
        const loader = {};
        const trace = BlackboxHandle.of(loader, { label: "Loader" });
        const construction = trace.construct("Created");
        construction.dispose();
        await trace.runScope("Load", async () => {
            BlackboxHandle.of(loader).write("Preparing data");
            await delay(5);
            BlackboxHandle.of(loader).write("Data loaded");
        }, { methodName: "load" });
        trace.dispose("Sample complete");
        trace.export();
    },
    exert: async () => {
        const loader = {};
        const worker = {};
        const trace = BlackboxHandle.of(loader, { label: "Loader" });
        BlackboxHandle.of(worker, { label: "Worker" });
        await trace.runExert(worker, "Worker.process", async () => {
            await BlackboxHandle.of(worker).runScope("Process", async () => {
                await delay(5);
                BlackboxHandle.of(worker).write("Processing finished");
            }, { methodName: "process" });
        });
        trace.export();
    },
    tag: () => {
        const order = {};
        const customer = {};
        const trace = BlackboxHandle.of(order, { label: "Order" });
        BlackboxHandle.of(customer, { label: "Customer" });
        trace.write("Prepare %0; optional owner: %1").with(customer, null, TargetTypes.Full);
        trace.exertMessage(customer, "Notify customer");
        trace.export();
    },
    exception: () => {
        const loader = {};
        const worker = {};
        const trace = BlackboxHandle.of(loader, { label: "Loader" });
        BlackboxHandle.of(worker, { label: "Worker" });
        trace.write("About to run the deliberate failure");
        try {
            throw new Error("The sample operation failed deliberately.");
        } catch (error) {
            trace.writeError(error, {
                exceptionHandling: "crashExport",
                targets: [{ context: "worker", target: worker }],
                methodName: "load",
            });
            console.log("Expected failure recorded; the session is frozen.");
        }
    },
    async: async () => {
        const sharedSubject = {};
        const keys = await Promise.all(["left", "right"].map(label =>
            BlackboxHandle.runSession(async () => {
                const trace = BlackboxHandle.of(sharedSubject, { label });
                await trace.runScope("Independent operation", async () => {
                    await delay(label === "left" ? 10 : 5);
                    BlackboxHandle.of(sharedSubject).write("Finished " + label);
                });
                trace.export();
                return trace.sessionKey;
            }, sessionOptions),
        ));
        if (keys[0] === keys[1]) throw new Error("Parallel sample sessions were not isolated.");
        console.log("Parallel operations kept separate sessions across await.");
    },
};

function printHelp() {
    console.log("Commands: write, exert, tag, exception, async, all, help, exit");
    console.log("Exports: " + outputDirectory);
}

async function runCommand(command) {
    if (command === "help") { printHelp(); return 0; }
    if (command === "exit") return 0;
    const selected = command === "all" ? Object.keys(scenarios) : [command];
    if (selected.some(name => !Object.hasOwn(scenarios, name))) {
        console.error("Unknown command: " + command);
        printHelp();
        return 2;
    }
    for (const name of selected) {
        await BlackboxHandle.runSession(scenarios[name], sessionOptions);
        console.log("Completed: " + name);
    }
    return 0;
}

function reportFailure(error) {
    console.error("Sample failed: " + (error instanceof Error ? error.message : String(error)));
}

async function main() {
    const args = process.argv.slice(2);
    if (args.length > 1) {
        console.error("Supply one command without additional arguments.");
        return 2;
    }
    if (args.length === 1) return runCommand(args[0].toLowerCase());

    printHelp();
    const input = createInterface({ input: process.stdin, output: process.stdout });
    const lines = input[Symbol.asyncIterator]();
    let inputClosed = false;
    input.once("close", () => { inputClosed = true; });
    input.setPrompt("sample> ");
    input.prompt();
    try {
        for await (const line of lines) {
            const command = line.trim().toLowerCase();
            if (command === "exit") break;
            if (command) {
                try { await runCommand(command); }
                catch (error) { reportFailure(error); }
            }
            if (!inputClosed) input.prompt();
        }
    } finally {
        input.close();
    }
    return 0;
}

try {
    process.exitCode = await main();
} catch (error) {
    reportFailure(error);
    process.exitCode = 1;
}
