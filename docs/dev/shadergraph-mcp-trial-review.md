# ShaderGraph MCP Trial Review

Date: 2026-06-14

Scope:
- Reviewed the ShaderGraph MCP delta on `custom/shadergraph-mcp` relative to `custom/main`.
- Focused on the ShaderGraph control surface, supporting helpers, tests, and the ShaderGraph-specific docs.
- Did not review unrelated MCP areas in depth.

Follow-up status:
- The three original P1 findings from the code review were fixed after this review.
- The later reflection-outline node-coverage P1 is implemented and live-validated by `docs/dev/shadergraph-mcp-plan.md` Epic 7A.
- A 2026-06-14 reference-image recreation trial confirmed a separate ShaderGraph node-coverage blocker: `View Direction`, `View Vector`, `Normal Vector`, `Position`, `Transform`, `Gradient Noise`, `Sine`, `Cosine`, and `Negate` were not exposed by the node lifecycle tools at the time of review.
- A later second outline trial confirmed one additional node blocker, ShaderGraph `Object`; this is implemented and live-validated by `docs/dev/shadergraph-mcp-plan.md` Epic 7B.
- The MinionsArt water-shader pretrial confirmed missing water-core nodes: `Screen Position`, `Scene Depth`, `Time`, `Smoothstep`, `Saturate`, and real `Vector 2`. These are implemented and live-validated in Epic 7C.
- The exact MinionsArt water reference graph then confirmed remaining behavior-node blockers: `Scene Color`, `Comparison`, `Normal From Height`, `Blend`, `Remap`, and `Swizzle`. These are implemented and validated in Epic 7D.

## P0

- None found in this pass.

## P1

- Reflection-outline reference recreation was blocked by missing core node families at review time.
  - Current status: implemented by Epic 7A after this review and live-validated through the existing project-scoped Unity MCP/editor session.
  - Historical evidence: at review time, `assets-shadergraph-add-node` only exposed math/split/combine/sample/tiling/branch nodes. The 2026-06-14 reflection-outline trial needed `View Direction`, `View Vector`, `Normal Vector`, `Position`, `Transform`, `Gradient Noise`, `Sine`, `Cosine`, and `Negate`.
  - Impact at review time: the MCP could create a valid approximation graph/material/scene, but it could not faithfully author the reference graph's source-vector, reflection-normal, noise-displacement, transform, and vertex-output chain.
  - Why this was P1: it blocked the next meaningful reference-image trial for a common outline/reflection shader family. It did not corrupt existing graphs, but it prevented the feature from meeting the intended "most ShaderGraphs from reference" goal.

- Second outline trial recreation was blocked by missing ShaderGraph `Object` node support.
  - Current status: implemented by Epic 7B after this review and live-validated through the existing project-scoped Unity MCP/editor session.
  - Evidence: the second trial needed `Object.Scale -> Divide.B` for object-scale-aware outline thickness. Current capabilities now list `object` as an allowlisted node and document Object slot readback.
  - Impact at blocker time: the MCP could not author the simple outline graph topology without manual intervention or a non-equivalent graph.
  - Why this was P1: it blocked a concrete trial graph and affected a common outline-shader pattern, but did not corrupt existing graph assets.

- MinionsArt water-shader recreation was blocked by missing water-core ShaderGraph node families and screen-depth wiring.
  - Current status: implemented by Epic 7C after this review and live-validated through the existing project-scoped Unity MCP/editor session.
  - Evidence: the water graph needs `Screen Position`, `Scene Depth`, `Time`, `Smoothstep`, `Saturate`, and `Vector 2`, including `ScreenPosition.Out -> SceneDepth.UV`, Scene Depth `eye` sampling, and Lit/PBR-style output blocks.
  - Impact at blocker time: the MCP could not author the water graph's core depth-fade and output chain without manual intervention.
  - Why this is P1: it blocks the next concrete reference-shader trial and covers common water/transparent-surface graph patterns.

- MinionsArt water-shader exact recreation was also blocked by missing behavior-node families.
  - Current status: implemented by Epic 7D after this review with readback, settings-update, lifecycle, and behavior-path validation.
  - Evidence: the exact reference graph uses `Scene Color`, `Comparison`, `Normal From Height`, `Blend`, `Remap`, and `Swizzle`, plus a `Normal From Height.Out -> SurfaceDescription.NormalWS` edge.
  - Impact at blocker time: the MCP could author a minimal water-core approximation, but not the original graph's refraction, color blend, remap, comparison, and generated-normal behavior.
  - Why this is P1: it blocked faithful MinionsArt recreation even after the water-core path compiled.

- Default-reference-name flows are broken for properties that do not set `overrideReferenceName`.
  - Evidence: [Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/API/Tool/Assets.ShaderGraph.UpdateProperty.cs](/Users/suporte/Unity-MCP/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/API/Tool/Assets.ShaderGraph.UpdateProperty.cs:486) uses `GetEffectivePropertyReferenceName(...)` for duplicate detection, but [that helper](/Users/suporte/Unity-MCP/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/API/Tool/Assets.ShaderGraph.UpdateProperty.cs:521) returns `m_OverrideReferenceName` whenever the field exists, even when it is the empty string. New properties created without an override name explicitly serialize `m_OverrideReferenceName = ""`, for example in [Assets.ShaderGraph.AddProperty.cs](/Users/suporte/Unity-MCP/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/API/Tool/Assets.ShaderGraph.AddProperty.cs:257).
  - Impact: the toolchain can fail to resolve properties by their generated default reference names and can miss duplicate generated reference names. This affects `add-property` uniqueness checks plus every property-selector path that relies on reference name lookup, including `update-property`, `delete-property`, `reorder-property`, `set-property-category`, and `add-property-node`.
  - Why this is P1: this is a core authoring-path defect for any agent flow that creates properties without forcing explicit override names first.

- Default-category fallback can silently target the wrong category.
  - Evidence: [Assets.ShaderGraph.AddProperty.cs](/Users/suporte/Unity-MCP/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/API/Tool/Assets.ShaderGraph.AddProperty.cs:499) resolves category placement with `allowDefaultFallback: true`, but [GetOrCreateDefaultCategoryObject](/Users/suporte/Unity-MCP/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/API/Tool/Assets.ShaderGraph.Blackboard.cs:438) returns the first category in `m_CategoryData`, not the category whose name is actually empty.
  - Impact: when an add-property request omits category selectors, the new property can be inserted into an arbitrary named category if that category happens to be first. That directly contradicts the documented "default category" behavior and makes blackboard placement order-dependent.
  - Why this is P1: it can silently misplace properties during normal authoring, which is exactly the kind of defect that wastes trial time and is hard to diagnose from the agent side.

- The stated validation story was not reproducible enough to support the "ready for first-pass trial" claim at review time.
  - Historical evidence: the capabilities and future-debt docs previously described the validation baseline ambiguously. The current workspace has `Unity-test/TestShadergraph`, but `Unity-test/` is git-ignored, and earlier Unity `tests-run` attempts did not discover the package editor tests from the local validation project.
  - Impact: the branch should not treat local live validation as a complete automated validation baseline. Before a serious release checkpoint, another engineer needs a reproducible validation path they can run without guesswork.
  - Why this is P1: this does not break runtime mutation directly, but it does break confidence in the checkpoint and should be fixed before using the branch as the basis for a meaningful trial.

## P2

- Open ShaderGraph windows are not reloaded consistently after every mutation path.
  - Evidence: several mutators use manual `ImportAsset/SaveAssets/Refresh` sequences instead of `FinalizeShaderGraphMutation(...)`, for example [Assets.ShaderGraph.AddNode.cs](/Users/suporte/Unity-MCP/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/API/Tool/Assets.ShaderGraph.AddNode.cs:92), [Assets.ShaderGraph.UpdateEdge.cs](/Users/suporte/Unity-MCP/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/API/Tool/Assets.ShaderGraph.UpdateEdge.cs:250), and [Assets.ShaderGraph.Settings.Common.cs](/Users/suporte/Unity-MCP/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Editor/Scripts/API/Tool/Assets.ShaderGraph.Settings.Common.cs:93). Other ShaderGraph mutators do use `FinalizeShaderGraphMutation(...)`, and that helper is the only place that calls `ReloadOpenShaderGraphWindows(...)`.
  - Impact: with a ShaderGraph editor window open, some successful mutations can leave the UI stale until the graph is manually reopened or refreshed. That is a debugging and operator-confidence problem more than a serialization problem.
  - Why this is P2: it is real, but it is not an immediate blocker for the first trial if you are prepared to verify by reopening the graph after those operations.

- The current editor test coverage does not appear to exercise the two P1 blackboard-helper failure modes.
  - Evidence: the property tests I reviewed use explicit `OverrideReferenceName` values, for example [AssetsShaderGraphTests.cs](/Users/suporte/Unity-MCP/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Tests/Editor/Tool/Assets/AssetsShaderGraphTests.cs:422), [AssetsShaderGraphTests.cs](/Users/suporte/Unity-MCP/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Tests/Editor/Tool/Assets/AssetsShaderGraphTests.cs:729), and [AssetsShaderGraphTests.cs](/Users/suporte/Unity-MCP/Unity-MCP-Plugin/Packages/com.ivanmurzak.unity.mcp/Tests/Editor/Tool/Assets/AssetsShaderGraphTests.cs:1026). I did not find coverage for generated default-reference-name lookup or for default-category fallback when category selectors are omitted.
  - Impact: both helper defects above are the kind of issues that will continue to regress unless they get explicit tests.
  - Why this is P2: the missing tests are not the production defect, but they are why the defect escaped into the checkpoint.

## Review Notes

- I did not execute Unity Editor tests from this environment.
- The P1 items above are based on direct code inspection and documented validation-state conflicts, not speculation.
- Follow-up live validation was later performed in the local Unity validation project; automated package editor test discovery remains separate validation debt.
