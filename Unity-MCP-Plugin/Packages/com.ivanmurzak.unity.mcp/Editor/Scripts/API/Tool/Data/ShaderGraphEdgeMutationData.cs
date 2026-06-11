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
    [Description("Structured input for connecting two existing Shader Graph slots.")]
    public class ShaderGraphConnectEdgeInput
    {
        [Description("Serialized object id of the output node.")]
        public string? OutputNodeObjectId { get; set; }

        [Description("Serialized object id of the output slot attached to outputNodeObjectId.")]
        public string? OutputSlotObjectId { get; set; }

        [Description("Serialized object id of the input node.")]
        public string? InputNodeObjectId { get; set; }

        [Description("Serialized object id of the input slot attached to inputNodeObjectId.")]
        public string? InputSlotObjectId { get; set; }

        [Description("When true, automatically disconnect the current incoming edge on the target input slot before creating the new edge. Default: false")]
        public bool? ReplaceExistingInputConnection { get; set; }
    }

    [Description("Structured input for disconnecting an existing Shader Graph edge.")]
    public class ShaderGraphDisconnectEdgeInput
    {
        [Description("Serialized object id of the output node.")]
        public string? OutputNodeObjectId { get; set; }

        [Description("Serialized object id of the output slot attached to outputNodeObjectId.")]
        public string? OutputSlotObjectId { get; set; }

        [Description("Serialized object id of the input node.")]
        public string? InputNodeObjectId { get; set; }

        [Description("Serialized object id of the input slot attached to inputNodeObjectId.")]
        public string? InputSlotObjectId { get; set; }
    }

    [Description("Result of mutating Shader Graph edges and re-importing the graph.")]
    public class ShaderGraphEdgeMutationResultData
    {
        [Description("Snapshot of the edge that was connected or disconnected.")]
        public ShaderGraphEdgeDefinitionData? Edge { get; set; }

        [Description("Snapshot of the previously connected incoming edge that was removed during a replace operation, if any.")]
        public ShaderGraphEdgeDefinitionData? RemovedEdge { get; set; }

        [Description("Updated read-only graph structure after the mutation.")]
        public ShaderGraphStructureData? Structure { get; set; }

        [Description("Post-import Shader Graph summary and diagnostics.")]
        public ShaderGraphData? Graph { get; set; }

        [Description("List of edge fields that actually changed.")]
        public List<string>? ChangedFields { get; set; }
    }
}
