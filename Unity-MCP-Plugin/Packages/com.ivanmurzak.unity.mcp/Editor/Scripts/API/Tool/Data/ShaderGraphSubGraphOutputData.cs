/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Unity-MCP)    │
│  Copyright (c) 2025 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/

#nullable enable
using System.Collections.Generic;
using System.ComponentModel;

namespace AIGD
{
    [Description("A single desired output port on a Sub Graph's SubGraphOutputNode.")]
    public class ShaderGraphSubGraphOutputSlotInput
    {
        [Description("Display name for the output port. This becomes the port label on parent SubGraphNode instances. Must be unique within the output list.")]
        public string? Name { get; set; }

        [Description("Value type for the output port. Supported values: Color, Float, Vector2, Vector3, Vector4, Boolean. Texture2D, Matrix4, and Gradient are not supported in Phase 2.")]
        public string? Type { get; set; }

        [Description("Default value for a Float output.")]
        public float? FloatValue { get; set; }

        [Description("Default value for a Boolean output.")]
        public bool? BoolValue { get; set; }

        [Description("Default X component for Vector2/Vector3/Vector4/Color outputs.")]
        public float? X { get; set; }

        [Description("Default Y component for Vector2/Vector3/Vector4/Color outputs.")]
        public float? Y { get; set; }

        [Description("Default Z component for Vector3/Vector4 outputs.")]
        public float? Z { get; set; }

        [Description("Default W component for Vector4 outputs. For Color outputs, this is the alpha channel.")]
        public float? W { get; set; }
    }

    [Description("Structured input for setting the output port contract of a Sub Graph's SubGraphOutputNode.")]
    public class ShaderGraphSetSubGraphOutputsInput
    {
        [Description("Ordered list of desired output ports. Each entry declares a port name and value type. " +
            "This is a declarative replacement list — existing slots whose name and type match are preserved (keeping their slot ID and any incoming edge). " +
            "Existing slots whose name matches but type differs are replaced (incoming edges dropped). " +
            "New names are added. Missing names are removed when removeMissing is true.")]
        public List<ShaderGraphSubGraphOutputSlotInput>? Outputs { get; set; }

        [Description("When true (default), remove any existing SubGraphOutputNode slot whose name is not in the 'outputs' list. When false, keep unlisted slots.")]
        public bool? RemoveMissing { get; set; }
    }

    [Description("Per-output result describing what happened to a single output port during set-outputs reconciliation.")]
    public class ShaderGraphSubGraphOutputSlotResult
    {
        [Description("Display name of the output port.")]
        public string? Name { get; set; }

        [Description("Value type of the output port after reconciliation.")]
        public string? Type { get; set; }

        [Description("Slot ID after reconciliation.")]
        public int SlotId { get; set; }

        [Description("Action taken: 'kept' (name+type matched, slot preserved), 'replaced' (name matched but type changed — slot replaced, incoming edges dropped), 'added' (new slot created), 'removed' (slot removed because removeMissing=true).")]
        public string? Action { get; set; }

        [Description("Number of incoming edges dropped because the slot was replaced or removed.")]
        public int DroppedEdgeCount { get; set; }
    }

    [Description("Per-parent result describing the re-import outcome of a parent graph that references this sub-graph.")]
    public class ShaderGraphParentReimportResult
    {
        [Description("Asset path of the parent graph.")]
        public string? AssetPath { get; set; }

        [Description("Whether the parent graph still compiles without errors after re-import.")]
        public bool CompilesOk { get; set; }

        [Description("Warning messages for the parent, such as edges that became invalid after output port changes.")]
        public List<string>? Warnings { get; set; }
    }

    [Description("Result of mutating a Sub Graph's output port contract and re-importing the sub-graph and its parent graphs.")]
    public class ShaderGraphSetSubGraphOutputsResultData
    {
        [Description("Stable operation identifier.")]
        public string? Operation { get; set; }

        [Description("Per-output reconciliation results in the order of the input list. Removed outputs (if any) appear at the end.")]
        public List<ShaderGraphSubGraphOutputSlotResult>? OutputResults { get; set; }

        [Description("List of fields that actually changed during the mutation.")]
        public List<string>? ChangedFields { get; set; }

        [Description("Per-parent re-import results for every parent graph that references this sub-graph.")]
        public List<ShaderGraphParentReimportResult>? ParentResults { get; set; }

        [Description("Warning when the number of parent graphs exceeds the re-import cap. Null when all parents were re-imported.")]
        public string? ParentCapWarning { get; set; }

        [Description("Compact post-import summary of the sub-graph itself (always populated).")]
        public ShaderGraphSummaryData? GraphSummary { get; set; }

        [Description("Updated read-only structure of the sub-graph after the mutation. Populated only when includeStructure=true.")]
        public ShaderGraphStructureData? Structure { get; set; }

        [Description("Post-import Shader Graph data of the sub-graph. Populated only when includeGraph=true.")]
        public ShaderGraphData? Graph { get; set; }
    }
}
