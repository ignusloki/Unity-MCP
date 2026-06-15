# ShaderGraph MCP Future Debt

## Purpose

This document tracks deferred ShaderGraph MCP work, accepted limitations, validation gaps, and open questions after the current URP-first checkpoint.

The current exposed tool surface is documented in `docs/dev/shadergraph-mcp-capabilities.md`. The historical roadmap and epic breakdown remain in `docs/dev/shadergraph-mcp-plan.md`.

## Current Checkpoint

The implementation is sufficient to start first-pass URP Shader Graph recreation trials from a reference image, visual concept, or existing example, but it is not yet sufficient to claim broad reference recreation parity.

Future work should be driven by concrete failures found during those trials instead of continuing speculative expansion.

## Accepted Authoring Limitations

### Dynamic Default-Slot Editing

Direct typed node-setting mutation exists for high-value URP node families, but editor-facing validation showed that some dynamic-vector-driven default-slot edits are not surfaced reliably enough in the Shader Graph UI to be the preferred authoring flow.

Epic 7A validation also showed that connected dynamic arithmetic chains should not force a mismatched literal default value first. For example, setting a unary trig node's dynamic input fallback to a vector literal and then wiring a different vector width can cause Unity Shader Graph concretization diagnostics. The stable authoring flow is to wire connected dynamic chains first, or to use a blackboard property when an explicit reusable value is required.

Current stable workaround:

- Create or update a blackboard property.
- Add a `PropertyNode`.
- Connect that property-backed output into the target node input.

Validated workaround targets:

- `Tiling And Offset`
- `Branch`
- `Split`
- `Combine`
- `Add`
- `Subtract`
- `Divide`
- `Lerp`
- `One Minus`

Future work:

- Improve direct literal/default-slot mutation so this workaround is no longer needed.
- Expand direct `Multiply` input-slot literal editing beyond the current `multiplyType` support.
- Investigate whether connected dynamic inputs should automatically clear or normalize incompatible literal fallback metadata before edge creation.

### Texture Slot Assignment Boundaries

Direct `Sample Texture 2D.Texture` slot asset assignment is supported for unconnected texture slots.

Connected texture inputs should continue to use the stable blackboard `texture2D` property plus `PropertyNode` plus edge wiring flow.

Future work:

- Revisit connected texture-slot workflows only if real graph recreation trials show this is materially blocking.

### URP Target And Subtarget Coverage

The current target-setting coverage focuses on stable Universal target scalar fields and built-in stack blocks.

Known deferred items:

- Default Decal Blending and Default SSAO were not available on the Universal target object during validation.
- Subtarget-only fields need separate serialized-path validation through the `m_Datas` structure before mutation support.
- Unsupported or custom block mutation parity remains outside the current built-in block allowlist.

## Deferred Feature Work

### Reference Image Interpretation Helper

The former Slice 12.4 is now decision-only debt.

Question to revisit:

- Should MCP expose an explicit helper for turning a reference image or visual concept into ShaderGraph authoring steps?

Current position:

- Do not build this yet.
- Codex can interpret images or concepts outside Unity and use the existing MCP graph-control tools to author the graph.
- Only add a Unity-side helper if real recreation trials reveal a concrete gap.

### Additional Slot Compatibility

The former conditional Slice 10.5 remains deferred.

Future work:

- Add additional slot compatibility cases only when a concrete unsupported URP graph path fails the current matrix.
- Investigate direct `Vector3MaterialSlot -> UVMaterialSlot` only if Unity Shader Graph's editor and serialized model both validate that edge without an explicit conversion node.

Current matrix already covers:

- Exact slot-type matches.
- Compatible UV/vector2 slot pairs.
- Vector2-resolved `DynamicVectorMaterialSlot -> UVMaterialSlot` paths such as `Add.Out -> Tiling And Offset.UV`.
- Screen Position vector4 outputs into Shader Graph screen-position input slots, such as `ScreenPosition.Out -> SceneDepth.UV`.
- Compatible Vector3/Position slot pairs.
- Compatible color/vector slot pairs when one endpoint is a ShaderGraph color slot, such as Color properties feeding Fragment Base Color.
- Texture2D property outputs into Texture2D input slots.
- Compatible dynamic numeric, vector, and color slot families, including cross-family `DynamicValueMaterialSlot`/`DynamicVectorMaterialSlot` pairs.
- Safe explicit Vector3-to-UV authoring through `Vector3 -> Split -> Combine.RG -> UV`.

Accepted limitation:

- Direct `Vector3 -> UV` edges are intentionally rejected. Use the explicit `Split` plus `Combine.RG` narrowing path when a source vector such as `View Vector` must feed a UV/vector2 input such as `Gradient Noise.UV`.
- Unity Shader Graph documents vector promotion/truncation between vector ports, and `Gradient Noise.UV` is a `Vector 2` input while `View Vector.Out` is a `Vector 3` output. The explicit `Split -> Combine.RG` path is therefore semantically equivalent to dropping the third channel, but it is not topology-equivalent to a direct editor wire.
- Before allowing direct `Vector3MaterialSlot -> UVMaterialSlot` writes, validate that Unity's serialized graph model imports and compiles that direct edge consistently across the supported Unity/ShaderGraph package versions. If validation passes, add typed compatibility tests for direct `Vector3 -> Vector2/UV`, `Vector4 -> Vector2`, and vector promotion cases; otherwise keep the explicit narrowing path and consider an MCP helper that auto-inserts the conversion nodes.

### Graph Organization And Cleanup

Deferred organization features:

- Groups.
- Sticky notes.
- Layout cleanup.
- Safe bulk graph refactors for supported node families.

These are useful for readability and maintainability but are not required for first-pass shader behavior recreation.

### Advanced And Long-Tail Authoring

Deferred advanced features:

- Subgraphs.
- Custom function nodes.
- Keyword-driven authoring.
- Enum-driven authoring.
- Explicit target/subtarget conversion between Unlit and Lit/PBR graphs. Current Lit/PBR-style validation uses Lit Shader Graph templates plus supported block control.
- Broader render-pipeline support beyond the current URP-first scope.
- Long-tail Shader Graph node families not covered by the current allowlist.

Note: the reflection-outline trial nodes implemented in Epic 7A, the `Object` node implemented in Epic 7B, the MinionsArt water-core nodes implemented in Epic 7C, and the MinionsArt behavior nodes implemented in Epic 7D are no longer considered long-tail.

### Redirect Nodes

`RedirectNodeData` remains intentionally unsupported for add, duplicate, and explicit read/write mutation flows.

Current position:

- Treat redirect nodes as layout/readability data rather than behavior-bearing graph topology.
- Do not block ShaderGraph recreation work on redirect support unless a concrete trial proves Unity import or shader behavior depends on recreating them.

Future work:

- Revisit safe redirect-node authoring only if a later reference graph proves reroute-node parity is necessary for practical agent workflows or serialized-graph fidelity.

### Style Recipe Expansion

The style-recipe tool intentionally returns warnings for fields outside the current implementation.

Future work:

- Expand recipe coverage only if recipe-based graph creation becomes useful again.
- Keep direct graph-control tools as the primary path for now.

## Validation And Tooling Debt

### Unity Test Runner Discovery

A compile sanity check, `dotnet build Assembly-CSharp.csproj -v minimal`, passed in the local Unity 6 URP/ShaderGraph validation project with 0 warnings and 0 errors at the last checkpoint.

Live project-scoped MCP validation for Epic 7A passed on 2026-06-14 through the already-open Unity Editor:

- `Codex_Epic7A_SettingsSmoke.shadergraph` imported successfully after creating/updating all new node families and exercising duplicate/move/delete on `Transform`.
- `Codex_Epic7A_E2E.shadergraph` imported successfully after wiring the reflection-outline path, including source-vector nodes, trig chain, gradient-noise displacement modulation, transform to vertex position, and preserved base texture/color output.
- `Codex_ViewVectorUV_ReflectionOutline.shadergraph` imported successfully after wiring the exact trial path with `View Vector -> Split -> Combine.RG -> Gradient Noise.UV`, source-vector nodes, trig chain, gradient-noise displacement modulation, transform to vertex position, and preserved base texture/color output.
- `Codex_ObjectScaleOutline.shadergraph` imported successfully after wiring the second-trial object-scale outline path, including `Object.Scale -> Divide.B`, object-space position extrusion to vertex position, outline color to fragment base color, and Universal target `surface=opaque`, `renderFace=back`.
- `Codex_WaterCore_MinionsArt.shadergraph` imported successfully after wiring the MinionsArt water-core pretrial path, including `ScreenPosition.Out -> SceneDepth.UV`, Screen Position `raw`, Scene Depth `eye`, `Smoothstep`/`Saturate`, `Time`, real `Vector2 -> Sample Texture 2D.UV`, and Lit Base Color/Alpha/Smoothness outputs.

The current workspace includes `Unity-test/TestShadergraph`, but `Unity-test/` is git-ignored. Treat it as local validation state, not a reproducible repository fixture.

Earlier Unity `tests-run` attempts did not discover the package editor tests from the local validation project. A later targeted test-run attempt for the Object outline editor test was blocked because the already-open Unity Editor had an unsaved scene; a targeted attempt for the water-core editor test returned "No tests found". Do not save user/editor scene state automatically just to run package tests.

Future work:

- Make package editor tests discoverable from the local Unity validation project.
- Decide whether `Unity-test/TestShadergraph` should remain local-only, become documented setup-required state, or be replaced by a tracked minimal validation fixture.
- Add higher-signal end-to-end ShaderGraph authoring validation cases.

Until this is fixed, use:

- Compile sanity checks.
- Live MCP mutation validation.
- Unity Editor visual/user verification before committing behavior changes.

### Version Alignment Check

During earlier validation, the running standalone MCP server reported `0.80.1.0` while package files were updated to `0.81.0`.

Future work:

- Refresh the running server process when version alignment matters.
- Confirm the runtime MCP server version before diagnosing tool availability issues.

## Open Questions

- Which Unity versions should be first-class validation targets beyond the current Unity 6 local project?
- Should broad ShaderGraph control remain private-fork only until the mutation model is fully proven?
- If upstream contribution is desired later, should future slices be reshaped around smaller upstream-friendly patches?
