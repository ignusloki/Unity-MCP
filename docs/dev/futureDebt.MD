# ShaderGraph MCP Future Debt

## Purpose

This document tracks deferred ShaderGraph MCP work, accepted limitations, validation gaps, and open questions after the current URP-first checkpoint.

The current exposed tool surface is documented in `docs/dev/shadergraph-mcp-capabilities.md`. The historical roadmap and epic breakdown remain in `docs/dev/shadergraph-mcp-plan.md`.

## Current Checkpoint

The implementation is sufficient to start first-pass URP Shader Graph recreation trials from a reference image, visual concept, or existing example.

Future work should be driven by concrete failures found during those trials instead of continuing speculative expansion.

## Accepted Authoring Limitations

### Dynamic Default-Slot Editing

Direct typed node-setting mutation exists for high-value URP node families, but editor-facing validation showed that some dynamic-vector-driven default-slot edits are not surfaced reliably enough in the Shader Graph UI to be the preferred authoring flow.

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

Current matrix already covers:

- Exact slot-type matches.
- Compatible UV/vector2 slot pairs.
- Texture2D property outputs into Texture2D input slots.
- Compatible dynamic numeric, vector, and color slot families.

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
- Broader render-pipeline support beyond the current URP-first scope.
- Long-tail Shader Graph node families not covered by the current allowlist.

### Style Recipe Expansion

The style-recipe tool intentionally returns warnings for fields outside the current implementation.

Future work:

- Expand recipe coverage only if recipe-based graph creation becomes useful again.
- Keep direct graph-control tools as the primary path for now.

## Validation And Tooling Debt

### Unity Test Runner Discovery

`dotnet build Assembly-CSharp.csproj -v minimal` passes in the local Unity test project with existing warnings and no errors at the last checkpoint.

Earlier Unity `tests-run` attempts did not discover the package editor tests from `TestShadergraph`.

Future work:

- Make package editor tests discoverable from the local Unity validation project.
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
