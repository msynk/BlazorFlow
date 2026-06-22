using Microsoft.JSInterop;

namespace BlazorFlow.Internal;

/// <summary>
/// Thin wrapper over the <c>blazorFlow.js</c> module.
/// </summary>
internal sealed class FlowInterop : IAsyncDisposable
{
    private readonly Lazy<Task<IJSObjectReference>> _moduleTask;

    public FlowInterop(IJSRuntime js)
    {
        _moduleTask = new(() => js.InvokeAsync<IJSObjectReference>(
            "import", "./_content/BlazorFlow/blazorFlow.js").AsTask());
    }

    public async ValueTask<DomRect?> GetRectAsync(ElementReferenceLike el)
        => await (await _moduleTask.Value).InvokeAsync<DomRect?>("getRect", el.Reference);

    public async ValueTask<MeasuredNode[]> MeasureNodesAsync(ElementReferenceLike viewport, double zoom)
        => await (await _moduleTask.Value).InvokeAsync<MeasuredNode[]>("measureNodes", viewport.Reference, zoom)
           ?? [];

    public async ValueTask<HandleHit?> ElementDataAtPointAsync(double x, double y, string selector)
        => await (await _moduleTask.Value).InvokeAsync<HandleHit?>("elementDataAtPoint", x, y, selector);

    public async ValueTask SetPointerCaptureAsync(object element, long pointerId)
        => await (await _moduleTask.Value).InvokeVoidAsync("setPointerCapture", element, pointerId);

    public async ValueTask DisposeAsync()
    {
        if (_moduleTask.IsValueCreated)
        {
            var module = await _moduleTask.Value;
            await module.DisposeAsync();
        }
    }
}

/// <summary>Wrapper so we can pass an ElementReference without leaking the type around.</summary>
internal readonly record struct ElementReferenceLike(object Reference);

public sealed record DomRect(double Left, double Top, double Width, double Height);

public sealed record MeasuredNode(string Id, double Width, double Height, MeasuredHandle[] Handles);

public sealed record MeasuredHandle(string HandleId, string Type, string Position, double OffsetX, double OffsetY);

public sealed record HandleHit(string? NodeId, string? HandleId, string? HandleType, string? Position);
