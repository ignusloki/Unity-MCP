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
    [Description("Ordered list of Shader Graph mutation operations to apply atomically against one '.shadergraph' asset.")]
    public class ShaderGraphBatchInput
    {
        [Description("Ordered operations to execute. Each envelope must set exactly one typed operation field.")]
        public List<ShaderGraphBatchOperationInput>? Operations { get; set; }

        [Description("When true, abort the batch on the first operation failure and roll the .shadergraph file back to its pre-batch content. When false, persist whatever succeeded and surface per-op errors in the response. Default: true.")]
        public bool? StopOnError { get; set; }

        [Description("Controls how much post-batch detail the response carries. Default: Summary. See ShaderGraphResponseMode for the per-mode contract.")]
        public ShaderGraphResponseMode? ResponseMode { get; set; }
    }

    [Description("One operation in a Shader Graph batch. Set Kind plus exactly one matching payload. Operations execute in declaration order against the live asset.")]
    public class ShaderGraphBatchOperationInput
    {
        [Description("Operation kind. Supported values: addNode, updateNodeSettings, deleteNode, addProperty, updateProperty, deleteProperty, addPropertyNode, connectEdge, updateNodePosition, setSettings, setBlocks, setOutputs.")]
        public string? Kind { get; set; }

        [Description("Batch-local alias to assign to the object created by this op (addNode, addProperty, addPropertyNode). Other op kinds ignore Alias. Aliases stay in scope only within the current batch.")]
        public string? Alias { get; set; }

        [Description("Operation payload for kind=addNode.")]
        public ShaderGraphAddNodeInput? AddNode { get; set; }

        [Description("Operation payload for kind=updateNodeSettings.")]
        public ShaderGraphUpdateNodeSettingsInput? UpdateNodeSettings { get; set; }

        [Description("Operation payload for kind=deleteNode.")]
        public ShaderGraphDeleteNodeInput? DeleteNode { get; set; }

        [Description("Operation payload for kind=addProperty.")]
        public ShaderGraphAddPropertyInput? AddProperty { get; set; }

        [Description("Operation payload for kind=updateProperty.")]
        public ShaderGraphPropertyUpdateInput? UpdateProperty { get; set; }

        [Description("Operation payload for kind=deleteProperty.")]
        public ShaderGraphDeletePropertyInput? DeleteProperty { get; set; }

        [Description("Operation payload for kind=addPropertyNode.")]
        public ShaderGraphAddPropertyNodeInput? AddPropertyNode { get; set; }

        [Description("Operation payload for kind=connectEdge.")]
        public ShaderGraphConnectEdgeInput? ConnectEdge { get; set; }

        [Description("Operation payload for kind=updateNodePosition.")]
        public ShaderGraphUpdateNodePositionInput? UpdateNodePosition { get; set; }

        [Description("Operation payload for kind=setSettings. Applies a narrow allowlist of graph + URP target settings.")]
        public ShaderGraphSettingsUpdateInput? SetSettings { get; set; }

        [Description("Operation payload for kind=setBlocks. Replaces the supported built-in block stack for one context (vertex or fragment).")]
        public ShaderGraphSetBlocksInput? SetBlocks { get; set; }

        [Description("Operation payload for kind=setOutputs. Declares the output port contract of a Sub Graph's SubGraphOutputNode. Only valid on '.shadersubgraph' assets.")]
        public ShaderGraphSetSubGraphOutputsInput? SetOutputs { get; set; }
    }

    [Description("Consolidated result of a Shader Graph batch invocation.")]
    public class ShaderGraphBatchResultData
    {
        [Description("Per-operation summaries, in the same order they were submitted.")]
        public List<ShaderGraphBatchOperationResultData>? Operations { get; set; }

        [Description("Compact post-batch graph summary. ShaderResolved, HasErrors, NodeCount, EdgeCount, plus filtered error/warning diagnostics.")]
        public ShaderGraphSummaryData? GraphSummary { get; set; }

        [Description("Aliases registered during the batch, mapped to the serialized object ids they ultimately resolved to. Useful when the caller wants to keep using those ids in a follow-up call.")]
        public Dictionary<string, string>? AliasMap { get; set; }

        [Description("Number of operations that ran to completion before the batch stopped.")]
        public int CompletedOperationCount { get; set; }

        [Description("True when the batch ran every supplied op without error; false when one or more ops failed (or were skipped due to stopOnError=true).")]
        public bool Success { get; set; }

        [Description("The response mode that produced this result. Echoed so the caller can confirm the contract that was applied.")]
        public ShaderGraphResponseMode ResponseMode { get; set; }

        [Description("Selection projection of the post-batch graph scoped to the nodes touched by this batch. Populated only when ResponseMode = Selection. Uses the same shape as assets-shadergraph-query-structure.")]
        public ShaderGraphQueryStructureData? Selection { get; set; }

        [Description("Full read-only post-batch graph structure. Populated only when ResponseMode = Full. Equivalent to calling assets-shadergraph-get-structure after the batch.")]
        public ShaderGraphStructureData? Structure { get; set; }
    }

    [Description("Per-operation result entry inside a Shader Graph batch response.")]
    public class ShaderGraphBatchOperationResultData
    {
        [Description("Zero-based index of this op in the submitted batch.")]
        public int Index { get; set; }

        [Description("Operation kind copied from the input.")]
        public string? Kind { get; set; }

        [Description("Alias the input assigned to the produced object, when applicable.")]
        public string? Alias { get; set; }

        [Description("True when the op ran without error.")]
        public bool Success { get; set; }

        [Description("Error message when Success is false.")]
        public string? Error { get; set; }

        [Description("Serialized object id of the object the op produced or targeted (node id for node ops, property id for property ops). Empty for ops that do not produce or target a single object id.")]
        public string? ObjectId { get; set; }

        [Description("Stable identifier of the op's operation tag, e.g. 'add', 'updateSettings', 'addProperty', 'connectEdge'. Mirrors the single-op tool's Operation field.")]
        public string? Operation { get; set; }

        [Description("Mutation diff fields actually touched by this op, mirroring the single-op tool's ChangedFields.")]
        public List<string>? ChangedFields { get; set; }
    }
}
