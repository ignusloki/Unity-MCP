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
    [Description("Graph-level Shader Graph settings plus currently supported target settings.")]
    public class ShaderGraphSettingsData
    {
        [Description("Reference to the imported Shader Graph asset for future operations.")]
        public AssetObjectRef? Reference { get; set; }

        [Description("Project-relative asset path of the Shader Graph source file.")]
        public string? AssetPath { get; set; }

        [Description("Whether the Shader Graph source file was successfully parsed.")]
        public bool SourceParsed { get; set; }

        [Description("Parse error if source parsing failed.")]
        public string? ParseError { get; set; }

        [Description("Root serialized graph type stored in the source file.")]
        public string? GraphType { get; set; }

        [Description("Shader Graph source version from the graph file.")]
        public int? GraphVersion { get; set; }

        [Description("Resolved active target type names declared by the graph.")]
        public List<string>? ActiveTargetTypes { get; set; }

        [Description("Root graph settings serialized on the main GraphData object.")]
        public ShaderGraphRootSettingsData? Graph { get; set; }

        [Description("Currently supported Universal Render Pipeline target settings, if present.")]
        public ShaderGraphUniversalTargetSettingsData? UniversalTarget { get; set; }

        [Description("Warnings for active targets or settings that are present but not yet exposed by this tool.")]
        public List<string>? Warnings { get; set; }
    }

    public class ShaderGraphRootSettingsData
    {
        [Description("Shader menu/category path used as the compiled shader name prefix.")]
        public string? ShaderMenuPath { get; set; }

        [Description("Graph precision name. Values: single, graph, half.")]
        public string? GraphPrecision { get; set; }

        [Description("Raw serialized graph precision enum value.")]
        public int? GraphPrecisionValue { get; set; }

        [Description("Preview mode name. Values: inherit, preview2d, preview3d.")]
        public string? PreviewMode { get; set; }

        [Description("Raw serialized preview mode enum value.")]
        public int? PreviewModeValue { get; set; }
    }

    public class ShaderGraphUniversalTargetSettingsData
    {
        [Description("Serialized object id of the Universal target.")]
        public string? ObjectId { get; set; }

        [Description("Serialized object type of the Universal target.")]
        public string? Type { get; set; }

        [Description("Whether the target allows material overrides.")]
        public bool? AllowMaterialOverride { get; set; }

        [Description("Surface type name. Values: opaque, transparent.")]
        public string? SurfaceType { get; set; }

        [Description("Raw serialized surface type enum value.")]
        public int? SurfaceTypeValue { get; set; }

        [Description("Alpha mode name. Values: alpha, premultiply, additive, multiply.")]
        public string? AlphaMode { get; set; }

        [Description("Raw serialized alpha mode enum value.")]
        public int? AlphaModeValue { get; set; }

        [Description("Render face name. Values: front, back, both.")]
        public string? RenderFace { get; set; }

        [Description("Raw serialized render face enum value.")]
        public int? RenderFaceValue { get; set; }

        [Description("Whether alpha clipping is enabled.")]
        public bool? AlphaClip { get; set; }

        [Description("Whether the target casts shadows.")]
        public bool? CastShadows { get; set; }

        [Description("Whether the target receives shadows.")]
        public bool? ReceiveShadows { get; set; }

        [Description("Whether LOD cross-fade support is enabled.")]
        public bool? SupportsLodCrossFade { get; set; }
    }

    [Description("Structured input for mutating a narrow allowlist of Shader Graph settings.")]
    public class ShaderGraphSettingsUpdateInput
    {
        [Description("Optional root graph settings to mutate.")]
        public ShaderGraphRootSettingsUpdateInput? Graph { get; set; }

        [Description("Optional Universal Render Pipeline target settings to mutate.")]
        public ShaderGraphUniversalTargetSettingsUpdateInput? UniversalTarget { get; set; }
    }

    public class ShaderGraphRootSettingsUpdateInput
    {
        [Description("New shader menu/category path prefix for the compiled shader name. Example: 'Unlit/MyCategory'.")]
        public string? ShaderMenuPath { get; set; }

        [Description("New graph precision. Supported values: single, graph, half.")]
        public string? GraphPrecision { get; set; }

        [Description("New preview mode. Supported values: inherit, preview2d, preview3d.")]
        public string? PreviewMode { get; set; }
    }

    public class ShaderGraphUniversalTargetSettingsUpdateInput
    {
        [Description("Whether to allow material overrides.")]
        public bool? AllowMaterialOverride { get; set; }

        [Description("New surface type. Supported values: opaque, transparent.")]
        public string? SurfaceType { get; set; }

        [Description("New alpha mode. Supported values: alpha, premultiply, additive, multiply.")]
        public string? AlphaMode { get; set; }

        [Description("New render face mode. Supported values: front, back, both.")]
        public string? RenderFace { get; set; }

        [Description("Whether alpha clipping should be enabled.")]
        public bool? AlphaClip { get; set; }

        [Description("Whether the target should cast shadows.")]
        public bool? CastShadows { get; set; }

        [Description("Whether the target should receive shadows.")]
        public bool? ReceiveShadows { get; set; }

        [Description("Whether LOD cross-fade support should be enabled.")]
        public bool? SupportsLodCrossFade { get; set; }
    }

    [Description("Result of mutating Shader Graph settings and re-importing the graph.")]
    public class ShaderGraphSettingsMutationResultData
    {
        [Description("Settings snapshot after the mutation was applied.")]
        public ShaderGraphSettingsData? Settings { get; set; }

        [Description("Post-import Shader Graph summary and diagnostics.")]
        public ShaderGraphData? Graph { get; set; }

        [Description("List of settings fields that actually changed.")]
        public List<string>? ChangedFields { get; set; }
    }
}
