/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Unity-MCP)    │
│  Copyright (c) 2025 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
└──────────────────────────────────────────────────────────────────┘
*/

#nullable enable
using System.Collections.Generic;
using System.ComponentModel;

namespace AIGD
{
    [Description("Filters for the assets-shadergraph-query-structure tool. Leave a filter null to disable it. When no filter is provided, the result mirrors a normal get-structure read but adds a Stats summary.")]
    public class ShaderGraphQueryStructureInput
    {
        [Description("When true, return only the Stats summary (node/edge/property/category/target counts and parse status). Overrides every other filter.")]
        public bool? StatsOnly { get; set; }

        [Description("When true, return only Properties and Categories — Nodes, Edges, Targets, and Contexts are dropped. Use this to inspect the blackboard without paying for the full graph.")]
        public bool? PropertiesOnly { get; set; }

        [Description("Restrict the returned Nodes list to nodes whose ObjectId is in this set. When null, every node passes this filter.")]
        public List<string>? NodeObjectIds { get; set; }

        [Description("Restrict the returned Nodes list to nodes whose Type contains any of these substrings (ordinal, case-sensitive). For example 'SimpleNoise' matches 'UnityEditor.ShaderGraph.SimpleNoiseNode'. When null, every node passes this filter.")]
        public List<string>? NodeTypeSubstrings { get; set; }

        [Description("Restrict the returned Nodes list to nodes whose display Name matches any of these values exactly. When null, every node passes this filter.")]
        public List<string>? NodeDisplayNames { get; set; }

        [Description("When false, the Slots list on every returned node is stripped. Default is true. Use false to cut node payloads roughly in half when only positions or types matter.")]
        public bool? IncludeSlots { get; set; }

        [Description("When false, every typed *NodeSettings field on returned nodes (Multiply, Sample Texture 2D, Smoothstep, etc.) is stripped. Default is true.")]
        public bool? IncludeNodeSettings { get; set; }

        [Description("When false, the Edges list is dropped from the response. Default is true.")]
        public bool? IncludeEdges { get; set; }

        [Description("Restrict the returned Edges list to edges where OutputNodeId or InputNodeId is in this set. When null, every edge passes this filter. Has no effect when IncludeEdges is false.")]
        public List<string>? EdgesTouchingNodeIds { get; set; }

        [Description("When false, Targets, VertexContext, and FragmentContext are dropped from the response. Default is true.")]
        public bool? IncludeTargets { get; set; }
    }

    [Description("Counts and parse-status summary returned by the assets-shadergraph-query-structure tool. Lightweight token-cheap snapshot of the graph.")]
    public class ShaderGraphQueryStatsData
    {
        [Description("Whether the Shader Graph source file was successfully parsed.")]
        public bool SourceParsed { get; set; }

        [Description("Parse error if source parsing failed, otherwise null.")]
        public string? ParseError { get; set; }

        [Description("Total node count in the graph (pre-filter).")]
        public int NodeCount { get; set; }

        [Description("Total edge count in the graph (pre-filter).")]
        public int EdgeCount { get; set; }

        [Description("Total blackboard property count in the graph.")]
        public int PropertyCount { get; set; }

        [Description("Total blackboard category count in the graph.")]
        public int CategoryCount { get; set; }

        [Description("Total active target count in the graph.")]
        public int TargetCount { get; set; }
    }

    [Description("Result of the assets-shadergraph-query-structure tool. The Stats field is always populated; the projected lists are populated only when the corresponding filters include them.")]
    public class ShaderGraphQueryStructureData
    {
        [Description("Reference to the imported Shader Graph asset for future operations.")]
        public AssetObjectRef? Reference { get; set; }

        [Description("Project-relative asset path of the Shader Graph source file.")]
        public string? AssetPath { get; set; }

        [Description("Counts and parse-status summary. Always populated.")]
        public ShaderGraphQueryStatsData? Stats { get; set; }

        [Description("Projected blackboard properties. Null when PropertiesOnly is false and other filters dropped them, or when StatsOnly is true.")]
        public List<ShaderGraphPropertyDefinitionData>? Properties { get; set; }

        [Description("Projected blackboard categories. Null when StatsOnly is true.")]
        public List<ShaderGraphCategoryDefinitionData>? Categories { get; set; }

        [Description("Projected nodes after filtering. Null when StatsOnly or PropertiesOnly is true.")]
        public List<ShaderGraphNodeDefinitionData>? Nodes { get; set; }

        [Description("Projected edges after filtering. Null when StatsOnly is true, PropertiesOnly is true, or IncludeEdges is false.")]
        public List<ShaderGraphEdgeDefinitionData>? Edges { get; set; }

        [Description("Projected active targets. Null when StatsOnly is true, PropertiesOnly is true, or IncludeTargets is false.")]
        public List<ShaderGraphTargetDefinitionData>? Targets { get; set; }

        [Description("Vertex context block references. Null when StatsOnly is true, PropertiesOnly is true, or IncludeTargets is false.")]
        public ShaderGraphContextDefinitionData? VertexContext { get; set; }

        [Description("Fragment context block references. Null when StatsOnly is true, PropertiesOnly is true, or IncludeTargets is false.")]
        public ShaderGraphContextDefinitionData? FragmentContext { get; set; }
    }
}
