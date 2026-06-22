// BlazorFlow JS interop module.
// Kept intentionally small: Blazor handles pointer/wheel events natively,
// JS is only used to measure DOM rects that C# cannot compute on its own.

export function getRect(el) {
    if (!el) return null;
    const r = el.getBoundingClientRect();
    return { left: r.left, top: r.top, width: r.width, height: r.height };
}

// Measures every node and its handles inside the (transformed) viewport element.
// Sizes/offsets are divided by the current zoom so they are expressed in flow units.
export function measureNodes(viewportEl, zoom) {
    const result = [];
    if (!viewportEl) return result;
    const z = zoom || 1;

    viewportEl.querySelectorAll('.blazorflow__node').forEach(nodeEl => {
        const rect = nodeEl.getBoundingClientRect();
        const id = nodeEl.getAttribute('data-id');
        const handles = [];

        nodeEl.querySelectorAll('.blazorflow__handle').forEach(h => {
            const hr = h.getBoundingClientRect();
            const cx = hr.left + hr.width / 2;
            const cy = hr.top + hr.height / 2;
            handles.push({
                handleId: h.getAttribute('data-handle-id') || '',
                type: h.getAttribute('data-handle-type') || 'source',
                position: h.getAttribute('data-handle-pos') || 'bottom',
                offsetX: (cx - rect.left) / z,
                offsetY: (cy - rect.top) / z
            });
        });

        result.push({
            id,
            width: rect.width / z,
            height: rect.height / z,
            handles
        });
    });

    return result;
}

// Returns the element under the given client point that matches `selector`,
// walking up from the topmost element. Used to resolve connection drop targets.
export function elementDataAtPoint(x, y, selector) {
    let el = document.elementFromPoint(x, y);
    while (el) {
        if (el.matches && el.matches(selector)) {
            return {
                nodeId: el.getAttribute('data-id'),
                handleId: el.getAttribute('data-handle-id') || '',
                handleType: el.getAttribute('data-handle-type') || '',
                position: el.getAttribute('data-handle-pos') || ''
            };
        }
        el = el.parentElement;
    }
    return null;
}

// Sets pointer capture so dragging continues even when the cursor leaves the element.
export function setPointerCapture(el, pointerId) {
    try { el?.setPointerCapture(pointerId); } catch { /* ignore */ }
}
