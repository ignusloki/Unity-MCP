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
using System.ComponentModel;

namespace AIGD
{
    [Description("Selector for an existing or batch-created Shader Graph node. Set exactly one of the fields; ObjectId wins when present.")]
    public class ShaderGraphNodeRef
    {
        [Description("Serialized object id of the node. Takes precedence over Alias and DisplayName when present.")]
        public string? ObjectId { get; set; }

        [Description("Batch-local alias assigned to the node when it was created earlier in the same batch via the addNode operation.")]
        public string? Alias { get; set; }

        [Description("Display name of the node, e.g. 'Simple Noise' or 'Multiply'. Resolves against the first matching node in the current graph when ObjectId and Alias are not provided.")]
        public string? DisplayName { get; set; }
    }

    [Description("Selector for an existing Shader Graph slot. Set exactly one of the fields; ObjectId wins when present, otherwise DisplayName resolves against the parent Node selector.")]
    public class ShaderGraphSlotRef
    {
        [Description("Serialized object id of the slot. Takes precedence over Node + DisplayName when present.")]
        public string? ObjectId { get; set; }

        [Description("Parent node selector. Required when resolving by DisplayName.")]
        public ShaderGraphNodeRef? Node { get; set; }

        [Description("Slot display name on the parent node, e.g. 'UV', 'Out', 'Edge1'. Case-sensitive and matched after Node resolution.")]
        public string? DisplayName { get; set; }
    }

    [Description("Selector for an existing or batch-created Shader Graph blackboard property. Set exactly one of the fields; ObjectId wins when present.")]
    public class ShaderGraphPropertyRef
    {
        [Description("Serialized object id of the property. Takes precedence over the other fields when present.")]
        public string? ObjectId { get; set; }

        [Description("Batch-local alias assigned to the property when it was created earlier in the same batch via the addProperty operation.")]
        public string? Alias { get; set; }

        [Description("Effective reference name of the property, e.g. '_BaseColor' or '_Texture'.")]
        public string? ReferenceName { get; set; }

        [Description("Display name of the property as shown in the blackboard, e.g. 'Color' or 'Base Map'.")]
        public string? DisplayName { get; set; }
    }
}
