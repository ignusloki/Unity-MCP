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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using AIGD;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Json;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.Unity.MCP.Editor.Utils;
using UnityEditor;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderGraphCreateFromStyleRecipeToolId = "assets-shadergraph-create-from-style-recipe";

        static readonly JsonSerializerOptions StyleRecipeJsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        static readonly HashSet<string> TopLevelStyleRecipeFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "styleName",
            "description",
            "renderPipeline",
            "graphTemplate",
            "surface",
            "palette",
            "lighting",
            "outline",
            "texture",
            "material",
            "notes"
        };

        static readonly HashSet<string> SurfaceFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "type",
            "alphaClipping",
            "doubleSided"
        };

        static readonly HashSet<string> PaletteFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "baseColors",
            "shadowColors",
            "highlightColors"
        };

        static readonly HashSet<string> LightingFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "mode",
            "steps",
            "shadowSoftness",
            "rimLight"
        };

        static readonly HashSet<string> OutlineFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "enabled",
            "color",
            "width"
        };

        static readonly HashSet<string> TextureFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "useReferenceTexture",
            "noiseAmount",
            "brushGrain"
        };

        static readonly HashSet<string> MaterialFields = new(StringComparer.OrdinalIgnoreCase)
        {
            "smoothness",
            "metallic",
            "normalStrength",
            "emissionStrength"
        };

        sealed class ResolvedStyleTemplate
        {
            public string RequestedId { get; set; } = "unlit-simple";
            public string ResolvedId { get; set; } = "unlit-simple";
            public string AssetPath { get; set; } = DefaultTemplateAssetPath;
        }

        [AiTool
        (
            AssetsShaderGraphCreateFromStyleRecipeToolId,
            Title = "Assets / Shader Graph / Create From Style Recipe"
        )]
        [AiSkillDescription("Create a Shader Graph and Material from a validated, declarative style recipe JSON.")]
        [AiSkillBody("Create a Shader Graph and Material from a validated style recipe. " +
            "The recipe is declarative JSON, not raw Shader Graph source. Unknown or not-yet-applied fields return warnings instead of mutating arbitrary graph internals.\n\n" +
            "## Inputs\n\n" +
            "- `styleRecipe` — JSON object or JSON string matching the style recipe schema.\n" +
            "- `graphAssetPath` — destination `.shadergraph` path under `Assets/`.\n" +
            "- `materialAssetPath` — destination `.mat` path under `Assets/`.\n" +
            "- `overwrite` — when true, replace existing generated assets.\n\n" +
            "Current Epic 5 behavior validates the full recipe, creates the graph from a safe template, creates a material from the graph, applies the first base palette color if the shader exposes one, and returns warnings for recipe fields deferred to later template work.")]
        [Description("Create a Shader Graph and Material from a validated, declarative style recipe JSON.")]
        public ShaderStyleRecipeCreateResult CreateFromStyleRecipe
        (
            [JsonStringOrObject]
            [Description("Style recipe JSON object or JSON string.")]
            string styleRecipe,
            [Description("Destination Shader Graph asset path. Must start with 'Assets/' and end with '.shadergraph'.")]
            string graphAssetPath,
            [Description("Destination Material asset path. Must start with 'Assets/' and end with '.mat'.")]
            string materialAssetPath,
            [Description("When true, replace existing generated assets. Default: false")]
            bool? overwrite = false
        )
        {
            var warnings = new List<string>();
            var recipe = ParseStyleRecipe(styleRecipe, warnings);
            var resolvedTemplate = ResolveStyleTemplate(recipe, warnings);
            var allowOverwrite = overwrite ?? false;

            return MainThread.Instance.Run(() =>
            {
                ValidateOutputAssetPaths(graphAssetPath, materialAssetPath, allowOverwrite);

                var graphData = Create(
                    assetPath: graphAssetPath,
                    templateAssetPath: resolvedTemplate.AssetPath,
                    overwrite: allowOverwrite);

                var graphReference = graphData.Reference ?? new AssetObjectRef(graphAssetPath);
                var materialReference = CreateMaterial(
                    assetRef: graphReference,
                    materialAssetPath: materialAssetPath,
                    overwrite: allowOverwrite);

                var material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
                if (material == null)
                    throw new Exception($"Material asset was not created at path '{materialAssetPath}'.");

                var appliedMaterialProperties = new List<string>();
                ApplyRecipeToMaterial(material, recipe, warnings, appliedMaterialProperties);

                EditorUtility.SetDirty(material);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                EditorUtils.RepaintAllEditorWindows();

                return new ShaderStyleRecipeCreateResult
                {
                    Recipe = recipe,
                    RequestedTemplateId = resolvedTemplate.RequestedId,
                    ResolvedTemplateId = resolvedTemplate.ResolvedId,
                    TemplateAssetPath = resolvedTemplate.AssetPath,
                    Graph = graphData,
                    MaterialReference = materialReference,
                    MaterialAssetPath = materialAssetPath,
                    MaterialShaderName = material.shader != null ? material.shader.name : null,
                    AppliedMaterialProperties = appliedMaterialProperties.Count == 0 ? null : appliedMaterialProperties,
                    Warnings = warnings.Count == 0
                        ? null
                        : warnings
                            .Distinct(StringComparer.Ordinal)
                            .ToList()
                };
            });
        }

        static ShaderStyleRecipeData ParseStyleRecipe(string styleRecipe, List<string> warnings)
        {
            if (string.IsNullOrWhiteSpace(styleRecipe))
                throw new ArgumentException("Style recipe JSON is required.", nameof(styleRecipe));

            try
            {
                using var document = JsonDocument.Parse(styleRecipe);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    throw new ArgumentException("Style recipe JSON must be an object at the root.", nameof(styleRecipe));

                ValidateKnownStyleRecipeFields(document.RootElement, warnings);

                var recipe = System.Text.Json.JsonSerializer.Deserialize<ShaderStyleRecipeData>(styleRecipe, StyleRecipeJsonOptions)
                    ?? throw new ArgumentException("Style recipe JSON could not be deserialized.", nameof(styleRecipe));

                NormalizeStyleRecipe(recipe);
                ValidateStyleRecipe(recipe);
                return recipe;
            }
            catch (JsonException ex)
            {
                throw new ArgumentException($"Style recipe JSON is invalid: {ex.Message}", nameof(styleRecipe), ex);
            }
        }

        static void NormalizeStyleRecipe(ShaderStyleRecipeData recipe)
        {
            recipe.StyleName = string.IsNullOrWhiteSpace(recipe.StyleName)
                ? "Generated Style Shader"
                : recipe.StyleName.Trim();

            recipe.Description = string.IsNullOrWhiteSpace(recipe.Description)
                ? null
                : recipe.Description.Trim();

            recipe.RenderPipeline = string.IsNullOrWhiteSpace(recipe.RenderPipeline)
                ? "URP"
                : recipe.RenderPipeline.Trim();

            recipe.GraphTemplate = string.IsNullOrWhiteSpace(recipe.GraphTemplate)
                ? "unlit-simple"
                : recipe.GraphTemplate.Trim().ToLowerInvariant();

            recipe.Surface ??= new ShaderStyleRecipeSurfaceData();
            recipe.Surface.Type = string.IsNullOrWhiteSpace(recipe.Surface.Type)
                ? "Opaque"
                : recipe.Surface.Type.Trim();

            recipe.Palette ??= new ShaderStyleRecipePaletteData();
            recipe.Palette.BaseColors ??= new List<string>();
            recipe.Palette.ShadowColors ??= new List<string>();
            recipe.Palette.HighlightColors ??= new List<string>();
            if (recipe.Palette.BaseColors.Count == 0)
                recipe.Palette.BaseColors.Add("#FFFFFF");

            recipe.Lighting ??= new ShaderStyleRecipeLightingData();
            if (string.IsNullOrWhiteSpace(recipe.Lighting.Mode))
                recipe.Lighting.Mode = "unlit";
            if (recipe.Lighting.Steps <= 0)
                recipe.Lighting.Steps = 1;

            recipe.Outline ??= new ShaderStyleRecipeOutlineData();
            recipe.Outline.Color = string.IsNullOrWhiteSpace(recipe.Outline.Color)
                ? "#000000"
                : recipe.Outline.Color.Trim();
            if (recipe.Outline.Width < 0f)
                recipe.Outline.Width = 0f;

            recipe.Texture ??= new ShaderStyleRecipeTextureData();
            recipe.Material ??= new ShaderStyleRecipeMaterialData();
            recipe.Notes ??= new List<string>();
        }

        static void ValidateStyleRecipe(ShaderStyleRecipeData recipe)
        {
            recipe.RenderPipeline = NormalizeRenderPipeline(recipe.RenderPipeline!);

            ValidateHexColorList(recipe.Palette!.BaseColors!, "palette.baseColors");
            ValidateHexColorList(recipe.Palette.ShadowColors!, "palette.shadowColors");
            ValidateHexColorList(recipe.Palette.HighlightColors!, "palette.highlightColors");
            ValidateHexColor(recipe.Outline!.Color!, "outline.color");

            if (recipe.Lighting!.Steps < 1)
                throw new ArgumentException("lighting.steps must be greater than or equal to 1.");

            ValidateRange(recipe.Lighting.ShadowSoftness, 0f, 1f, "lighting.shadowSoftness");
            ValidateRange(recipe.Lighting.RimLight, 0f, 1f, "lighting.rimLight");
            ValidateRange(recipe.Texture!.NoiseAmount, 0f, 1f, "texture.noiseAmount");
            ValidateRange(recipe.Texture.BrushGrain, 0f, 1f, "texture.brushGrain");
            ValidateRange(recipe.Material!.Smoothness, 0f, 1f, "material.smoothness");
            ValidateRange(recipe.Material.Metallic, 0f, 1f, "material.metallic");

            if (recipe.Material.NormalStrength < 0f)
                throw new ArgumentException("material.normalStrength must be greater than or equal to 0.");

            if (recipe.Material.EmissionStrength < 0f)
                throw new ArgumentException("material.emissionStrength must be greater than or equal to 0.");

            if (recipe.Outline.Width < 0f)
                throw new ArgumentException("outline.width must be greater than or equal to 0.");
        }

        static string NormalizeRenderPipeline(string renderPipeline)
        {
            if (string.Equals(renderPipeline, "URP", StringComparison.OrdinalIgnoreCase)
                || string.Equals(renderPipeline, "Universal", StringComparison.OrdinalIgnoreCase)
                || string.Equals(renderPipeline, "Universal Render Pipeline", StringComparison.OrdinalIgnoreCase))
            {
                return "URP";
            }

            throw new ArgumentException(
                $"Unsupported renderPipeline '{renderPipeline}'. Epic 5 currently supports only 'URP'.",
                nameof(renderPipeline));
        }

        static void ValidateOutputAssetPaths(string graphAssetPath, string materialAssetPath, bool overwrite)
        {
            if (string.IsNullOrWhiteSpace(graphAssetPath))
                throw new ArgumentException(Tool_Assets.Error.EmptyAssetPath(), nameof(graphAssetPath));

            if (!graphAssetPath.StartsWith("Assets/"))
                throw new ArgumentException(Tool_Assets.Error.AssetPathMustStartWithAssets(graphAssetPath), nameof(graphAssetPath));

            if (!IsShaderGraphAssetPath(graphAssetPath))
                throw new ArgumentException(Error.AssetPathMustEndWithShaderGraph(graphAssetPath), nameof(graphAssetPath));

            if (string.IsNullOrWhiteSpace(materialAssetPath))
                throw new ArgumentException(Tool_Assets.Error.EmptyAssetPath(), nameof(materialAssetPath));

            if (!materialAssetPath.StartsWith("Assets/"))
                throw new ArgumentException(Tool_Assets.Error.AssetPathMustStartWithAssets(materialAssetPath), nameof(materialAssetPath));

            if (!materialAssetPath.EndsWith(".mat", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(Tool_Assets.Error.AssetPathMustEndWithMat(materialAssetPath), nameof(materialAssetPath));

            if (!overwrite && AssetDatabase.LoadMainAssetAtPath(graphAssetPath) != null)
                throw new InvalidOperationException(Error.ShaderGraphAssetAlreadyExists(graphAssetPath));

            if (!overwrite && AssetDatabase.LoadMainAssetAtPath(materialAssetPath) != null)
                throw new InvalidOperationException(Tool_Assets.Error.MaterialAssetAlreadyExists(materialAssetPath));
        }

        static ResolvedStyleTemplate ResolveStyleTemplate(ShaderStyleRecipeData recipe, List<string> warnings)
        {
            var requestedTemplate = recipe.GraphTemplate ?? "unlit-simple";
            switch (requestedTemplate)
            {
                case "unlit-simple":
                    return new ResolvedStyleTemplate
                    {
                        RequestedId = requestedTemplate,
                        ResolvedId = "unlit-simple",
                        AssetPath = DefaultTemplateAssetPath
                    };
                case "cross-pipeline-unlit":
                    warnings.Add("graphTemplate 'cross-pipeline-unlit' currently resolves to the safe built-in 'unlit-simple' template.");
                    return new ResolvedStyleTemplate
                    {
                        RequestedId = requestedTemplate,
                        ResolvedId = "unlit-simple",
                        AssetPath = DefaultTemplateAssetPath
                    };
                case "toon-unlit":
                case "posterized-unlit":
                    warnings.Add($"graphTemplate '{requestedTemplate}' is validated but currently falls back to the safe 'unlit-simple' template until parameterized style templates are added.");
                    return new ResolvedStyleTemplate
                    {
                        RequestedId = requestedTemplate,
                        ResolvedId = "unlit-simple",
                        AssetPath = DefaultTemplateAssetPath
                    };
                default:
                    throw new ArgumentException(
                        $"Unknown graphTemplate '{requestedTemplate}'. Supported values in Epic 5: 'unlit-simple', 'cross-pipeline-unlit', 'toon-unlit', 'posterized-unlit'.",
                        nameof(recipe));
            }
        }

        static void ApplyRecipeToMaterial(
            Material material,
            ShaderStyleRecipeData recipe,
            List<string> warnings,
            List<string> appliedMaterialProperties)
        {
            var baseColors = recipe.Palette!.BaseColors!;
            if (baseColors.Count > 0)
            {
                var baseColor = ParseHexColor(baseColors[0], "palette.baseColors[0]");
                if (material.HasColor("_BaseColor"))
                {
                    material.SetColor("_BaseColor", baseColor);
                    appliedMaterialProperties.Add("_BaseColor");
                }
                else if (material.HasColor("_Color"))
                {
                    material.SetColor("_Color", baseColor);
                    appliedMaterialProperties.Add("_Color");
                }
                else
                {
                    warnings.Add("Recipe base color was validated but the generated material exposes no writable color property.");
                }

                if (baseColors.Count > 1)
                    warnings.Add("Only the first palette.baseColors entry is applied in Epic 5.");
            }

            if (recipe.Palette.ShadowColors!.Count > 0 || recipe.Palette.HighlightColors!.Count > 0)
                warnings.Add("palette.shadowColors and palette.highlightColors are validated but not yet applied by the current template.");

            if (!string.Equals(recipe.Surface!.Type, "Opaque", StringComparison.OrdinalIgnoreCase)
                || recipe.Surface.AlphaClipping
                || recipe.Surface.DoubleSided)
            {
                warnings.Add("surface fields are validated but not yet applied by the current template.");
            }

            if (!string.Equals(recipe.Lighting!.Mode, "unlit", StringComparison.OrdinalIgnoreCase)
                || recipe.Lighting.Steps != 1
                || !Mathf.Approximately(recipe.Lighting.ShadowSoftness, 0f)
                || !Mathf.Approximately(recipe.Lighting.RimLight, 0f))
            {
                warnings.Add("lighting fields are validated but not yet applied by the current template.");
            }

            if (recipe.Outline!.Enabled
                || !string.Equals(recipe.Outline.Color, "#000000", StringComparison.OrdinalIgnoreCase)
                || !Mathf.Approximately(recipe.Outline.Width, 0f))
            {
                warnings.Add("outline fields are validated but not yet applied by the current template.");
            }

            if (recipe.Texture!.UseReferenceTexture
                || !Mathf.Approximately(recipe.Texture.NoiseAmount, 0f)
                || !Mathf.Approximately(recipe.Texture.BrushGrain, 0f))
            {
                warnings.Add("texture fields are validated but not yet applied by the current template.");
            }

            if (!Mathf.Approximately(recipe.Material!.Smoothness, 0f)
                || !Mathf.Approximately(recipe.Material.Metallic, 0f)
                || !Mathf.Approximately(recipe.Material.NormalStrength, 0f)
                || !Mathf.Approximately(recipe.Material.EmissionStrength, 0f))
            {
                warnings.Add("material fields are validated but not yet applied by the current template.");
            }

            if (recipe.Notes!.Count > 0)
                warnings.Add("notes are preserved in the result but are not directly applied to the generated assets.");
        }

        static void ValidateKnownStyleRecipeFields(JsonElement root, List<string> warnings)
        {
            WarnOnUnknownFields(root, TopLevelStyleRecipeFields, string.Empty, warnings);

            if (TryGetObjectProperty(root, "surface", out var surface))
                WarnOnUnknownFields(surface, SurfaceFields, "surface.", warnings);

            if (TryGetObjectProperty(root, "palette", out var palette))
                WarnOnUnknownFields(palette, PaletteFields, "palette.", warnings);

            if (TryGetObjectProperty(root, "lighting", out var lighting))
                WarnOnUnknownFields(lighting, LightingFields, "lighting.", warnings);

            if (TryGetObjectProperty(root, "outline", out var outline))
                WarnOnUnknownFields(outline, OutlineFields, "outline.", warnings);

            if (TryGetObjectProperty(root, "texture", out var texture))
                WarnOnUnknownFields(texture, TextureFields, "texture.", warnings);

            if (TryGetObjectProperty(root, "material", out var material))
                WarnOnUnknownFields(material, MaterialFields, "material.", warnings);
        }

        static bool TryGetObjectProperty(JsonElement root, string propertyName, out JsonElement value)
        {
            value = default;
            foreach (var property in root.EnumerateObject())
            {
                if (!string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    value = property.Value;
                    return true;
                }

                return false;
            }

            return false;
        }

        static void WarnOnUnknownFields(
            JsonElement element,
            HashSet<string> allowedFields,
            string pathPrefix,
            List<string> warnings)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (!allowedFields.Contains(property.Name))
                    warnings.Add($"Unsupported style recipe field '{pathPrefix}{property.Name}' will be ignored.");
            }
        }

        static void ValidateHexColorList(IReadOnlyList<string> colors, string fieldPath)
        {
            for (var i = 0; i < colors.Count; i++)
                ValidateHexColor(colors[i], $"{fieldPath}[{i}]");
        }

        static void ValidateHexColor(string color, string fieldPath)
        {
            if (!ColorUtility.TryParseHtmlString(color, out _))
                throw new ArgumentException($"Invalid color value '{color}' at '{fieldPath}'. Expected a hex color such as '#E8B88F'.");
        }

        static Color ParseHexColor(string color, string fieldPath)
        {
            if (!ColorUtility.TryParseHtmlString(color, out var parsedColor))
                throw new ArgumentException($"Invalid color value '{color}' at '{fieldPath}'. Expected a hex color such as '#E8B88F'.");

            return parsedColor;
        }

        static void ValidateRange(float value, float min, float max, string fieldPath)
        {
            if (value < min || value > max)
                throw new ArgumentException($"{fieldPath} must be between {min} and {max}. Actual value: {value}.");
        }
    }
}
