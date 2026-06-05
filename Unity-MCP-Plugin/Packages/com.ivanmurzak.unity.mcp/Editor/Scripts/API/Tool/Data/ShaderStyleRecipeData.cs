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
    [Description("Declarative recipe describing the intended style of a generated Shader Graph material.")]
    public class ShaderStyleRecipeData
    {
        [Description("Human-readable style name.")]
        public string? StyleName { get; set; }

        [Description("Short natural-language description of the target look.")]
        public string? Description { get; set; }

        [Description("Target render pipeline. Epic 5 currently supports URP-oriented recipes.")]
        public string? RenderPipeline { get; set; }

        [Description("Logical style template identifier requested by the AI agent.")]
        public string? GraphTemplate { get; set; }

        [Description("Surface configuration.")]
        public ShaderStyleRecipeSurfaceData? Surface { get; set; }

        [Description("Palette configuration.")]
        public ShaderStyleRecipePaletteData? Palette { get; set; }

        [Description("Lighting intent.")]
        public ShaderStyleRecipeLightingData? Lighting { get; set; }

        [Description("Outline intent.")]
        public ShaderStyleRecipeOutlineData? Outline { get; set; }

        [Description("Texture and grain intent.")]
        public ShaderStyleRecipeTextureData? Texture { get; set; }

        [Description("Material-value intent.")]
        public ShaderStyleRecipeMaterialData? Material { get; set; }

        [Description("Free-form implementation notes for the AI agent and user.")]
        public List<string>? Notes { get; set; }
    }

    public class ShaderStyleRecipeSurfaceData
    {
        [Description("Surface type such as Opaque or Transparent.")]
        public string? Type { get; set; }

        [Description("Whether alpha clipping is requested.")]
        public bool AlphaClipping { get; set; }

        [Description("Whether double-sided rendering is requested.")]
        public bool DoubleSided { get; set; }
    }

    public class ShaderStyleRecipePaletteData
    {
        [Description("Primary palette colors as hex strings.")]
        public List<string>? BaseColors { get; set; }

        [Description("Shadow palette colors as hex strings.")]
        public List<string>? ShadowColors { get; set; }

        [Description("Highlight palette colors as hex strings.")]
        public List<string>? HighlightColors { get; set; }
    }

    public class ShaderStyleRecipeLightingData
    {
        [Description("Desired lighting mode such as unlit or toon.")]
        public string? Mode { get; set; }

        [Description("Requested discrete lighting steps.")]
        public int Steps { get; set; }

        [Description("Requested shadow softness in the range 0..1.")]
        public float ShadowSoftness { get; set; }

        [Description("Requested rim-light intensity in the range 0..1.")]
        public float RimLight { get; set; }
    }

    public class ShaderStyleRecipeOutlineData
    {
        [Description("Whether outlines are requested.")]
        public bool Enabled { get; set; }

        [Description("Outline color as a hex string.")]
        public string? Color { get; set; }

        [Description("Requested outline width.")]
        public float Width { get; set; }
    }

    public class ShaderStyleRecipeTextureData
    {
        [Description("Whether the tool should use a reference texture if one is available.")]
        public bool UseReferenceTexture { get; set; }

        [Description("Requested procedural noise amount in the range 0..1.")]
        public float NoiseAmount { get; set; }

        [Description("Requested brush-grain intensity in the range 0..1.")]
        public float BrushGrain { get; set; }
    }

    public class ShaderStyleRecipeMaterialData
    {
        [Description("Requested smoothness in the range 0..1.")]
        public float Smoothness { get; set; }

        [Description("Requested metallic value in the range 0..1.")]
        public float Metallic { get; set; }

        [Description("Requested normal-map strength.")]
        public float NormalStrength { get; set; }

        [Description("Requested emission strength.")]
        public float EmissionStrength { get; set; }
    }

    [Description("Result of creating a Shader Graph and Material from a declarative style recipe.")]
    public class ShaderStyleRecipeCreateResult
    {
        [Description("Normalized recipe after defaults and validation.")]
        public ShaderStyleRecipeData? Recipe { get; set; }

        [Description("Template identifier requested or normalized by the recipe.")]
        public string? RequestedTemplateId { get; set; }

        [Description("Template identifier actually used to create the graph.")]
        public string? ResolvedTemplateId { get; set; }

        [Description("Asset path of the template source used to create the graph.")]
        public string? TemplateAssetPath { get; set; }

        [Description("Created Shader Graph data.")]
        public ShaderGraphData? Graph { get; set; }

        [Description("Reference to the created Material asset.")]
        public AssetObjectRef? MaterialReference { get; set; }

        [Description("Asset path of the created Material.")]
        public string? MaterialAssetPath { get; set; }

        [Description("Shader name used by the created Material.")]
        public string? MaterialShaderName { get; set; }

        [Description("Material property names that were applied from the recipe.")]
        public List<string>? AppliedMaterialProperties { get; set; }

        [Description("Warnings about ignored, deferred, or unsupported recipe aspects.")]
        public List<string>? Warnings { get; set; }
    }
}
