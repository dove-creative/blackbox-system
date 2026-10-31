import { rmSync, writeFileSync } from "node:fs";

// Freeze the compiled report script as a string literal so consumer transforms cannot rewrite its body.
const output = new URL("../dist/report-script.js", import.meta.url);
const { reportScript } = await import(output.href);
if (typeof reportScript !== "string" || /<\/script|<!--/i.test(reportScript))
    throw new Error("The report script must be a string without script-closing or comment sequences.");
writeFileSync(output, `export const reportScript = ${JSON.stringify(reportScript)};\n`);
rmSync(new URL("../dist/report-script.js.map", import.meta.url), { force: true });
