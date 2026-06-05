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

    [Description("Result of adding an allowlisted Shader Graph node and re-importing the graph.")]
    public class ShaderGraphNodeMutationResultData
    {
        [Description("Snapshot of the created node after the mutation.")]
        public ShaderGraphNodeDefinitionData? Node { get; set; }

        [Description("Updated read-only graph structure after the mutation.")]
        public ShaderGraphStructureData? Structure { get; set; }

        [Description("Post-import Shader Graph summary and diagnostics.")]
        public ShaderGraphData? Graph { get; set; }

        [Description("List of node fields that actually changed.")]
        public List<string>? ChangedFields { get; set; }
    }
}
