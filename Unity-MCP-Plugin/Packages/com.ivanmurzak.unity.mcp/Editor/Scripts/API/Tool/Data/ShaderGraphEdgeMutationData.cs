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

    [Description("Structured input for reconnecting an existing Shader Graph edge to a new endpoint.")]
    public class ShaderGraphReconnectEdgeInput
    {
        [Description("Serialized object id of the existing output node.")]
        public string? ExistingOutputNodeObjectId { get; set; }

        [Description("Serialized object id of the existing output slot attached to existingOutputNodeObjectId.")]
        public string? ExistingOutputSlotObjectId { get; set; }

        [Description("Serialized object id of the existing input node.")]
        public string? ExistingInputNodeObjectId { get; set; }

        [Description("Serialized object id of the existing input slot attached to existingInputNodeObjectId.")]
        public string? ExistingInputSlotObjectId { get; set; }

        [Description("Serialized object id of the new output node. Optional if the output side should stay unchanged.")]
        public string? NewOutputNodeObjectId { get; set; }

        [Description("Serialized object id of the new output slot attached to newOutputNodeObjectId. Optional if the output side should stay unchanged.")]
        public string? NewOutputSlotObjectId { get; set; }

        [Description("Serialized object id of the new input node. Optional if the input side should stay unchanged.")]
        public string? NewInputNodeObjectId { get; set; }

        [Description("Serialized object id of the new input slot attached to newInputNodeObjectId. Optional if the input side should stay unchanged.")]
        public string? NewInputSlotObjectId { get; set; }

        [Description("When true, automatically disconnect any other incoming edge already attached to the new target input before creating the reconnected edge. Default: false")]
        public bool? ReplaceExistingInputConnection { get; set; }
    }

    [Description("Structured input for rerouting every outgoing edge from one Shader Graph output slot to another output slot.")]
    public class ShaderGraphRerouteOutputSlotInput
    {
        [Description("Serialized object id of the existing output node whose outgoing edges should be moved.")]
        public string? ExistingOutputNodeObjectId { get; set; }

        [Description("Serialized object id of the existing output slot attached to existingOutputNodeObjectId.")]
        public string? ExistingOutputSlotObjectId { get; set; }

        [Description("Serialized object id of the new output node that should feed the existing downstream inputs.")]
        public string? NewOutputNodeObjectId { get; set; }

        [Description("Serialized object id of the new output slot attached to newOutputNodeObjectId.")]
        public string? NewOutputSlotObjectId { get; set; }
    }

    [Description("Result of mutating Shader Graph edges and re-importing the graph.")]
    public class ShaderGraphEdgeMutationResultData
    {
        [Description("Snapshot of the edge that was connected or disconnected.")]
        public ShaderGraphEdgeDefinitionData? Edge { get; set; }

        [Description("Snapshots of every edge that was connected by the mutation.")]
        public List<ShaderGraphEdgeDefinitionData>? Edges { get; set; }

        [Description("Snapshot of the previously connected incoming edge that was removed during a replace operation, if any.")]
        public ShaderGraphEdgeDefinitionData? RemovedEdge { get; set; }

        [Description("Snapshots of every edge removed during the mutation, including the primary disconnected edge and any replaced incoming edge.")]
        public List<ShaderGraphEdgeDefinitionData>? RemovedEdges { get; set; }

        [Description("Updated read-only graph structure after the mutation.")]
        public ShaderGraphStructureData? Structure { get; set; }

        [Description("Post-import Shader Graph summary and diagnostics.")]
        public ShaderGraphData? Graph { get; set; }

        [Description("List of edge fields that actually changed.")]
        public List<string>? ChangedFields { get; set; }
    }
}
