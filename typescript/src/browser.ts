import type { ExportArtifact } from "./types.js";

export * from "./public.js";

/** Starts a browser download. The browser controls the destination and completion. */
export function downloadExport(artifact: ExportArtifact): void {
    const url = URL.createObjectURL(new Blob([artifact.content], { type: artifact.mimeType }));
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = artifact.fileName;
    anchor.hidden = true;
    try {
        document.body.append(anchor);
        anchor.click();
    } finally {
        anchor.remove();
        // Keep the URL alive until the browser has consumed the navigation.
        setTimeout(() => URL.revokeObjectURL(url), 1000);
    }
}
