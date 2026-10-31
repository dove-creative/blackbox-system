/** @internal Runs cleanup after a synchronous result or after a returned thenable settles. */
export function finish<T>(callback: () => T, cleanup: () => void): T {
    let result: T;
    try {
        result = callback();
        if (isThenable(result)) {
            return Promise.resolve(result).then(
                value => { cleanup(); return value; },
                error => { try { cleanup(); } catch { /* Preserve the callback failure. */ } throw error; },
            ) as T;
        }
    } catch (error) {
        try { cleanup(); } catch { /* Preserve the callback failure. */ }
        throw error;
    }
    cleanup();
    return result;
}

function isThenable(value: unknown): value is PromiseLike<unknown> {
    return value !== null && (typeof value === "object" || typeof value === "function")
        && typeof (value as { then?: unknown }).then === "function";
}

/** @internal Explicit disposal remains available when the host does not implement Symbol.dispose. */
export function supportDispose(type: { prototype: { dispose(): void } }): void {
    if (typeof Symbol.dispose === "symbol")
        Object.defineProperty(type.prototype, Symbol.dispose, { value: type.prototype.dispose, configurable: true });
}
