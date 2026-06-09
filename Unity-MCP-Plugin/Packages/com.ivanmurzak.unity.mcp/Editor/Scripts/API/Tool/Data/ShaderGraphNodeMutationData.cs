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
    [Description("Structured input for moving an existing Shader Graph node by serialized node id.")]
    public class ShaderGraphUpdateNodePositionInput
    {
        [Description("Serialized object id of the node to move.")]
        public string? NodeObjectId { get; set; }

        [Description("New serialized X position for the node.")]
        public float? PositionX { get; set; }

        [Description("New serialized Y position for the node.")]
        public float? PositionY { get; set; }
    }

    [Description("Structured input for adding a safe allowlisted node to an existing Shader Graph.")]
    public class ShaderGraphAddPropertyNodeInput
    {
        [Description("Serialized object id of the blackboard property to expose as a node. Optional if propertyReferenceName is provided.")]
        public string? PropertyObjectId { get; set; }

        [Description("Effective property reference name to expose as a node, such as '_BaseColor'. Optional if propertyObjectId is provided.")]
        public string? PropertyReferenceName { get; set; }

        [Description("Serialized X position for the new Property node. Default: 0.")]
        public float? PositionX { get; set; }

        [Description("Serialized Y position for the new Property node. Default: 0.")]
        public float? PositionY { get; set; }
    }

    [Description("Structured input for adding a safe allowlisted Shader Graph node.")]
    public class ShaderGraphAddNodeInput
    {
        [Description("Allowlisted node type to create. Supported values: add, subtract, multiply, divide, lerp, oneMinus, split, combine, sampleTexture2D, tilingAndOffset, branch.")]
        public string? NodeType { get; set; }

        [Description("Serialized X position for the new node. Default: 0.")]
        public float? PositionX { get; set; }

        [Description("Serialized Y position for the new node. Default: 0.")]
        public float? PositionY { get; set; }
    }

    [Description("Structured input for deleting an existing Shader Graph node by serialized node id.")]
    public class ShaderGraphDeleteNodeInput
    {
        [Description("Serialized object id of the node to delete.")]
        public string? NodeObjectId { get; set; }
    }

    [Description("Result of adding an allowlisted Shader Graph node and re-importing the graph.")]
    public class ShaderGraphNodeMutationResultData
    {
        [Description("Snapshot of the affected node. For add operations this is the created node after import; for delete operations this is the deleted node before removal.")]
        public ShaderGraphNodeDefinitionData? Node { get; set; }

        [Description("Updated read-only graph structure after the mutation.")]
        public ShaderGraphStructureData? Structure { get; set; }

        [Description("Post-import Shader Graph summary and diagnostics.")]
        public ShaderGraphData? Graph { get; set; }

        [Description("Number of connected edges that were removed automatically while deleting the node, if applicable.")]
        public int? RemovedEdgeCount { get; set; }

        [Description("List of node fields that actually changed.")]
        public List<string>? ChangedFields { get; set; }
    }
}
