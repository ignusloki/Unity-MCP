# Unity MCP Tools Bulk Toggle Plan

## Goal

Add a bulk-selection control to the Unity-side `MCP Tools` window so users do not have to enable tool toggles one by one.

The current pain point is the `MCP Tools` list in the Unity Editor UI, where enabling a large number of tools manually is slow and repetitive.

## User Problem

Today, the `MCP Tools` window exposes one toggle per tool card.

That works for small lists, but it becomes tedious when:

- a project has many installed tool packs
- the user wants to enable almost everything
- the user wants to filter the list and enable only one category of tools

The request is to add a button for selecting all toggles. The best UX version of that request is to support bulk actions against the currently filtered list, not only the entire global list.

## Recommended UX

### Preferred V1

Add two buttons near the existing filter controls in the `MCP Tools` window:

- `Enable Filtered`
- `Disable Filtered`

Behavior:

- if no text/type filter is active, the buttons apply to all tools
- if the user has filtered the list, the buttons apply only to the currently filtered tools
- after the action, the list refreshes and the stats label updates
- the action saves once after the whole batch, not once per item

This covers the original “select all toggles” request, while also making filtered workflows much better.

### Why Not A Single Master Toggle

A single top-level toggle is less clear because:

- it is ambiguous whether it applies to all tools or only visible tools
- it is awkward with the current `Enabled` / `Disabled` filter
- it is harder to communicate a no-op state cleanly

Buttons are simpler, more explicit, and fit the existing UI better.

## Current Code Map

Main files involved:

- `Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/UI/Window/McpToolsWindow.cs`
  Tools-specific list window, item binding, and per-tool toggle handling.
- `Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/UI/Window/McpListWindowBase.cs`
  Shared list-window infrastructure, filters, ListView population, and common toggle binding.
- `Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/UI/uxml/McpToolsWindow.uxml`
  Current window layout for the tools list.
- `Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/UI/uss/McpToolsWindow.uss`
  Tools-window-specific styling.
- `Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/UI/uxml/ToolItem.uxml`
  Individual tool item layout.

Related windows that use the same base pattern:

- `.../McpPromptsWindow.cs`
- `.../McpResourcesWindow.cs`

These are relevant only because the list-window base is shared. The requested feature should stay scoped to tools first.

## Important Implementation Constraint

Do not implement bulk enablement by simulating many UI toggle clicks.

Reason:

- `OnItemToggleChanged()` currently saves on every toggle change
- the list uses a virtualized `ListView`
- repeated UI-driven updates would be noisy, slower, and harder to reason about

Instead, the bulk action should:

1. compute the current filtered tool set
2. update tool enabled state directly through the tool manager
3. update the view-model state in memory
4. save once
5. repopulate the list

## Proposed Technical Approach

### 1. Add a window-specific initialization hook

Add a protected hook to `McpListWindowBase<TViewModel>` so derived windows can wire extra controls without overriding the whole `CreateGUI()` flow.

Example direction:

- base class keeps loading UXML, wiring filters, populating the list, and subscribing to updates
- derived windows get an `InitializeWindowControls(root)` or similar method

This keeps the tools-specific enhancement isolated and reusable if prompts/resources ever need similar bulk actions later.

### 2. Add bulk-action buttons to the tools window layout

Update `McpToolsWindow.uxml` to include a compact action row near the existing filter controls.

Recommended controls:

- `btn-enable-filtered`
- `btn-disable-filtered`

Recommended placement:

- below the `Type` / filter-stats row
- above the `ListView`

### 3. Wire the buttons in `McpToolsWindow`

In `McpToolsWindow.cs`:

- query the two buttons
- register click handlers
- compute the current filtered tool view-models
- apply the new enabled state in batch
- save once
- call `PopulateList()`

The tools window already has access to:

- `allItems`
- `FilterItems()`
- `UnityMcpPluginEditor.Instance.Tools`

That should be enough for a clean implementation.

### 4. Keep the action scoped to tools only

Do not add bulk-toggle buttons to prompts or resources in the same change unless there is a strong reason.

Reason:

- the user asked for the tools workflow specifically
- the tools list is the painful case shown in the screenshot
- keeping scope narrow reduces review risk

## Suggested User-Facing Behavior

### `Enable Filtered`

- enables every currently filtered tool
- if everything filtered is already enabled, it becomes a no-op
- after completion, the list updates immediately

### `Disable Filtered`

- disables every currently filtered tool
- if everything filtered is already disabled, it becomes a no-op

### With Type Filter = `Disabled`

If the user clicks `Enable Filtered` while `Disabled` is selected:

- all visible disabled tools become enabled
- the current list may become empty afterward

That is acceptable as long as the stats and list refresh correctly.

## Optional Small UX Enhancements

These are good but not required for V1:

- show a small info label or notification like `Enabled 42 tools`
- disable the button when the filtered result count is zero
- add tooltips clarifying that the action applies to the current filtered set

## Out Of Scope For This Feature

- prompts/resources bulk toggle controls
- invert selection
- tri-state master toggle behavior
- persistence of bulk-action history
- redesign of the tools window layout

## Test Plan

### Manual Validation

1. Open `MCP Tools` in Unity.
2. With no filter applied, click `Enable Filtered`.
3. Confirm all tools become enabled.
4. Click `Disable Filtered`.
5. Confirm all tools become disabled.
6. Enter a text filter that narrows the list.
7. Click `Enable Filtered`.
8. Confirm only the filtered tools become enabled.
9. Switch `Type` to `Disabled` and verify the list updates correctly after enabling filtered items.
10. Reopen the window or reload domain and confirm the enabled state persisted.

### Automated Validation

Add editor tests around the bulk action logic. Prefer testing the state-change behavior, not just UI querying.

Minimum cases:

- bulk enable all filtered tools
- bulk disable all filtered tools
- no-op when filtered set is empty
- filtered-only behavior does not change non-filtered tools
- batch action saves resulting state correctly

Probable location:

- `Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Tests/Editor/UI/`
  or
- `.../Tests/Editor/Tool/Tool/`

## Recommended Implementation Order

1. Add the extension point in `McpListWindowBase.cs`.
2. Add action buttons to `McpToolsWindow.uxml`.
3. Style them in `McpToolsWindow.uss`.
4. Implement batch enable/disable in `McpToolsWindow.cs`.
5. Add automated tests.
6. Do a Unity Editor manual pass with large filtered/unfiltered sets.

## Branch Intent

This plan is written on branch:

- `codex/unity-tools-select-all-plan`

The next chat can use this branch as the starting point for implementation, or create a follow-up implementation branch from it if you want to keep planning and coding separate.
