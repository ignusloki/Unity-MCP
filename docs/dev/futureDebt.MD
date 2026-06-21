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

**v1 + Dissolve-4 follow-ups landed** on `custom/shadergraph-mcp`. The `assets-shadergraph-batch` tool ships with:

- Operation coverage: `addNode`, `updateNodeSettings`, `deleteNode`, `addProperty`, `updateProperty`, `deleteProperty`, `addPropertyNode`, `connectEdge`, `updateNodePosition`, `setSettings`, `setBlocks`.
- Batch-local aliases on `addNode` / `addProperty` / `addPropertyNode`. Aliases stay in scope for the rest of the batch and resolve via either the `Node` / `Property` reference envelopes on `updateNodeSettings` / `updateProperty` / `addPropertyNode`, or as bare / `@alias` strings in the existing `*ObjectId` fields.
- `ConnectEdge` accepts the reference form: `OutputSlot` / `InputSlot` carry `Node` (Alias / DisplayName / ObjectId) plus the slot `DisplayName`. The resolver consults the live structure plus the batch alias bag, removing the need for a separate `get-structure` lookup.
- `stopOnError` (default `true`). On the first failure the `.shadergraph` file is snapshot-rolled back, the file mtime is bumped, and `FinalizeShaderGraphMutation` (re-import + ShaderGraph window reload) runs so the importer drops its cached `GraphData`. The thrown error is self-describing: it ships an MD5 snapshot hash, a `Pre-batch fingerprint` block (`NodeCount=… EdgeCount=… ShaderResolved=…`), and either `Asset rolled back to pre-batch content; retries are safe.` (verified hash match after restore) or `WARNING: rollback verification FAILED.` (the bytes on disk don't match the snapshot — typically a dirty Shader Graph editor window stomped the restore). `stopOnError=false` persists whatever succeeded and reports per-op errors.
- One consolidated `ShaderGraphBatchResultData` response: per-op summaries (operation tag, ObjectId, ChangedFields, error), an alias-to-id map, and a single post-batch `GraphSummary`. No per-op `Structure` / `Graph` echo.

**v2 follow-up debt** (the actual per-edge import cost):

- Share a single in-memory `GraphData` across the batch so the batch performs **one** AssetDatabase import instead of N. The v1 batch still pays N imports because each op delegates to its single-op helper, which loads + saves + imports on its own.
- Extend the batch to the remaining single-op mutations not yet covered: `disconnectEdge`, `reconnectEdge`, `duplicateNode`, `setPropertyCategory`, `reorderProperty`, `createCategory`, `deleteCategory`, `pruneEmptyCategories`.
- Add `continueOnAlreadyExists` flag for idempotent edge creation (already lands in P1's idempotent-edge work via `AllowExisting`; mirroring it on `reconnect-edge` and `reroute-output-slot` is the open work).
- **E-3 — Hostile-window rollback hardening.** If E-2's hash-mismatch warning ever fires in a real trial, the cause is almost certainly that `FinalizeShaderGraphMutation` calls `AssetDatabase.SaveAssets()` *before* `ReloadOpenShaderGraphWindows()`. A dirty open Shader Graph window holding the user's in-memory graph would have its state saved on top of the just-restored snapshot. Fix: add a `FinalizeForRollback` variant that reloads open windows first, then re-imports, and skips `SaveAssets()`. Deferred until E-2 actually flags a failure — landing it preemptively touches ShaderGraph reflection that's hard to unit-test in isolation.

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

**v1 landed** on `custom/shadergraph-mcp`. The batch tool's alias system covers the trial use cases:

- Node aliases assigned at creation time on `addNode` / `addProperty` / `addPropertyNode`.
- Property lookup by reference name on the existing `*PropertyReferenceName` fields.
- Slot lookup by display name on `connectEdge` (`OutputSlot` / `InputSlot.DisplayName` with parent `Node` envelope).
- Edge creation using aliases instead of serialized ids on `connectEdge`.
- Batch-local alias resolution on every node/property-touching op via the `Node` / `Property` reference envelopes or the legacy `*ObjectId` strings (which now accept bare or `@alias` forms).

**v2 follow-up debt**:

- Cross-session aliases: today aliases die at the end of the batch. A trial might want to attach human-readable handles persisted in the graph (sticky-note-style) and resolve them across batches. Requires a stable serialized field and a careful "agent renamed it" story.
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

**v1 landed** on `custom/shadergraph-mcp`. The plugin ships a vendored URP-only blank fixture at `Packages/com.ivanmurzak.unity.mcp/Editor/Templates/Unlit URP Clean.shadergraph` (URP target only, 6 block-stack nodes, no inherited blackboard properties / categories, no HDTarget scaffolding). `assets-shadergraph-create` exposes the shortcut flag `useCleanUrpTemplate` to clone it.

**v2 follow-up debt**:

- Author a clean URP **Lit** template (Lit/PBR block stack with no inherited cross-pipeline scaffolding) for trials that need PBR. The current fixture is Unlit only.
- Author a clean HDRP-only template for HDRP-only recreation trials. Same authoring pattern: strip URP scaffolding from a cross-pipeline source.
- When Unity exposes a stable serialised path for groups and sticky notes, sweep them out of any future MCP-owned template too.

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
- `Step.Edge` is a special literal-threshold slot. Do not wire dynamic outputs into it; MCP rejects that topology because Unity imports it with a Shader Graph concretization error. Use `assets-shadergraph-update-node-settings` to set `step.edge` and route dynamic dissolve/noise values into `Step.In`.

Future work:

- Normalize incompatible literal fallback metadata before edge creation where safe.
- Improve direct literal/default-slot mutation only when trial evidence shows a real need.
- Revisit `Step.Edge` only if a future Unity ShaderGraph package exposes a safe non-literal threshold mode or if Unity's own editor starts accepting dynamic edges into that slot without import errors.

### Texture Slot Assignment Boundaries

Direct `Sample Texture 2D.Texture` slot asset assignment is supported for unconnected texture slots.

Accepted authoring rule:

- Use a blackboard `texture2D` property plus `PropertyNode` plus edge wiring for connected texture inputs.

Future work:

- Revisit direct connected texture-slot mutation only if it blocks a real recreation case.

### Vector3 To UV Narrowing

**v1 landed** on `custom/shadergraph-mcp`. Direct `Vector3MaterialSlot -> UVMaterialSlot` edges remain intentionally rejected by `ValidateEdgeCompatibility`, but `assets-shadergraph-connect-edge` now exposes `AutoNarrowVector3ToUV`. When true and the resolved slot pair is exactly Vector3 -> UV, the plugin authors the chain `source -> Split.In, Split.R -> Combine.R, Split.G -> Combine.G, Combine.RG -> target.UV` in one call. The Split + Combine ids are returned in `AutoCreatedNodeObjectIds`; the three intermediate edges in `AutoCreatedEdges`; the final `Edge` reports the Combine.RG -> target connection.

**v2 follow-up debt**:

- The narrow currently costs 6 AssetDatabase imports (2 add-node + 4 connect-edge) because each step delegates to the existing single-op helpers. Collapsing to 1 import per narrow is part of the Epic 8B v2 shared in-memory `GraphData` work.
- Extend the same auto-narrow contract to `assets-shadergraph-reconnect-edge` and to the batch tool's `connectEdge` op.
- Investigate whether Unity ever exposes a stable direct Vector3 -> UV path across the supported Unity / ShaderGraph package versions. If a trial proves it round-trips correctly, replace the auto-narrow path with a direct edge and keep the flag as a compatibility shim.

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
