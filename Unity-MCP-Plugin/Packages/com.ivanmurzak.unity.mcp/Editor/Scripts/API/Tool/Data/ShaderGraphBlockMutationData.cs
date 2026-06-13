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
    [Description("Structured input for setting the ordered block stack of a Shader Graph vertex or fragment context.")]
    public class ShaderGraphSetBlocksInput
    {
        [Description("Context to mutate. Supported values: vertex, fragment.")]
        public string? Context { get; set; }

        [Description("Ordered built-in block descriptors or aliases to keep in the context. This is a full replacement list for supported blocks in the selected context.")]
        public List<string>? Blocks { get; set; }

        [Description("When true, connected removed blocks are allowed and their edges are removed. Default: false")]
        public bool? AllowRemovingConnectedBlocks { get; set; }
    }

    [Description("Result of mutating a Shader Graph block stack and re-importing the graph.")]
    public class ShaderGraphBlockMutationResultData
    {
        [Description("Stable operation identifier.")]
        public string? Operation { get; set; }

        [Description("Mutated context name. Values: vertex, fragment.")]
        public string? Context { get; set; }

        [Description("Ordered built-in block descriptors after mutation.")]
        public List<string>? BlockDescriptors { get; set; }

        [Description("Serialized node ids created by the mutation.")]
        public List<string>? CreatedBlockNodeIds { get; set; }

        [Description("Serialized node ids removed by the mutation.")]
        public List<string>? RemovedBlockNodeIds { get; set; }

        [Description("Number of connected edges removed because allowRemovingConnectedBlocks was true.")]
        public int RemovedEdgeCount { get; set; }

        [Description("Updated read-only graph structure after the mutation.")]
        public ShaderGraphStructureData? Structure { get; set; }

        [Description("Post-import Shader Graph summary and diagnostics.")]
        public ShaderGraphData? Graph { get; set; }

        [Description("List of block stack fields that actually changed.")]
        public List<string>? ChangedFields { get; set; }
    }
}
