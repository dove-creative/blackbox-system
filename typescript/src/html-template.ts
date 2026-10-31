import { reportScript } from "./report-script.js";

export const htmlDocumentStart = [
    "<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">",
    '<meta name="viewport" content="width=device-width, initial-scale=1">',
    "<title>Blackbox Log Report</title><style>",
    ":root{color-scheme:dark;--bg:#1e1e1e;--fg:#d4d4d4;--accent:#4ec9b0;--border:#444}",
    "*{box-sizing:border-box}body{margin:0;padding:24px;background:var(--bg);color:var(--fg);font:13px/1.6 Consolas,Monaco,monospace}",
    "h1{margin:0;font-size:23px}h2{font-size:15px;margin:0}.report-header{margin-bottom:24px}.report-header p{color:#aaa;overflow-wrap:anywhere}",
    "button{font:inherit;color:inherit;background:#333;border:1px solid #666;border-radius:4px;padding:4px 10px;cursor:pointer}",
    "button:focus-visible,a:focus-visible{outline:2px solid #79caff;outline-offset:3px}nav{display:flex;gap:8px}",
    ".box{border:1px solid var(--border);border-radius:6px;background:#252526;margin-bottom:28px;overflow:auto}",
    ".header{padding:10px 15px;background:#333;position:sticky;left:0;top:0;z-index:1}.id-badge{background:#007acc;color:white;border-radius:4px;padding:2px 6px}",
    ".parent{font-size:12px;color:#aaa;font-weight:normal}.parent a{color:#aaa}.log-container{padding:10px 0}.log-row{display:flex;align-items:flex-start;min-width:min-content;scroll-margin-block:80px}",
    ".log-row:hover{background:#303033}.log-row[hidden]{display:none}.time{flex:0 0 320px;color:#999;padding-left:15px;white-space:nowrap;font-size:11px;user-select:text}",
    ".content{flex:1;min-width:280px;padding-right:15px;white-space:pre-wrap;overflow-wrap:anywhere}.method{font-weight:bold;color:#ffd700}",
    ".scope-toggle{display:inline-block;padding:0 4px;margin-right:5px;line-height:1.3;border:0;color:#ffd700;background:transparent}",
    ".folded .message:after{content:' …';color:#ffd700}.interaction{display:inline-block;color:var(--accent);background:#1e3a3a;border:1px solid #2b5656;border-radius:3px;font-size:12px;line-height:1.5;padding:0 4px;text-decoration:none;margin-left:5px;white-space:normal}",
    ".interaction:hover{border-color:var(--accent)}.tag-interaction{color:#f6a04d;background:#3b2818;border-color:#704316}.tag-interaction:hover{border-color:#f6a04d}",
    ".interaction.disabled{opacity:.65;cursor:default;color:#aaa;background:#303030;border-color:#555}.tag-interaction.disabled{color:#b98954}",
    ".crash{border:1px solid #d16969;border-radius:6px;padding:14px;margin-bottom:24px;background:#382525}.crash pre{white-space:pre-wrap;overflow-wrap:anywhere;margin-bottom:0}",
    ".highlight{animation:highlight 1.4s ease-out}@keyframes highlight{from{background:#807020}to{background:transparent}}",
    "@media(prefers-reduced-motion:reduce){.highlight{animation:none;outline:2px solid #ffd700}html{scroll-behavior:auto}}",
    "@media(max-width:700px){body{padding:12px}.time{flex-basis:160px;white-space:normal}.content{min-width:180px}}",
    "</style></head><body>",
].join("\n");

export const htmlDocumentEnd = `<script>${reportScript}</script></body></html>`;
