import { execFileSync } from "node:child_process";
import { mkdirSync, mkdtempSync, writeFileSync, readFileSync } from "node:fs";
import { resolve, join } from "node:path";
import { fileURLToPath } from "node:url";
import assert from "node:assert/strict";

const packageRoot = fileURLToPath(new URL("../", import.meta.url));
const artifactRoot = resolve(packageRoot, "../artifacts/ts-package");
mkdirSync(artifactRoot, { recursive: true });
const consumer = mkdtempSync(join(artifactRoot, "consumer-"));
const npmCli = process.env.npm_execpath;
if (!npmCli) throw new Error("Run through npm run test:package.");
const npm = (args, cwd) => execFileSync(process.execPath, [npmCli, ...args], { cwd, encoding: "utf8" });
const [packed] = JSON.parse(npm(["pack", "--ignore-scripts", "--json", "--pack-destination", artifactRoot], packageRoot));
assert.ok(packed.files.some(file => file.path === "dist/LICENSE.md"));
assert.ok(!packed.files.some(file => file.path.startsWith("tests/")));
writeFileSync(join(consumer, "package.json"), JSON.stringify({ private: true, type: "module" }));
npm(["install", "--ignore-scripts", "--offline", "--no-audit", "--no-fund", join(artifactRoot, packed.filename)], consumer);
writeFileSync(join(consumer, "check.mjs"), `import { BlackboxHandle } from '@blackthunder/blackbox-system';
const handle = BlackboxHandle.of({});
handle.write('package-marker');
if (!handle.export({ format: 'text' }).content.includes('package-marker')) throw new Error('package import failed');
`);
execFileSync(process.execPath, ["check.mjs"], { cwd: consumer, stdio: "inherit" });
writeFileSync(join(consumer, "check.ts"), `import { BlackboxHandle, TargetTypes, type ExportArtifact } from '@blackthunder/blackbox-system/browser';
const artifact: ExportArtifact | undefined = BlackboxHandle.of({}).export();
console.log(artifact?.content);
const tag = BlackboxHandle.of({}).write('tag %0');
tag.with({}, null, TargetTypes.Name);
// @ts-expect-error TargetTypes must be the final tag argument.
tag.with(TargetTypes.Name, {});
`);
writeFileSync(join(consumer, "tsconfig.json"), JSON.stringify({ compilerOptions: {
    strict: true, target: "ES2022", module: "NodeNext", moduleResolution: "NodeNext", types: [],
    lib: ["ES2022", "DOM"], noEmit: true,
}, include: ["check.ts"] }));
execFileSync(process.execPath, [join(packageRoot, "node_modules/typescript/bin/tsc"), "-p", "tsconfig.json"], { cwd: consumer, stdio: "inherit" });
const manifest = JSON.parse(readFileSync(join(consumer, "node_modules/@blackthunder/blackbox-system/package.json"), "utf8"));
assert.equal(manifest.dependencies, undefined);
console.log(`Packed consumer import and browser declarations verified: ${packed.filename}`);
