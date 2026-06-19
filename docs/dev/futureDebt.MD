# ShaderGraph MCP Future Debt

## Purpose

This document tracks active ShaderGraph MCP debt after the reference recreation trials.

The current capability surface is documented in `docs/dev/shadergraph-mcp-capabilities.md`. The historical roadmap remains in `docs/dev/shadergraph-mcp-plan.md`.

## Current Decision

Pause new ShaderGraph recreation trials until graph authoring efficiency is improved.

The MCP can now recreate several medium ShaderGraphs, but the current per-node and per-edge authoring flow is too expensive for repeated trial work. Even with compact mutation responses, agents still spend too much time and context on many individual round trips, repeated Unity imports, and occasional full graph payloads.

## Stale Debt Removed

The following items are no longer active debt:

- Direct `Power` node support. `PowerNode` can now be created and configured.
- Scalar `Vector1MaterialSlot -> UVMaterialSlot` broadcast. This now supports paths such as `Time.Time -> Simple Noise.UV`.
- Direct `Vector4MaterialSlot -> UVMaterialSlot` truncation. This remains supported for Unity's documented `.xy` vector truncation behavior.
- Reflection-outline, object-outline, water-core, behavior, dissolve, and distortion trial node families that have already been implemented and validated through live MCP use.

Do not keep re-listing fixed trial gaps as future work unless a regression is found.

## P0 Efficiency Debt

### Batch ShaderGraph Mutation Tool

**v1 landed** on `custom/shadergraph-mcp`. The `assets-shadergraph-batch` tool ships with:

- Operation coverage: `addNode`, `updateNodeSettings`, `deleteNode`, `addProperty`, `updateProperty`, `deleteProperty`, `addPropertyNode`, `connectEdge`, `updateNodePosition`.
- Batch-local aliases on `addNode` / `addProperty` / `addPropertyNode`. Aliases stay in scope for the rest of the batch; later ops swap them into the existing `*ObjectId` selector fields.
- `ConnectEdge` accepts the reference form: `OutputSlot` / `InputSlot` carry `Node` (Alias / DisplayName / ObjectId) plus the slot `DisplayName`. The resolver consults the live structure plus the batch alias bag, removing the need for a separate `get-structure` lookup.
- `stopOnError` (default `true`). On the first failure the `.shadergraph` file is snapshot-rolled back to its pre-batch content and the failure is surfaced with the failing op's index. `stopOnError=false` persists whatever succeeded and reports per-op errors.
- One consolidated `ShaderGraphBatchResultData` response: per-op summaries (operation tag, ObjectId, ChangedFields, error), an alias-to-id map, and a single post-batch `GraphSummary`. No per-op `Structure` / `Graph` echo.

**v2 follow-up debt** (the actual per-edge import cost):

- Share a single in-memory `GraphData` across the batch so the batch performs **one** AssetDatabase import instead of N. The v1 batch still pays N imports because each op delegates to its single-op helper, which loads + saves + imports on its own.
- Extend the batch to the remaining operation kinds called for in trials: `setBlocks`, `setSettings`, `disconnectEdge`, `reconnectEdge`, `duplicateNode`, `setPropertyCategory`, `reorderProperty`, `createCategory`.
- Add `continueOnAlreadyExists` flag for idempotent edge creation (also lands in P1's idempotent-edge work).
- Extend alias coverage so `addPropertyNode` can take a `PropertyRef` (Alias / ReferenceName) instead of requiring `PropertyObjectId`.

### Compact Responses By Default

Mutation tools should default to compact responses.

Current good behavior:

- Mutation tools can return created/changed object ids, changed fields, affected edge, and `GraphSummary` without returning the full graph.
- `assets-shadergraph-batch` now exposes the `ShaderGraphResponseMode` enum (`Summary` / `Diff` / `Selection` / `Full`). `Diff` skips `GraphSummary`, `Selection` projects only the touched nodes (+ touching edges) via the QueryStructure projection, `Full` mirrors a post-batch `get-structure` call.

Remaining debt:

- Wire `ShaderGraphResponseMode` into the 17 single-op mutation tools so callers no longer need to combine `includeStructure` / `includeGraph` / `includeMessages` / `includeProperties` flags by hand. This is intentionally deferred because the change touches every mutation tool's signature, the dispatcher helpers, and every existing editor test that pins those flags. Plan: introduce a shared `ShaderGraphMutationOptions` input DTO that owns the response mode (and folds in the legacy flags for back-compat), then migrate tools one batch at a time.
- Avoid accidental full graph payloads unless explicitly requested. Keep `includeStructure`, `includeGraph`, and `includeProperties` defaulted to false for the single-op tools until they migrate to the new enum.
- Return only the requested node/property/edge subset when detail is needed.

### Filtered Graph Read API

**v1 landed** on `custom/shadergraph-mcp`. The `assets-shadergraph-query-structure` tool ships with:

- A `Stats` summary (node/edge/property/category/target counts and parse status) returned on every call.
- `StatsOnly` shortcut for the cheapest possible probe.
- `PropertiesOnly` shortcut for blackboard-only inspection.
- Node filters: `NodeObjectIds`, `NodeTypeSubstrings`, `NodeDisplayNames` (any-match across the three).
- Per-node projection toggles: `IncludeSlots` (default true) and `IncludeNodeSettings` (default true) for trimming heavy per-node payloads.
- Edge filters: `IncludeEdges` (default true) and `EdgesTouchingNodeIds` for scoping to a node set.
- Target/context toggle: `IncludeTargets` (default true) for skipping `Targets`, `VertexContext`, and `FragmentContext`.

`assets-shadergraph-get-structure` stays unchanged for callers that need the full graph.

**v2 follow-up debt**:

- Operate on a single in-memory parse instead of re-reading the file per query (currently each query rebuilds the full structure before projecting). Acceptable for now because the read tool is called rarely compared to per-edge mutations.
- Add a `Diagnostics` projection mode (graph diagnostics + parse error in isolation) once Epic 8A's compact-response work introduces a stable diagnostics-only payload.
- Add a settings-only / targets-only filter pair to cover the trimmed "blocks/targets/settings only" need without forcing callers to use `get-settings`.

### Stable Agent-Friendly References

Agents currently have to preserve many serialized object ids across calls. That is brittle and token-heavy.

Future API should support:

- Node aliases assigned at creation time.
- Property lookup by reference name.
- Slot lookup by display name plus slot direction.
- Edge creation using aliases instead of serialized ids.
- Batch-local alias resolution.
- Optional persisted comments/labels only when graph organization is requested.

### Import Strategy

Per-edge Unity imports dominate authoring time.

Future work:

- Defer import until the end of a batch when safe.
- Import early only when Unity must materialize slots or generated ids.
- Return clear diagnostics if an operation cannot be validated without import.
- Measure import time separately from MCP serialization time.

## P1 Authoring Debt

### Clean Universal Template

The trials sometimes clone existing graphs to avoid unrelated HDRP target scaffolding. That can preserve stale Blackboard categories and template metadata.

Future work:

- Provide a clean MCP-owned Universal-only blank ShaderGraph template.
- Ensure the template does not include unused categories, properties, groups, sticky notes, or target scaffolding unrelated to URP.
- Prefer this template for all strict recreation trials.

### Blackboard Category Cleanup

**v1 landed** on `custom/shadergraph-mcp`. `assets-shadergraph-delete-category` deletes a named or id-selected category (refuses the default category, refuses non-empty categories unless `ReassignPropertiesToDefault=true`). `assets-shadergraph-prune-empty-categories` sweeps and deletes every empty non-default category, and skips the AssetDatabase import entirely when nothing is removed.

**v2 follow-up debt**:

- Prune category property ids that no longer resolve (stale references to deleted properties), not just empty-list categories.
- Repair the default category in cloned templates that ship with stale child references inherited from the source template.
- Expose a single "blackboard repair" tool that bundles delete-empty + reassign-orphans + repair-default into one call for clone-then-customize workflows.

### Idempotent Edge Creation

**v1 landed** on `custom/shadergraph-mcp`. `ShaderGraphConnectEdgeInput.AllowExisting` is honoured by both the single-op `assets-shadergraph-connect-edge` tool and the batch tool's `connectEdge` op. When true and the requested edge already exists, the executor short-circuits before the JSON write + AssetDatabase import, returns the existing edge with `ChangedFields=["edge.alreadyExists"]` and `AlreadyExisted=true`. Incompatible slot pairings and conflicting input connections still fail loudly.

**v2 follow-up debt**:

- Apply the same idempotent treatment to `assets-shadergraph-add-node` (skip when an equivalent node already exists at the requested type + position) and `assets-shadergraph-add-property` (skip when a property with the same effective reference name already exists). Both want a careful "what counts as equivalent" rule; defer until a trial requires them.
- Mirror `AllowExisting` on `assets-shadergraph-reconnect-edge` and `assets-shadergraph-reroute-output-slot` for the same resume-friendly semantics.

### Dynamic Default-Slot Editing

Direct typed node-setting mutation exists for common node families, but dynamic-vector default-slot editing remains fragile in Unity ShaderGraph.

Accepted authoring rule:

- Prefer edge wiring or blackboard properties over forcing dynamic literal defaults before the dynamic slot has resolved its final width.

Future work:

- Normalize incompatible literal fallback metadata before edge creation where safe.
- Improve direct literal/default-slot mutation only when trial evidence shows a real need.

### Texture Slot Assignment Boundaries

Direct `Sample Texture 2D.Texture` slot asset assignment is supported for unconnected texture slots.

Accepted authoring rule:

- Use a blackboard `texture2D` property plus `PropertyNode` plus edge wiring for connected texture inputs.

Future work:

- Revisit direct connected texture-slot mutation only if it blocks a real recreation case.

### Vector3 To UV Narrowing

Direct `Vector3MaterialSlot -> UVMaterialSlot` edges remain intentionally rejected.

Accepted authoring rule:

- Use `Vector3 -> Split -> Combine.RG -> UV` when a Vector3 source must feed a UV/vector2 input.

Future work:

- Validate whether Unity's serialized graph model consistently imports direct `Vector3 -> Vector2/UV` edges across supported Unity/ShaderGraph versions.
- If safe, add compatibility support and tests.
- If not safe, keep the explicit narrowing path and consider an MCP helper that auto-inserts conversion nodes.

## P2 Feature Debt

### Graph Organization

Deferred organization features:

- Groups.
- Sticky notes.
- Layout cleanup.
- Safe bulk graph refactors.

These improve maintainability but are lower priority than efficient graph construction.

### Advanced ShaderGraph Coverage

Deferred advanced features:

- Subgraphs.
- Custom Function nodes.
- Keyword-driven authoring.
- Dropdown/enum-driven authoring.
- Redirect node creation.
- Broader render-pipeline support beyond the current URP-first scope.
- Long-tail ShaderGraph node families not yet encountered in trials.

### Invert Colors Alpha Toggle

`Invert Colors` red, green, and blue toggles are supported. Alpha is still rejected because the installed ShaderGraph package did not expose a safely serialized alpha backing field during validation.

Future work:

- Revisit only if a Unity package update exposes a safe persisted path or a real trial requires alpha inversion.

### URP Target And Subtarget Coverage

Known deferred target work:

- Default Decal Blending.
- Default SSAO.
- Subtarget-only fields that require separate serialized-path validation through `m_Datas`.
- Unsupported/custom block mutation outside the built-in block allowlist.

## Validation Debt

The local Unity validation project under `Unity-test/TestShadergraph` is git-ignored and should be treated as local validation state, not a reproducible repository fixture.

Current validation strategy:

- Compile sanity checks.
- Live MCP mutation validation in Unity.
- Material texture binding validation through MCP.
- Unity Editor visual verification for reference recreation trials.

Future work:

- Make package editor tests discoverable and runnable from the local Unity validation project.
- Add tracked minimal validation fixtures or a documented setup flow.
- Add batch-mutation end-to-end tests once the batch tool exists.
- Track MCP serialization payload size, Unity import count, and total authoring time per trial.

## Version Alignment

Keep the running standalone MCP server aligned with the package code under test before diagnosing tool availability.

Future work:

- Add a fast version/capability check that reports package version, server version, available ShaderGraph node types, and supported slot compatibility cases.

## Open Questions

- Should batch mutation become the only recommended path for medium/large ShaderGraph creation?
- Which Unity and ShaderGraph package versions should be first-class validation targets?
- Should strict recreation trials allow documented auto-conversion helpers, or should topology parity remain the default?
- If upstream contribution is desired later, should these changes be split into smaller upstream-friendly patches?
