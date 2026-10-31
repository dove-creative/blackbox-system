import { rmSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const packageRoot = fileURLToPath(new URL("../", import.meta.url));
const output = resolve(packageRoot, "dist");
if (dirname(output) !== resolve(packageRoot)) throw new Error("Unexpected build output directory.");
rmSync(output, { recursive: true, force: true });
