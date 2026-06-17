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
    public class ShaderGraphData
    {
        [Description("Reference to the imported Shader Graph asset for future operations.")]
        public AssetObjectRef? Reference { get; set; }

        [Description("Project-relative asset path of the Shader Graph source file.")]
        public string? AssetPath { get; set; }

        [Description("Source file extension, typically '.shadergraph'.")]
        public string? SourceFileExtension { get; set; }

        [Description("Whether the Shader Graph source file was successfully parsed.")]
        public bool SourceParsed { get; set; }

        [Description("The importer type handling this asset, if available.")]
        public string? ImporterType { get; set; }

        [Description("Compiled shader name exposed by Unity after import.")]
        public string? ShaderName { get; set; }

        [Description("Whether Unity resolved the imported Shader Graph to a Shader asset.")]
        public bool ShaderResolved { get; set; }

        [Description("Whether the compiled shader is supported on the current GPU and platform.")]
        public bool IsSupported { get; set; }

        [Description("Whether the compiled shader currently reports any errors.")]
        public bool HasErrors { get; set; }

        [Description("Shader Graph source version from the graph file.")]
        public int? GraphVersion { get; set; }

        [Description("Root serialized graph type stored in the source file.")]
        public string? GraphType { get; set; }

        [Description("Shader menu/category path declared by the graph source.")]
        public string? ShaderMenuPath { get; set; }

        [Description("Number of graph properties declared in the source file.")]
        public int GraphPropertyCount { get; set; }

        [Description("Number of keywords declared in the source file.")]
        public int KeywordCount { get; set; }

        [Description("Number of dropdowns declared in the source file.")]
        public int DropdownCount { get; set; }

        [Description("Number of nodes declared in the source file.")]
        public int NodeCount { get; set; }

        [Description("Number of edges declared in the source file.")]
        public int EdgeCount { get; set; }

        [Description("Number of graph groups declared in the source file.")]
        public int GroupCount { get; set; }

        [Description("Number of sticky notes declared in the source file.")]
        public int StickyNoteCount { get; set; }

        [Description("Number of sub-data blocks declared in the source file.")]
        public int SubDataCount { get; set; }

        [Description("Number of active targets declared in the source file.")]
        public int ActiveTargetCount { get; set; }

        [Description("Resolved type names of active targets declared in the source file.")]
        public List<string>? ActiveTargetTypes { get; set; }

        [Description("Number of compiled shader properties exposed after import.")]
        public int ShaderPropertyCount { get; set; }

        [Description("Number of compiled shader passes exposed after import.")]
        public int PassCount { get; set; }

        [Description("Render queue value of the compiled shader.")]
        public int RenderQueue { get; set; }

        [Description("RenderType tag value from the first compiled pass, if available.")]
        public string? RenderType { get; set; }

        [Description("Compiled shader properties exposed after import. Null if not requested.")]
        public List<ShaderPropertyData>? Properties { get; set; }

        [Description("Compiled shader messages reported by Unity. Null if not requested.")]
        public List<ShaderMessageData>? Messages { get; set; }

        [Description("Graph and import diagnostics. Null if not requested.")]
        public List<ShaderGraphDiagnosticData>? Diagnostics { get; set; }
    }

    [Description("Compact post-import summary returned by Shader Graph mutation tools by default. Use the includeStructure / includeGraph flags to opt in to the full Structure or Graph payloads.")]
    public class ShaderGraphSummaryData
    {
        [Description("Whether Unity resolved the imported Shader Graph to a Shader asset after the mutation.")]
        public bool ShaderResolved { get; set; }

        [Description("Whether the compiled shader currently reports any errors after the mutation.")]
        public bool HasErrors { get; set; }

        [Description("Number of nodes declared in the source file after the mutation.")]
        public int NodeCount { get; set; }

        [Description("Number of edges declared in the source file after the mutation.")]
        public int EdgeCount { get; set; }

        [Description("Filtered graph and import diagnostics (errors and warnings only). Informational success diagnostics are omitted from the slim summary.")]
        public List<ShaderGraphDiagnosticData>? Diagnostics { get; set; }
    }
}
