import { copyFile } from "node:fs/promises";

await copyFile(new URL("../../LICENSE.md", import.meta.url), new URL("../dist/LICENSE.md", import.meta.url));
