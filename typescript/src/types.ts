export type Message = string | (() => string);
export type ExportFormat = "html" | "text";
export type Subject = object;

export enum TargetTypes {
    None = 0,
    Name = 1,
    InteractionId = 2,
    Message = 4,
    Full = 7,
    BasedOnSettings = 8,
}

export interface ExportArtifact {
    content: string;
    fileName: string;
    mimeType: string;
}

export interface BlackboxOptions {
    useBlackbox?: boolean;
    maxLogCount?: number;
    strongReference?: boolean;
    defaultRecursionDepth?: number;
    exportFormat?: ExportFormat;
    fullExport?: boolean;
    openLog?: boolean;
    exceptionHandling?: "none" | "crashExport";
    tagTargetTypes?: TargetTypes;
    normalLogger?: ((message: string) => void) | undefined;
    warningLogger?: ((message: string) => void) | undefined;
    onExport?: ((artifact: ExportArtifact) => void) | undefined;
    logDirectory?: string | undefined;
}

export interface OfOptions {
    label?: string;
}

export interface RecordOptions {
    methodName?: string;
}

export interface ExportOptions {
    recursionDepth?: number;
    format?: ExportFormat;
    fullExport?: boolean;
    openLog?: boolean;
}

export interface ErrorTarget {
    context: string;
    target: Subject | null;
}

export interface ErrorOptions extends RecordOptions, ExportOptions {
    targets?: readonly ErrorTarget[];
    exceptionHandling?: "none" | "crashExport";
}
