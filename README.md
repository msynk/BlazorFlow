# BlazorFlow

A native Blazor library for building node-based UIs, editors, flow charts and diagrams —
a Blazor counterpart to [React Flow](https://reactflow.dev) ([xyflow](https://github.com/xyflow/xyflow)).

It is written in idiomatic C#/Razor. Pointer and wheel interactions are handled with Blazor's
native event system; a tiny JS module (`blazorFlow.js`) is used only to measure DOM rectangles
that cannot be computed in C#.

## Features

- **Pannable / zoomable viewport** (drag to pan, scroll to zoom toward the cursor)
- **Draggable nodes** with single and multi-selection (Shift to add, Delete to remove)
- **Connectable handles** — drag from a handle to another node to create an edge
- **Built-in node types**: `input`, `output`, `default`, plus fully custom node templates
- **Built-in edge paths**: `Bezier`, `Straight`, `Step`, `SmoothStep`, with labels, arrows and animation
- **Sub-flows / grouping**: nodes can have a `ParentId`; children move with their parent
- **Node resizing**: drop a `<NodeResizer>` into a node to get corner/edge resize controls
- **Node toolbars**: `<NodeToolbar>` renders constant-size controls anchored to a node
- **Connection validation**: pass `IsValidConnection` to accept/reject connections live
- **Add-ons**: `<Background>` (dots / lines / cross), `<Controls>`, `<MiniMap>`, `<Panel>`
- **Two-way friendly**: mutates your node/edge lists in place and raises change callbacks

## Project layout

```
src/BlazorFlow            # the component library (Razor Class Library)
src/BlazorFlow.Demo       # a Blazor WebAssembly demo app
src/BlazorFlow.slnx       # the solution file
```

## Quick start

```razor
@using BlazorFlow
@using BlazorFlow.Models

<div style="height: 600px">
    <FlowCanvas Nodes="_nodes" Edges="_edges" FitViewOnInit="true" OnConnect="OnConnect">
        <Background Variant="BackgroundVariant.Dots" />
        <Controls />
        <MiniMap />
    </FlowCanvas>
</div>

@code {
    List<Node> _nodes =
    [
        new Node { Id = "1", Type = "input",  Label = "Input",  Position = new(250, 25) },
        new Node { Id = "2",                  Label = "Node",   Position = new(150, 150) },
        new Node { Id = "3", Type = "output", Label = "Output", Position = new(280, 300) },
    ];

    List<Edge> _edges =
    [
        new Edge { Id = "e1-2", Source = "1", Target = "2", Animated = true },
        new Edge { Id = "e2-3", Source = "2", Target = "3", Label = "edge" },
    ];

    void OnConnect(Connection c) { /* the edge is added automatically; react to it here */ }
}
```

Add the stylesheet to your host page (`index.html` / `App.razor`):

```html
<link rel="stylesheet" href="_content/BlazorFlow/blazorflow.css" />
```

## Custom nodes

Provide a `NodeTemplate` and place `<Handle>` components anywhere inside it:

```razor
<FlowCanvas Nodes="_nodes" Edges="_edges">
    <NodeTemplate Context="node">
        <Handle Type="HandleType.Target" Position="Position.Left" />
        <div class="my-node">@node.Label</div>
        <Handle Type="HandleType.Source" Id="a" Position="Position.Right" />
    </NodeTemplate>
    <ChildContent>
        <Background />
        <Controls />
    </ChildContent>
</FlowCanvas>
```

## Sub-flows, resizing & toolbars

Group nodes by giving children a `ParentId` (their `Position` becomes relative to the
parent). Add resize controls and a toolbar inside a custom node:

```razor
<NodeTemplate Context="node">
    <NodeResizer MinWidth="100" MinHeight="50" />
    <NodeToolbar Position="Position.Top">
        <button @onclick="() => Delete(node)">Delete</button>
    </NodeToolbar>
    <Handle Type="HandleType.Target" Position="Position.Left" />
    <div>@node.Label</div>
    <Handle Type="HandleType.Source" Position="Position.Right" />
</NodeTemplate>
```

Validate connections as the user drags (rejected handles are highlighted):

```razor
<FlowCanvas Nodes="_nodes" Edges="_edges"
            IsValidConnection="c => c.Target == \"sink\"" />
```

## Running the demo

```bash
dotnet run --project src/BlazorFlow.Demo
```

Then open the printed `http://localhost:****` URL. The demo includes Basic Flow,
Custom Nodes, Edge Types, and Groups &amp; Resizing pages.

## Theming

Override the CSS custom properties on the `.blazorflow` element, e.g.:

```css
.blazorflow {
    --bf-node-selected: #2563eb;
    --bf-edge-stroke: #b1b1b7;
}
```

## Architecture notes

- `FlowCanvas` owns interaction state and implements an internal `IFlowContext` that is
  cascaded to descendants (handles, background, controls, minimap).
- The viewport is a single transformed layer (`translate(x,y) scale(zoom)`); nodes are
  absolutely positioned inside it and edges are SVG paths in the same coordinate space.
- Edge endpoints are derived from measured handle offsets so custom handle layouts
  connect accurately. Path geometry lives in `Geometry/EdgePath.cs` (ports of React
  Flow's bezier / straight / smoothstep algorithms).

## Status

This implementation covers React Flow's core feature set plus sub-flows, node
resizing, node toolbars and connection validation. Possible next steps: connection
snapping/edge re-routing, viewport pan/zoom transitions, keyboard accessibility, and
serialization helpers.
