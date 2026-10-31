import type { ExportFormat, TargetTypes } from "./types.js";

// Immutable export input. These types are internal and are not package exports.
export interface ExportTag {
    id: number;
    targetId: number | null;
    targetName: string;
}

export interface ExportLog {
    sequence: number;
    time: string;
    contextId: number;
    message: string;
    methodName: string;
    scopeType: "none" | "open" | "close" | "step";
    scopeId: number;
    parentScopeId: number | null;
    interactionId: number;
    exertingTo?: number;
    exertedBy?: number;
    taggedBy?: number;
    tagTargetTypes: TargetTypes;
    tags: readonly ExportTag[];
}

export interface ExportNode {
    id: number;
    name: string;
    logs: readonly ExportLog[];
    parentId: number | null;
    depth: number;
}

export interface ExportSnapshot {
    sessionKey: string;
    rootId: number;
    maxSequence: number;
    nodes: readonly ExportNode[];
    names: Readonly<Record<number, string>>;
    crash?: { message: string; stack: string };
}

export type RenderExport = (snapshot: ExportSnapshot, format: ExportFormat) => string;
