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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using AIGD;
using com.IvanMurzak.Unity.MCP.Editor.API;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Editor.Tests
{
    public partial class AssetsShaderGraphTests : BaseTest
    {
        const string TemplateAssetPath =
            "Packages/com.unity.shadergraph/GraphTemplates/Cross Pipeline/Unlit Simple.shadergraph";
        const string TestFolder = "Assets/Unity-MCP-Test/ShaderGraphs";

        [Test]
        public void ShaderGraph_Find_ReturnsShaderGraphAsset()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_Find.shadergraph");
            try
            {
                var result = new Tool_Assets_ShaderGraph().Find(
                    filter: "Validation_Find",
                    searchInFolders: new[] { TestFolder },
                    maxResults: 10);

                Assert.IsNotNull(result);
                Assert.IsTrue(result.Any(asset => asset.AssetPath == assetPath),
                    $"Expected Shader Graph search results to include '{assetPath}'.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_GetData_ReturnsSummaryAndDiagnostics()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_GetData.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var result = new Tool_Assets_ShaderGraph().GetData(
                    new AssetObjectRef(shader),
                    includeMessages: true,
                    includeProperties: true,
                    includeDiagnostics: true);

                Assert.IsNotNull(result);
                Assert.AreEqual(assetPath, result.AssetPath);
                Assert.IsTrue(result.SourceParsed, "Shader Graph source should parse successfully.");
                Assert.IsTrue(result.ShaderResolved, "Unity should resolve a Shader from the imported graph.");
                Assert.IsTrue(result.GraphVersion.HasValue, "Graph version should be available.");
                Assert.Greater(result.NodeCount, 0, "Shader Graph should contain nodes.");
                Assert.Greater(result.ActiveTargetCount, 0, "Shader Graph should declare at least one active target.");
                Assert.IsNotEmpty(result.ActiveTargetTypes, "Active target types should be resolved.");
                Assert.Greater(result.ShaderPropertyCount, 0, "Compiled shader should expose properties.");
                Assert.IsNotEmpty(result.Properties, "Compiled shader properties should be returned.");
                Assert.IsNotEmpty(result.Diagnostics, "Diagnostics should always include at least one entry.");
                Assert.IsFalse(result.Diagnostics!.Any(d => d.Severity == "Error"),
                    "A clean template-derived graph should not report error diagnostics.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_GetStructure_ReturnsPropertiesNodesEdgesAndTargets()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_GetStructure.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var result = new Tool_Assets_ShaderGraph().GetStructure(new AssetObjectRef(shader));

                Assert.IsNotNull(result);
                Assert.IsTrue(result.SourceParsed, "Shader Graph source should parse successfully.");
                Assert.AreEqual(assetPath, result.AssetPath);
                Assert.AreEqual("UnityEditor.ShaderGraph.GraphData", result.GraphType);
                Assert.AreEqual("Unlit", result.ShaderMenuPath);

                Assert.IsNotEmpty(result.Properties, "Structure result should include blackboard properties.");
                Assert.IsTrue(result.Properties!.Any(p => p.OverrideReferenceName == "_BaseColor"),
                    "Expected a property with override reference name '_BaseColor'.");
                Assert.IsTrue(result.Properties.Any(p => p.OverrideReferenceName == "_BaseMap"),
                    "Expected a property with override reference name '_BaseMap'.");

                Assert.IsNotEmpty(result.Nodes, "Structure result should include node definitions.");
                var sampleTextureNode = result.Nodes!.FirstOrDefault(n => n.Name == "Sample Texture 2D");
                Assert.IsNotNull(sampleTextureNode, "Expected a 'Sample Texture 2D' node.");
                Assert.IsNotEmpty(sampleTextureNode!.Slots, "Expected resolved slots for the sample texture node.");
                Assert.IsTrue(sampleTextureNode.Slots!.Any(s => s.DisplayName == "Texture"),
                    "Expected the sample texture node to expose a 'Texture' slot.");
                Assert.IsTrue(result.Nodes.Any(n =>
                        n.Type == "UnityEditor.ShaderGraph.PropertyNode"
                        && n.PropertyReferenceName == "_BaseColor"),
                    "Expected structure data to resolve PropertyNode references back to their blackboard property.");

                Assert.IsNotEmpty(result.Edges, "Structure result should include edge definitions.");
                Assert.IsTrue(result.Edges!.All(e => !string.IsNullOrEmpty(e.OutputNodeId) && !string.IsNullOrEmpty(e.InputNodeId)),
                    "Every edge should resolve both output and input node ids.");

                Assert.IsNotEmpty(result.Targets, "Structure result should include active target definitions.");
                Assert.IsTrue(result.Targets!.Any(t => t.Type != null && t.Type.Contains("UniversalTarget")),
                    "Expected a Universal target definition.");

                Assert.IsNotNull(result.VertexContext, "Vertex context should be present.");
                Assert.IsNotNull(result.FragmentContext, "Fragment context should be present.");
                Assert.IsNotEmpty(result.VertexContext!.BlockNodeIds, "Vertex context should reference block nodes.");
                Assert.IsNotEmpty(result.FragmentContext!.BlockNodeIds, "Fragment context should reference block nodes.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_GetSettings_ReturnsRootAndUniversalTargetSettings()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_GetSettings.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var result = new Tool_Assets_ShaderGraph().GetSettings(new AssetObjectRef(shader));

                Assert.IsNotNull(result);
                Assert.IsTrue(result.SourceParsed, "Shader Graph source should parse successfully.");
                Assert.AreEqual(assetPath, result.AssetPath);
                Assert.AreEqual("UnityEditor.ShaderGraph.GraphData", result.GraphType);
                Assert.IsNotNull(result.Graph, "Root graph settings should be returned.");
                Assert.AreEqual("Unlit", result.Graph!.ShaderMenuPath);
                Assert.AreEqual("graph", result.Graph.GraphPrecision);
                Assert.AreEqual("preview3d", result.Graph.PreviewMode);

                Assert.IsNotNull(result.UniversalTarget, "Expected Universal target settings to be returned.");
                Assert.AreEqual("opaque", result.UniversalTarget!.SurfaceType);
                Assert.AreEqual("alpha", result.UniversalTarget.AlphaMode);
                Assert.AreEqual("front", result.UniversalTarget.RenderFace);
                Assert.AreEqual("auto", result.UniversalTarget.DepthWrite);
                Assert.AreEqual("lessEqual", result.UniversalTarget.DepthTest);
                Assert.IsFalse(result.UniversalTarget.AlphaClip ?? true);
                Assert.IsTrue(result.UniversalTarget.CastShadows ?? false);
                Assert.IsTrue(result.UniversalTarget.ReceiveShadows ?? false);
                Assert.IsFalse(result.UniversalTarget.DisableTint ?? true);
                Assert.AreEqual("none", result.UniversalTarget.AdditionalMotionVectors);
                Assert.IsFalse(result.UniversalTarget.AlembicMotionVectors ?? true);
                Assert.IsFalse(result.UniversalTarget.SupportVfx ?? true);

                Assert.IsNotEmpty(result.ActiveTargetTypes, "Active target types should be surfaced.");
                Assert.IsTrue(result.ActiveTargetTypes!.Any(type => type.Contains("UniversalTarget")),
                    "Expected the Universal target type to be reported.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_SetSettings_UpdatesRootAndUniversalTargetSettings()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_SetSettings.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var result = new Tool_Assets_ShaderGraph().SetSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphSettingsUpdateInput
                    {
                        Graph = new ShaderGraphRootSettingsUpdateInput
                        {
                            ShaderMenuPath = "Validation/Settings",
                            GraphPrecision = "half",
                            PreviewMode = "preview2d"
                        },
                        UniversalTarget = new ShaderGraphUniversalTargetSettingsUpdateInput
                        {
                            AllowMaterialOverride = true,
                            SurfaceType = "transparent",
                            AlphaMode = "premultiply",
                            RenderFace = "both",
                            DepthWrite = "forceDisabled",
                            DepthTest = "greaterEqual",
                            AlphaClip = true,
                            CastShadows = false,
                            ReceiveShadows = false,
                            DisableTint = true,
                            AdditionalMotionVectors = "timeBased",
                            AlembicMotionVectors = true,
                            SupportsLodCrossFade = true,
                            CustomEditorGui = "Codex.Validation.CustomShaderGUI",
                            SupportVfx = true
                        }
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(result);
                Assert.IsNotEmpty(result.ChangedFields, "Expected at least one setting field to change.");
                Assert.IsTrue(result.ChangedFields!.Contains("graph.shaderMenuPath"));
                Assert.IsTrue(result.ChangedFields.Contains("graph.graphPrecision"));
                Assert.IsTrue(result.ChangedFields.Contains("graph.previewMode"));
                Assert.IsTrue(result.ChangedFields.Contains("universalTarget.surfaceType"));
                Assert.IsTrue(result.ChangedFields.Contains("universalTarget.alphaMode"));
                Assert.IsTrue(result.ChangedFields.Contains("universalTarget.renderFace"));
                Assert.IsTrue(result.ChangedFields.Contains("universalTarget.depthWrite"));
                Assert.IsTrue(result.ChangedFields.Contains("universalTarget.depthTest"));
                Assert.IsTrue(result.ChangedFields.Contains("universalTarget.alphaClip"));
                Assert.IsTrue(result.ChangedFields.Contains("universalTarget.additionalMotionVectors"));
                Assert.IsTrue(result.ChangedFields.Contains("universalTarget.customEditorGui"));

                Assert.IsNotNull(result.Settings);
                Assert.IsNotNull(result.Settings!.Graph);
                Assert.AreEqual("Validation/Settings", result.Settings.Graph!.ShaderMenuPath);
                Assert.AreEqual("half", result.Settings.Graph.GraphPrecision);
                Assert.AreEqual("preview2d", result.Settings.Graph.PreviewMode);

                Assert.IsNotNull(result.Settings.UniversalTarget);
                Assert.IsTrue(result.Settings.UniversalTarget!.AllowMaterialOverride ?? false);
                Assert.AreEqual("transparent", result.Settings.UniversalTarget.SurfaceType);
                Assert.AreEqual("premultiply", result.Settings.UniversalTarget.AlphaMode);
                Assert.AreEqual("both", result.Settings.UniversalTarget.RenderFace);
                Assert.AreEqual("forceDisabled", result.Settings.UniversalTarget.DepthWrite);
                Assert.AreEqual("greaterEqual", result.Settings.UniversalTarget.DepthTest);
                Assert.IsTrue(result.Settings.UniversalTarget.AlphaClip ?? false);
                Assert.IsFalse(result.Settings.UniversalTarget.CastShadows ?? true);
                Assert.IsFalse(result.Settings.UniversalTarget.ReceiveShadows ?? true);
                Assert.IsTrue(result.Settings.UniversalTarget.DisableTint ?? false);
                Assert.AreEqual("timeBased", result.Settings.UniversalTarget.AdditionalMotionVectors);
                Assert.IsTrue(result.Settings.UniversalTarget.AlembicMotionVectors ?? false);
                Assert.IsTrue(result.Settings.UniversalTarget.SupportsLodCrossFade ?? false);
                Assert.AreEqual("Codex.Validation.CustomShaderGUI", result.Settings.UniversalTarget.CustomEditorGui);
                Assert.IsTrue(result.Settings.UniversalTarget.SupportVfx ?? false);

                Assert.IsNotNull(result.Graph);
                Assert.IsTrue(result.Graph!.SourceParsed, "Updated Shader Graph source should parse successfully.");
                Assert.IsTrue(result.Graph.ShaderResolved, "Updated Shader Graph should still resolve a compiled shader.");
                Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Updating safe settings should not introduce import errors.");
                Assert.IsTrue(result.Graph.ShaderName!.StartsWith("Validation/Settings/", StringComparison.Ordinal),
                    "Compiled shader name should reflect the updated shader menu path.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateProperty_UpdatesColorAndTextureProperties()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateProperty.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var colorResult = tool.UpdateProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphPropertyUpdateInput
                    {
                        PropertyReferenceName = "_BaseColor",
                        DisplayName = "Tint",
                        OverrideReferenceName = "_TintColor",
                        ColorHex = "#FF7A00CC"
                    },
                    includeMessages: true,
                    includeProperties: true);

                var textureResult = tool.UpdateProperty(
                    new AssetObjectRef(assetPath),
                    new ShaderGraphPropertyUpdateInput
                    {
                        PropertyReferenceName = "_BaseMap",
                        DisplayName = "Diffuse Map",
                        OverrideReferenceName = "_DiffuseTex",
                        TextureDefaultType = "black",
                        TextureUseTilingAndOffset = false,
                        TextureUseTexelSize = false,
                        TextureIsMainTexture = false,
                        TextureIsHdr = true,
                        TextureModifiable = false
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(colorResult);
                Assert.IsTrue(colorResult.ChangedFields!.Contains("property.displayName"));
                Assert.IsTrue(colorResult.ChangedFields.Contains("property.overrideReferenceName"));
                Assert.IsTrue(colorResult.ChangedFields.Contains("property.color.r"));
                Assert.IsNotNull(colorResult.Property);
                Assert.AreEqual("Tint", colorResult.Property!.Name);
                Assert.AreEqual("_TintColor", colorResult.Property.OverrideReferenceName);
                StringAssert.Contains("\"r\":1", colorResult.Property.ValueJson);

                Assert.IsNotNull(textureResult);
                Assert.IsNotNull(textureResult.Property);
                Assert.AreEqual("Diffuse Map", textureResult.Property!.Name);
                Assert.AreEqual("_DiffuseTex", textureResult.Property.OverrideReferenceName);
                Assert.AreEqual("texture2D", textureResult.Property.PropertyKind);
                Assert.AreEqual("black", textureResult.Property.TextureDefaultType);
                Assert.AreEqual(1, textureResult.Property.TextureDefaultTypeValue);
                Assert.IsFalse(textureResult.Property.TextureUseTilingAndOffset ?? true);
                Assert.IsFalse(textureResult.Property.TextureUseTexelSize ?? true);
                Assert.IsFalse(textureResult.Property.TextureIsMainTexture ?? true);
                Assert.IsTrue(textureResult.Property.TextureIsHdr ?? false);
                Assert.IsFalse(textureResult.Property.TextureModifiable ?? true);

                Assert.IsNotNull(textureResult.Structure);
                Assert.IsTrue(textureResult.Structure!.Properties!.Any(p => p.OverrideReferenceName == "_TintColor"));
                Assert.IsTrue(textureResult.Structure.Properties.Any(p => p.OverrideReferenceName == "_DiffuseTex"));

                Assert.IsNotNull(textureResult.Graph);
                Assert.IsTrue(textureResult.Graph!.ShaderResolved, "Updated Shader Graph should still resolve a compiled shader.");
                Assert.IsFalse(textureResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Updating existing blackboard properties should not introduce import errors.");
                Assert.IsTrue(textureResult.Graph.Properties!.Any(p => p.Name == "_TintColor"),
                    "Compiled shader properties should include the renamed color reference.");
                Assert.IsTrue(textureResult.Graph.Properties.Any(p => p.Name == "_DiffuseTex"),
                    "Compiled shader properties should include the renamed texture reference.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateProperty_UpdatesExpandedDefaultValues()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateProperty_Expanded.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Glow Strength",
                        OverrideReferenceName = "_GlowStrength",
                        FloatValue = 0.25f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "vector3",
                        DisplayName = "Flow Direction",
                        OverrideReferenceName = "_FlowDirection",
                        VectorX = 0f,
                        VectorY = 1f,
                        VectorZ = 0f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "boolean",
                        DisplayName = "Use Rim",
                        OverrideReferenceName = "_UseRim",
                        BooleanValue = false
                    });

                var floatResult = tool.UpdateProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphPropertyUpdateInput
                    {
                        PropertyReferenceName = "_GlowStrength",
                        FloatValue = 1.25f
                    });
                var vectorResult = tool.UpdateProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphPropertyUpdateInput
                    {
                        PropertyReferenceName = "_FlowDirection",
                        VectorX = 0.5f,
                        VectorY = 0.25f,
                        VectorZ = 0.75f
                    });
                var booleanResult = tool.UpdateProperty(
                    new AssetObjectRef(assetPath),
                    new ShaderGraphPropertyUpdateInput
                    {
                        PropertyReferenceName = "_UseRim",
                        BooleanValue = true
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(floatResult.Property);
                Assert.AreEqual("float", floatResult.Property!.PropertyKind);
                Assert.AreEqual(1.25f, floatResult.Property.FloatValue);
                Assert.IsTrue(floatResult.ChangedFields!.Contains("property.floatValue"));

                Assert.IsNotNull(vectorResult.Property);
                Assert.AreEqual("vector3", vectorResult.Property!.PropertyKind);
                Assert.AreEqual(0.5f, vectorResult.Property.VectorX);
                Assert.AreEqual(0.25f, vectorResult.Property.VectorY);
                Assert.AreEqual(0.75f, vectorResult.Property.VectorZ);
                Assert.IsNull(vectorResult.Property.VectorW);
                Assert.IsTrue(vectorResult.ChangedFields!.Contains("property.vector.x"));
                Assert.IsTrue(vectorResult.ChangedFields.Contains("property.vector.y"));
                Assert.IsTrue(vectorResult.ChangedFields.Contains("property.vector.z"));

                Assert.IsNotNull(booleanResult.Property);
                Assert.AreEqual("boolean", booleanResult.Property!.PropertyKind);
                Assert.IsTrue(booleanResult.Property.BooleanValue ?? false);
                Assert.IsTrue(booleanResult.ChangedFields!.Contains("property.booleanValue"));

                Assert.IsNotNull(booleanResult.Graph);
                Assert.IsTrue(booleanResult.Graph!.ShaderResolved, "Updated Shader Graph should still resolve a compiled shader.");
                Assert.IsFalse(booleanResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Updating expanded blackboard property defaults should not introduce import errors.");
                Assert.IsTrue(booleanResult.Graph.Properties!.Any(p => p.Name == "_GlowStrength"));
                Assert.IsTrue(booleanResult.Graph.Properties.Any(p => p.Name == "_FlowDirection"));
                Assert.IsTrue(booleanResult.Graph.Properties.Any(p => p.Name == "_UseRim"));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateProperty_DuplicateReferenceName_Throws()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateProperty_Duplicate.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                Assert.Throws<InvalidOperationException>(() => tool.UpdateProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphPropertyUpdateInput
                    {
                        PropertyReferenceName = "_BaseColor",
                        OverrideReferenceName = "_BaseMap"
                    }));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddProperty_AddsColorAndFloatProperties()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddProperty.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var colorResult = tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "color",
                        DisplayName = "Accent",
                        OverrideReferenceName = "_AccentColor",
                        ColorHex = "#44CC88FF"
                    },
                    includeMessages: true,
                    includeProperties: true);

                var floatResult = tool.AddProperty(
                    new AssetObjectRef(assetPath),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Glow Strength",
                        OverrideReferenceName = "_GlowStrength",
                        FloatValue = 0.75f
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(colorResult);
                Assert.IsNotNull(colorResult.Property);
                Assert.AreEqual("Accent", colorResult.Property!.Name);
                Assert.AreEqual("_AccentColor", colorResult.Property.OverrideReferenceName);
                StringAssert.Contains("\"g\": 0.8", colorResult.Property.ValueJson);

                Assert.IsNotNull(floatResult);
                Assert.IsNotNull(floatResult.Property);
                Assert.AreEqual("Glow Strength", floatResult.Property!.Name);
                Assert.AreEqual("_GlowStrength", floatResult.Property.OverrideReferenceName);
                Assert.AreEqual("0.75", floatResult.Property.ValueJson);

                Assert.IsNotNull(floatResult.Structure);
                Assert.IsTrue(floatResult.Structure!.Properties!.Any(p => p.OverrideReferenceName == "_AccentColor"));
                Assert.IsTrue(floatResult.Structure.Properties.Any(p => p.OverrideReferenceName == "_GlowStrength"));

                Assert.IsNotNull(floatResult.Graph);
                Assert.IsTrue(floatResult.Graph!.ShaderResolved, "Updated Shader Graph should still resolve a compiled shader.");
                Assert.IsFalse(floatResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Adding blackboard properties should not introduce import errors.");
                Assert.IsTrue(floatResult.Graph.Properties!.Any(p => p.Name == "_AccentColor"),
                    "Compiled shader properties should include the added color property.");
                Assert.IsTrue(floatResult.Graph.Properties.Any(p => p.Name == "_GlowStrength"),
                    "Compiled shader properties should include the added float property.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddProperty_AddsExpandedBlackboardPropertyTypes()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddProperty_Expanded.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var textureResult = tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "texture2D",
                        DisplayName = "Mask Map",
                        OverrideReferenceName = "_MaskMap",
                        TextureDefaultType = "linearGrey",
                        TextureUseTilingAndOffset = true,
                        TextureUseTexelSize = false,
                        TextureIsHdr = true
                    });
                var vector2Result = tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "vector2",
                        DisplayName = "UV Scale",
                        OverrideReferenceName = "_UVScale",
                        VectorX = 2f,
                        VectorY = 3f
                    });
                var vector3Result = tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "vector3",
                        DisplayName = "Wind Direction",
                        OverrideReferenceName = "_WindDirection",
                        VectorX = 0.1f,
                        VectorY = 0.2f,
                        VectorZ = 0.3f
                    });
                var vector4Result = tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "vector4",
                        DisplayName = "Packed Controls",
                        OverrideReferenceName = "_PackedControls",
                        VectorX = 1f,
                        VectorY = 2f,
                        VectorZ = 3f,
                        VectorW = 4f
                    });
                var booleanResult = tool.AddProperty(
                    new AssetObjectRef(assetPath),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "boolean",
                        DisplayName = "Use Detail",
                        OverrideReferenceName = "_UseDetail",
                        BooleanValue = true
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(textureResult.Property);
                Assert.AreEqual("texture2D", textureResult.Property!.PropertyKind);
                Assert.AreEqual("_MaskMap", textureResult.Property.OverrideReferenceName);
                Assert.AreEqual("linearGrey", textureResult.Property.TextureDefaultType);
                Assert.IsTrue(textureResult.Property.TextureUseTilingAndOffset ?? false);
                Assert.IsFalse(textureResult.Property.TextureUseTexelSize ?? true);
                Assert.IsTrue(textureResult.Property.TextureIsHdr ?? false);

                Assert.IsNotNull(vector2Result.Property);
                Assert.AreEqual("vector2", vector2Result.Property!.PropertyKind);
                Assert.AreEqual(2f, vector2Result.Property.VectorX);
                Assert.AreEqual(3f, vector2Result.Property.VectorY);
                Assert.IsNull(vector2Result.Property.VectorZ);
                Assert.IsNull(vector2Result.Property.VectorW);

                Assert.IsNotNull(vector3Result.Property);
                Assert.AreEqual("vector3", vector3Result.Property!.PropertyKind);
                Assert.AreEqual(0.1f, vector3Result.Property.VectorX);
                Assert.AreEqual(0.2f, vector3Result.Property.VectorY);
                Assert.AreEqual(0.3f, vector3Result.Property.VectorZ);
                Assert.IsNull(vector3Result.Property.VectorW);

                Assert.IsNotNull(vector4Result.Property);
                Assert.AreEqual("vector4", vector4Result.Property!.PropertyKind);
                Assert.AreEqual(1f, vector4Result.Property.VectorX);
                Assert.AreEqual(2f, vector4Result.Property.VectorY);
                Assert.AreEqual(3f, vector4Result.Property.VectorZ);
                Assert.AreEqual(4f, vector4Result.Property.VectorW);

                Assert.IsNotNull(booleanResult.Property);
                Assert.AreEqual("boolean", booleanResult.Property!.PropertyKind);
                Assert.IsTrue(booleanResult.Property.BooleanValue ?? false);

                Assert.IsNotNull(booleanResult.Structure);
                Assert.IsTrue(booleanResult.Structure!.Properties!.Any(p => p.OverrideReferenceName == "_MaskMap"));
                Assert.IsTrue(booleanResult.Structure.Properties.Any(p => p.OverrideReferenceName == "_UVScale"));
                Assert.IsTrue(booleanResult.Structure.Properties.Any(p => p.OverrideReferenceName == "_WindDirection"));
                Assert.IsTrue(booleanResult.Structure.Properties.Any(p => p.OverrideReferenceName == "_PackedControls"));
                Assert.IsTrue(booleanResult.Structure.Properties.Any(p => p.OverrideReferenceName == "_UseDetail"));

                Assert.IsNotNull(booleanResult.Graph);
                Assert.IsTrue(booleanResult.Graph!.ShaderResolved, "Updated Shader Graph should still resolve a compiled shader.");
                Assert.IsFalse(booleanResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Adding expanded blackboard property types should not introduce import errors.");
                Assert.IsTrue(booleanResult.Graph.Properties!.Any(p => p.Name == "_MaskMap"));
                Assert.IsTrue(booleanResult.Graph.Properties.Any(p => p.Name == "_UVScale"));
                Assert.IsTrue(booleanResult.Graph.Properties.Any(p => p.Name == "_WindDirection"));
                Assert.IsTrue(booleanResult.Graph.Properties.Any(p => p.Name == "_PackedControls"));
                Assert.IsTrue(booleanResult.Graph.Properties.Any(p => p.Name == "_UseDetail"));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddProperty_DuplicateDisplayName_Throws()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddProperty_Duplicate.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                Assert.Throws<InvalidOperationException>(() => tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "color",
                        DisplayName = "Color",
                        OverrideReferenceName = "_AnotherColor",
                        ColorHex = "#FFFFFFFF"
                    }));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_DeleteProperty_RemovesDependentPropertyNodesAndEdges()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_DeleteProperty.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "texture2D",
                        DisplayName = "Delete Texture",
                        OverrideReferenceName = "_DeleteTexture",
                        TextureDefaultType = "black"
                    });

                var propertyNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_DeleteTexture",
                        PositionX = -720f,
                        PositionY = 120f
                    });

                var structureBeforeConnect = tool.GetStructure(new AssetObjectRef(shader));
                var sampleTextureNode = structureBeforeConnect.Nodes!
                    .First(n => n.Name == "Sample Texture 2D");
                var textureInput = sampleTextureNode.Slots!
                    .First(s => s.DisplayName == "Texture");
                var deleteTextureOutput = propertyNodeResult.Node!.Slots!.Single();

                tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = propertyNodeResult.Node.ObjectId,
                        OutputSlotObjectId = deleteTextureOutput.ObjectId,
                        InputNodeObjectId = sampleTextureNode.ObjectId,
                        InputSlotObjectId = textureInput.ObjectId,
                        ReplaceExistingInputConnection = true
                    });

                var deleteResult = tool.DeleteProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphDeletePropertyInput
                    {
                        PropertyReferenceName = "_DeleteTexture"
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual("delete", deleteResult.Operation);
                Assert.AreEqual("_DeleteTexture", deleteResult.PropertyReferenceName);
                Assert.AreEqual("texture2D", deleteResult.PropertyKind);
                Assert.AreEqual(1, deleteResult.RemovedNodeCount);
                Assert.AreEqual(1, deleteResult.RemovedEdgeCount);
                Assert.IsTrue(deleteResult.ChangedFields!.Contains("property.deleted"));
                Assert.IsTrue(deleteResult.ChangedFields.Contains("node.autoRemoved"));
                Assert.IsTrue(deleteResult.ChangedFields.Contains("edge.autoRemoved"));

                Assert.IsNotNull(deleteResult.Structure);
                Assert.IsFalse(deleteResult.Structure!.Properties!
                    .Any(p => p.EffectiveReferenceName == "_DeleteTexture"));
                Assert.IsFalse(deleteResult.Structure.Nodes!
                    .Any(n => n.PropertyReferenceName == "_DeleteTexture"));
                Assert.IsFalse(deleteResult.Structure.Edges!
                    .Any(e => e.OutputNodeId == propertyNodeResult.Node.ObjectId));

                Assert.IsNotNull(deleteResult.Graph);
                Assert.IsTrue(deleteResult.Graph!.ShaderResolved);
                Assert.IsFalse(deleteResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ReorderProperty_ReordersWithinDefaultCategory()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_ReorderProperty.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Reorder First",
                        OverrideReferenceName = "_ReorderFirst",
                        FloatValue = 0.1f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "color",
                        DisplayName = "Reorder Second",
                        OverrideReferenceName = "_ReorderSecond",
                        ColorHex = "#3366FFFF"
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "boolean",
                        DisplayName = "Reorder Third",
                        OverrideReferenceName = "_ReorderThird",
                        BooleanValue = true
                    });

                var reorderResult = tool.ReorderProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphReorderPropertyInput
                    {
                        PropertyReferenceName = "_ReorderThird",
                        CategoryIndex = 0
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual("reorder", reorderResult.Operation);
                Assert.AreEqual("_ReorderThird", reorderResult.PropertyReferenceName);
                Assert.AreEqual(0, reorderResult.Property!.CategoryIndex);
                Assert.IsTrue(reorderResult.ChangedFields!.Contains("property.reordered"));

                var defaultCategory = reorderResult.Structure!.Categories!
                    .First(c => string.IsNullOrEmpty(c.Name));
                var firstPropertyId = defaultCategory.PropertyObjectIds!.First();
                var firstProperty = reorderResult.Structure.Properties!
                    .First(p => p.ObjectId == firstPropertyId);
                Assert.AreEqual("_ReorderThird", firstProperty.EffectiveReferenceName);

                Assert.IsNotNull(reorderResult.Graph);
                Assert.IsTrue(reorderResult.Graph!.ShaderResolved);
                Assert.IsFalse(reorderResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_CategoryTools_CreatePlaceAndMoveProperties()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_PropertyCategories.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var categoryResult = tool.CreateCategory(
                    new AssetObjectRef(shader),
                    new ShaderGraphCreateCategoryInput
                    {
                        CategoryName = "Surface Controls"
                    });

                Assert.AreEqual("createCategory", categoryResult.Operation);
                Assert.AreEqual("Surface Controls", categoryResult.CategoryName);
                Assert.IsNotNull(categoryResult.CategoryObjectId);

                var tintResult = tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "color",
                        DisplayName = "Category Tint",
                        OverrideReferenceName = "_CategoryTint",
                        ColorHex = "#FF8844FF",
                        CategoryName = "Surface Controls",
                        CategoryIndex = 0
                    });
                var strengthResult = tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Category Strength",
                        OverrideReferenceName = "_CategoryStrength",
                        FloatValue = 0.42f,
                        CategoryName = "Surface Controls",
                        CategoryIndex = 1
                    });
                var detailResult = tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "vector2",
                        DisplayName = "Category Detail",
                        OverrideReferenceName = "_CategoryDetail",
                        VectorX = 2f,
                        VectorY = 4f,
                        CategoryName = "Auto Created",
                        CreateCategoryIfMissing = true,
                        CategoryIndex = 0
                    });

                Assert.AreEqual("Surface Controls", tintResult.Property!.CategoryName);
                Assert.AreEqual(0, tintResult.Property.CategoryIndex);
                Assert.AreEqual("Surface Controls", strengthResult.Property!.CategoryName);
                Assert.AreEqual(1, strengthResult.Property.CategoryIndex);
                Assert.AreEqual("Auto Created", detailResult.Property!.CategoryName);
                Assert.AreEqual(0, detailResult.Property.CategoryIndex);

                var moveResult = tool.SetPropertyCategory(
                    new AssetObjectRef(shader),
                    new ShaderGraphSetPropertyCategoryInput
                    {
                        PropertyReferenceName = "_CategoryStrength",
                        CategoryName = "Auto Created",
                        CategoryIndex = 1
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual("setCategory", moveResult.Operation);
                Assert.AreEqual("_CategoryStrength", moveResult.PropertyReferenceName);
                Assert.AreEqual("Auto Created", moveResult.Property!.CategoryName);
                Assert.AreEqual(1, moveResult.Property.CategoryIndex);

                var surfaceCategory = moveResult.Structure!.Categories!
                    .First(c => c.Name == "Surface Controls");
                var autoCategory = moveResult.Structure.Categories!
                    .First(c => c.Name == "Auto Created");
                var surfaceReferences = surfaceCategory.PropertyObjectIds!
                    .Select(id => moveResult.Structure.Properties!.First(p => p.ObjectId == id).EffectiveReferenceName)
                    .ToArray();
                var autoReferences = autoCategory.PropertyObjectIds!
                    .Select(id => moveResult.Structure.Properties!.First(p => p.ObjectId == id).EffectiveReferenceName)
                    .ToArray();

                CollectionAssert.AreEqual(new[] { "_CategoryTint" }, surfaceReferences);
                CollectionAssert.AreEqual(new[] { "_CategoryDetail", "_CategoryStrength" }, autoReferences);

                Assert.IsNotNull(moveResult.Graph);
                Assert.IsTrue(moveResult.Graph!.ShaderResolved);
                Assert.IsFalse(moveResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddPropertyNode_AddsSupportedPropertyNodes()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddPropertyNode.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Glow Strength",
                        OverrideReferenceName = "_GlowStrength",
                        FloatValue = 0.75f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "vector2",
                        DisplayName = "UV Scale",
                        OverrideReferenceName = "_UVScale",
                        VectorX = 2f,
                        VectorY = 3f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "vector3",
                        DisplayName = "Flow Direction",
                        OverrideReferenceName = "_FlowDirection",
                        VectorX = 0.5f,
                        VectorY = 0.25f,
                        VectorZ = 0.75f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "vector4",
                        DisplayName = "Packed Controls",
                        OverrideReferenceName = "_PackedControls",
                        VectorX = 1f,
                        VectorY = 2f,
                        VectorZ = 3f,
                        VectorW = 4f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "boolean",
                        DisplayName = "Use Detail",
                        OverrideReferenceName = "_UseDetail",
                        BooleanValue = true
                    });

                var colorNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_BaseColor",
                        PositionX = -720f,
                        PositionY = 160f
                    },
                    includeMessages: true,
                    includeProperties: true);

                var floatNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(assetPath),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_GlowStrength",
                        PositionX = -720f,
                        PositionY = 260f
                    },
                    includeMessages: true,
                    includeProperties: true);

                var textureNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_BaseMap",
                        PositionX = -720f,
                        PositionY = 360f
                    },
                    includeMessages: true,
                    includeProperties: true);

                var vector2NodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_UVScale",
                        PositionX = -720f,
                        PositionY = 460f
                    },
                    includeMessages: true,
                    includeProperties: true);

                var vector3NodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_FlowDirection",
                        PositionX = -720f,
                        PositionY = 560f
                    },
                    includeMessages: true,
                    includeProperties: true);

                var vector4NodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_PackedControls",
                        PositionX = -720f,
                        PositionY = 660f
                    },
                    includeMessages: true,
                    includeProperties: true);

                var booleanNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(assetPath),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_UseDetail",
                        PositionX = -720f,
                        PositionY = 760f
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(colorNodeResult);
                Assert.IsNotNull(colorNodeResult.Node);
                Assert.AreEqual("UnityEditor.ShaderGraph.PropertyNode", colorNodeResult.Node!.Type);
                Assert.AreEqual("_BaseColor", colorNodeResult.Node.PropertyReferenceName);
                Assert.AreEqual(-720f, colorNodeResult.Node.PositionX);
                Assert.AreEqual(160f, colorNodeResult.Node.PositionY);
                Assert.IsNotEmpty(colorNodeResult.Node.Slots);
                Assert.AreEqual("Color", colorNodeResult.Node.Slots![0].DisplayName);
                Assert.AreEqual("UnityEditor.ShaderGraph.Vector4MaterialSlot", colorNodeResult.Node.Slots[0].Type);

                Assert.IsNotNull(floatNodeResult);
                Assert.IsNotNull(floatNodeResult.Node);
                Assert.AreEqual("UnityEditor.ShaderGraph.PropertyNode", floatNodeResult.Node!.Type);
                Assert.AreEqual("_GlowStrength", floatNodeResult.Node.PropertyReferenceName);
                Assert.AreEqual(260f, floatNodeResult.Node.PositionY);
                Assert.IsNotEmpty(floatNodeResult.Node.Slots);
                Assert.AreEqual("Glow Strength", floatNodeResult.Node.Slots![0].DisplayName);
                Assert.AreEqual("UnityEditor.ShaderGraph.Vector1MaterialSlot", floatNodeResult.Node.Slots[0].Type);

                Assert.IsNotNull(textureNodeResult.Node);
                Assert.AreEqual("_BaseMap", textureNodeResult.Node!.PropertyReferenceName);
                Assert.AreEqual("UnityEditor.ShaderGraph.Texture2DMaterialSlot", textureNodeResult.Node.Slots![0].Type);

                Assert.IsNotNull(vector2NodeResult.Node);
                Assert.AreEqual("_UVScale", vector2NodeResult.Node!.PropertyReferenceName);
                Assert.AreEqual("UnityEditor.ShaderGraph.Vector2MaterialSlot", vector2NodeResult.Node.Slots![0].Type);
                StringAssert.Contains("\"x\":0", vector2NodeResult.Node.Slots[0].ValueJson);
                StringAssert.Contains("\"y\":0", vector2NodeResult.Node.Slots[0].ValueJson);

                Assert.IsNotNull(vector3NodeResult.Node);
                Assert.AreEqual("_FlowDirection", vector3NodeResult.Node!.PropertyReferenceName);
                Assert.AreEqual("UnityEditor.ShaderGraph.Vector3MaterialSlot", vector3NodeResult.Node.Slots![0].Type);
                StringAssert.Contains("\"z\":0", vector3NodeResult.Node.Slots[0].ValueJson);

                Assert.IsNotNull(vector4NodeResult.Node);
                Assert.AreEqual("_PackedControls", vector4NodeResult.Node!.PropertyReferenceName);
                Assert.AreEqual("UnityEditor.ShaderGraph.Vector4MaterialSlot", vector4NodeResult.Node.Slots![0].Type);
                StringAssert.Contains("\"w\":0", vector4NodeResult.Node.Slots[0].ValueJson);

                Assert.IsNotNull(booleanNodeResult.Node);
                Assert.AreEqual("_UseDetail", booleanNodeResult.Node!.PropertyReferenceName);
                Assert.AreEqual("UnityEditor.ShaderGraph.BooleanMaterialSlot", booleanNodeResult.Node.Slots![0].Type);
                Assert.AreEqual("false", booleanNodeResult.Node.Slots[0].ValueJson);
                Assert.AreEqual("false", booleanNodeResult.Node.Slots[0].DefaultValueJson);

                Assert.IsNotNull(booleanNodeResult.Structure);
                Assert.IsTrue(booleanNodeResult.Structure!.Nodes!.Any(n => n.PropertyReferenceName == "_BaseColor"));
                Assert.IsTrue(booleanNodeResult.Structure.Nodes.Any(n => n.PropertyReferenceName == "_BaseMap"));
                Assert.IsTrue(booleanNodeResult.Structure.Nodes.Any(n => n.PropertyReferenceName == "_GlowStrength"));
                Assert.IsTrue(booleanNodeResult.Structure.Nodes.Any(n => n.PropertyReferenceName == "_UVScale"));
                Assert.IsTrue(booleanNodeResult.Structure.Nodes.Any(n => n.PropertyReferenceName == "_FlowDirection"));
                Assert.IsTrue(booleanNodeResult.Structure.Nodes.Any(n => n.PropertyReferenceName == "_PackedControls"));
                Assert.IsTrue(booleanNodeResult.Structure.Nodes.Any(n => n.PropertyReferenceName == "_UseDetail"));

                Assert.IsNotNull(booleanNodeResult.Graph);
                Assert.IsTrue(booleanNodeResult.Graph!.ShaderResolved, "Updated Shader Graph should still resolve a compiled shader.");
                Assert.IsFalse(booleanNodeResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Adding safe Property nodes should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddPropertyNode_MissingProperty_Throws()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddPropertyNode_Missing.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                Assert.Throws<InvalidOperationException>(() => tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_MissingProperty",
                        PositionX = -720f,
                        PositionY = 120f
                    }));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddNode_AddsAllowlistedUtilityAndTextureNodes()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddNode.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var sampleTextureNodeResult = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "sampleTexture2D",
                        PositionX = -960f,
                        PositionY = 40f
                    },
                    includeMessages: true,
                    includeProperties: true);

                var branchNodeResult = tool.AddNode(
                    new AssetObjectRef(assetPath),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "branch",
                        PositionX = -560f,
                        PositionY = 260f
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(sampleTextureNodeResult);
                Assert.AreEqual("add", sampleTextureNodeResult.Operation);
                Assert.IsTrue(sampleTextureNodeResult.ChangedFields!.Contains("node.added"));
                Assert.IsNotNull(sampleTextureNodeResult.Node);
                Assert.AreEqual(sampleTextureNodeResult.Node!.ObjectId, sampleTextureNodeResult.NodeObjectId);
                Assert.AreEqual(sampleTextureNodeResult.Node.Type, sampleTextureNodeResult.NodeType);
                Assert.AreEqual("UnityEditor.ShaderGraph.SampleTexture2DNode", sampleTextureNodeResult.Node.Type);
                Assert.AreEqual("Sample Texture 2D", sampleTextureNodeResult.Node.Name);
                Assert.AreEqual(-960f, sampleTextureNodeResult.Node.PositionX);
                Assert.AreEqual(40f, sampleTextureNodeResult.Node.PositionY);
                Assert.IsNotEmpty(sampleTextureNodeResult.Node.Slots);
                Assert.IsTrue(sampleTextureNodeResult.Node.Slots!.Any(s => s.DisplayName == "Texture"));
                Assert.IsTrue(sampleTextureNodeResult.Node.Slots.Any(s => s.DisplayName == "UV"));
                Assert.IsTrue(sampleTextureNodeResult.Node.Slots.Any(s => s.DisplayName == "Sampler"));

                Assert.IsNotNull(branchNodeResult);
                Assert.IsTrue(branchNodeResult.ChangedFields!.Contains("node.added"));
                Assert.IsNotNull(branchNodeResult.Node);
                Assert.AreEqual("UnityEditor.ShaderGraph.BranchNode", branchNodeResult.Node!.Type);
                Assert.AreEqual("Branch", branchNodeResult.Node.Name);
                Assert.AreEqual(-560f, branchNodeResult.Node.PositionX);
                Assert.AreEqual(260f, branchNodeResult.Node.PositionY);
                Assert.IsNotEmpty(branchNodeResult.Node.Slots);
                Assert.IsTrue(branchNodeResult.Node.Slots!.Any(s => s.DisplayName == "Predicate"));
                Assert.IsTrue(branchNodeResult.Node.Slots.Any(s => s.DisplayName == "True"));
                Assert.IsTrue(branchNodeResult.Node.Slots.Any(s => s.DisplayName == "False"));

                Assert.IsNotNull(branchNodeResult.Structure);
                Assert.IsTrue(branchNodeResult.Structure!.Nodes!.Any(n => n.ObjectId == sampleTextureNodeResult.Node.ObjectId));
                Assert.IsTrue(branchNodeResult.Structure.Nodes.Any(n => n.ObjectId == branchNodeResult.Node.ObjectId));

                Assert.IsNotNull(branchNodeResult.Graph);
                Assert.IsTrue(branchNodeResult.Graph!.ShaderResolved, "Adding allowlisted nodes should keep the Shader Graph import valid.");
                Assert.IsFalse(branchNodeResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Adding allowlisted nodes should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddNode_UnsupportedType_Throws()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddNode_Unsupported.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                Assert.Throws<ArgumentException>(() => tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "fresnel"
                    }));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_DuplicateNode_CopiesSupportedNodeWithoutEdges()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_DuplicateNode.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var structureBeforeDuplicate = tool.GetStructure(new AssetObjectRef(shader));
                var multiplyNode = structureBeforeDuplicate.Nodes!
                    .First(n => n.Type == "UnityEditor.ShaderGraph.MultiplyNode");
                var edgeCountBeforeDuplicate = structureBeforeDuplicate.Edges!.Count;
                var nodeCountBeforeDuplicate = structureBeforeDuplicate.Nodes!.Count;

                Assert.IsTrue(structureBeforeDuplicate.Edges.Any(e =>
                        e.OutputNodeId == multiplyNode.ObjectId
                        || e.InputNodeId == multiplyNode.ObjectId),
                    "The validation graph should start with a connected Multiply node.");

                var duplicateResult = tool.DuplicateNode(
                    new AssetObjectRef(assetPath),
                    new ShaderGraphDuplicateNodeInput
                    {
                        NodeObjectId = multiplyNode.ObjectId,
                        PositionOffsetX = 64f,
                        PositionOffsetY = 48f
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(duplicateResult);
                Assert.AreEqual("duplicate", duplicateResult.Operation);
                Assert.IsTrue(duplicateResult.ChangedFields!.Contains("node.duplicated"));
                Assert.IsTrue(duplicateResult.ChangedFields.Contains("node.slot.duplicated"));
                Assert.IsTrue(duplicateResult.ChangedFields.Contains("node.positionX"));
                Assert.IsTrue(duplicateResult.ChangedFields.Contains("node.positionY"));
                Assert.IsNotNull(duplicateResult.Node);
                Assert.AreEqual(duplicateResult.Node!.ObjectId, duplicateResult.NodeObjectId);
                Assert.AreEqual(duplicateResult.Node.Type, duplicateResult.NodeType);
                Assert.AreEqual(multiplyNode.Type, duplicateResult.Node.Type);
                Assert.AreEqual(multiplyNode.Name, duplicateResult.Node.Name);
                Assert.AreNotEqual(multiplyNode.ObjectId, duplicateResult.Node.ObjectId);
                Assert.AreEqual(multiplyNode.PositionX + 64f, duplicateResult.Node.PositionX, 0.001f);
                Assert.AreEqual(multiplyNode.PositionY + 48f, duplicateResult.Node.PositionY, 0.001f);
                Assert.AreEqual(multiplyNode.Slots!.Count, duplicateResult.Node.Slots!.Count);
                CollectionAssert.IsEmpty(multiplyNode.SlotObjectIds!.Intersect(duplicateResult.Node.SlotObjectIds!));

                Assert.IsNotNull(duplicateResult.Structure);
                Assert.AreEqual(nodeCountBeforeDuplicate + 1, duplicateResult.Structure!.Nodes!.Count);
                Assert.AreEqual(edgeCountBeforeDuplicate, duplicateResult.Structure.Edges!.Count,
                    "Duplicating a node should not copy or remove edges.");
                Assert.IsFalse(duplicateResult.Structure.Edges.Any(e =>
                        e.OutputNodeId == duplicateResult.Node.ObjectId
                        || e.InputNodeId == duplicateResult.Node.ObjectId),
                    "Duplicated nodes should start disconnected so agents must wire them explicitly.");

                Assert.IsNotNull(duplicateResult.Graph);
                Assert.IsTrue(duplicateResult.Graph!.ShaderResolved, "Duplicating a supported node should keep the Shader Graph import valid.");
                Assert.IsFalse(duplicateResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Duplicating a supported node should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_DuplicateNode_UnsupportedBlockNode_Throws()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_DuplicateNode_Unsupported.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var structure = tool.GetStructure(new AssetObjectRef(shader));
                var blockNode = structure.Nodes!
                    .First(n => n.Type == "UnityEditor.ShaderGraph.BlockNode");

                Assert.Throws<InvalidOperationException>(() => tool.DuplicateNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphDuplicateNodeInput
                    {
                        NodeObjectId = blockNode.ObjectId
                    }));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_DeleteNode_RemovesNodeAndConnectedEdges()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_DeleteNode.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var structureBeforeDelete = tool.GetStructure(new AssetObjectRef(shader));
                var baseColorNode = structureBeforeDelete.Nodes!
                    .First(n => n.Type == "UnityEditor.ShaderGraph.PropertyNode"
                        && n.PropertyReferenceName == "_BaseColor");

                Assert.IsTrue(structureBeforeDelete.Edges!.Any(e => e.OutputNodeId == baseColorNode.ObjectId),
                    "The validation graph should start with at least one edge sourced from the _BaseColor PropertyNode.");

                var deleteResult = tool.DeleteNode(
                    new AssetObjectRef(assetPath),
                    new ShaderGraphDeleteNodeInput
                    {
                        NodeObjectId = baseColorNode.ObjectId
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(deleteResult);
                Assert.AreEqual("delete", deleteResult.Operation);
                Assert.IsTrue(deleteResult.ChangedFields!.Contains("node.deleted"));
                Assert.IsTrue(deleteResult.ChangedFields.Contains("edge.autoRemoved"),
                    "Deleting a connected node should report that edges were cleaned up automatically.");
                Assert.IsNotNull(deleteResult.Node);
                Assert.AreEqual(baseColorNode.ObjectId, deleteResult.NodeObjectId);
                Assert.AreEqual("UnityEditor.ShaderGraph.PropertyNode", deleteResult.NodeType);
                Assert.AreEqual(baseColorNode.ObjectId, deleteResult.Node!.ObjectId);
                Assert.AreEqual("UnityEditor.ShaderGraph.PropertyNode", deleteResult.Node.Type);
                Assert.AreEqual(1, deleteResult.RemovedEdgeCount,
                    "The template-derived _BaseColor PropertyNode should contribute exactly one edge in the validation graph.");

                Assert.IsNotNull(deleteResult.Structure);
                Assert.IsFalse(deleteResult.Structure!.Nodes!.Any(n => n.ObjectId == baseColorNode.ObjectId));
                Assert.IsFalse(deleteResult.Structure.Edges!.Any(e =>
                    e.OutputNodeId == baseColorNode.ObjectId
                    || e.InputNodeId == baseColorNode.ObjectId),
                    "No remaining edges should reference the deleted node.");
                Assert.IsTrue(deleteResult.Structure.Properties!.Any(p => p.OverrideReferenceName == "_BaseColor"),
                    "Deleting a PropertyNode should not remove the underlying blackboard property.");

                Assert.IsNotNull(deleteResult.Graph);
                Assert.IsTrue(deleteResult.Graph!.ShaderResolved, "Deleting a node should keep the Shader Graph import valid.");
                Assert.IsFalse(deleteResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Deleting a node through Unity's graph API should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_DeleteNode_NodeNotFound_Throws()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_DeleteNode_NotFound.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                Assert.Throws<InvalidOperationException>(() => tool.DeleteNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphDeleteNodeInput
                    {
                        NodeObjectId = "missing-node-id"
                    }));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateNodeSettings_UpdatesSampleTexture2DModes()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodeSettings.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var structureBeforeUpdate = tool.GetStructure(new AssetObjectRef(shader));
                var sampleTextureNode = structureBeforeUpdate.Nodes!
                    .First(n => n.Type == "UnityEditor.ShaderGraph.SampleTexture2DNode");

                var result = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = sampleTextureNode.ObjectId,
                        SampleTexture2D = new ShaderGraphSampleTexture2DNodeSettingsUpdateInput
                        {
                            TextureType = "normal",
                            NormalMapSpace = "object",
                            UseGlobalMipBias = false,
                            MipSamplingMode = "gradient"
                        }
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(result);
                Assert.IsTrue(result.ChangedFields!.Contains("node.sampleTexture2D.textureType"));
                Assert.IsTrue(result.ChangedFields.Contains("node.sampleTexture2D.normalMapSpace"));
                Assert.IsTrue(result.ChangedFields.Contains("node.sampleTexture2D.useGlobalMipBias"));
                Assert.IsTrue(result.ChangedFields.Contains("node.sampleTexture2D.mipSamplingMode"));

                Assert.IsNotNull(result.Node);
                Assert.AreEqual(sampleTextureNode.ObjectId, result.Node!.ObjectId);
                Assert.IsNotNull(result.Node.SampleTexture2D);
                Assert.AreEqual("normal", result.Node.SampleTexture2D!.TextureType);
                Assert.AreEqual("object", result.Node.SampleTexture2D.NormalMapSpace);
                Assert.IsFalse(result.Node.SampleTexture2D.UseGlobalMipBias ?? true);
                Assert.AreEqual("gradient", result.Node.SampleTexture2D.MipSamplingMode);

                Assert.IsNotEmpty(result.Node.Slots);
                Assert.IsTrue(result.Node.Slots!.Any(s => s.DisplayName == "DDX"),
                    "Gradient mip sampling mode should expose a DDX input slot.");
                Assert.IsTrue(result.Node.Slots.Any(s => s.DisplayName == "DDY"),
                    "Gradient mip sampling mode should expose a DDY input slot.");
                Assert.IsFalse(result.Node.Slots.Any(s => s.DisplayName == "Bias"),
                    "Gradient mip sampling mode should not keep the Bias slot active.");
                Assert.IsFalse(result.Node.Slots.Any(s => s.DisplayName == "LOD"),
                    "Gradient mip sampling mode should not keep the LOD slot active.");

                Assert.IsNotNull(result.Graph);
                Assert.IsTrue(result.Graph!.ShaderResolved, "Updating supported Sample Texture 2D settings should keep the Shader Graph import valid.");
                Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Updating supported Sample Texture 2D settings should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateNodeSettings_ReloadsOpenShaderGraphWindow()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodeSettings_WindowReload.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                OpenShaderGraphWindow(assetPath);

                var tool = new Tool_Assets_ShaderGraph();
                var structureBeforeUpdate = tool.GetStructure(new AssetObjectRef(shader));
                var sampleTextureNode = structureBeforeUpdate.Nodes!
                    .First(n => n.Type == "UnityEditor.ShaderGraph.SampleTexture2DNode");

                var windowStateBeforeUpdate = ReadOpenSampleTexture2DWindowState(assetPath);
                Assert.IsNotNull(windowStateBeforeUpdate, "Expected a Shader Graph editor window to be open for the validation asset.");
                Assert.AreEqual("Default", windowStateBeforeUpdate!.TextureType);
                Assert.AreEqual("Tangent", windowStateBeforeUpdate.NormalMapSpace);
                Assert.IsTrue(windowStateBeforeUpdate.UseGlobalMipBias);
                Assert.AreEqual("Standard", windowStateBeforeUpdate.MipSamplingMode);

                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = sampleTextureNode.ObjectId,
                        SampleTexture2D = new ShaderGraphSampleTexture2DNodeSettingsUpdateInput
                        {
                            TextureType = "normal",
                            NormalMapSpace = "object",
                            UseGlobalMipBias = false,
                            MipSamplingMode = "gradient"
                        }
                    });

                var windowStateAfterUpdate = ReadOpenSampleTexture2DWindowState(assetPath);
                Assert.IsNotNull(windowStateAfterUpdate, "Expected the Shader Graph editor window to stay open after reload.");
                Assert.AreEqual("Normal", windowStateAfterUpdate!.TextureType);
                Assert.AreEqual("Object", windowStateAfterUpdate.NormalMapSpace);
                Assert.IsFalse(windowStateAfterUpdate.UseGlobalMipBias);
                Assert.AreEqual("Gradient", windowStateAfterUpdate.MipSamplingMode);
            }
            finally
            {
                CloseShaderGraphWindows(assetPath);
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateNodeSettings_ReloadsExpandedNodeWindowState()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodeSettings_Expanded_WindowReload.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var tilingNode = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "tilingAndOffset", PositionX = -900f, PositionY = 0f });
                var branchNode = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "branch", PositionX = -900f, PositionY = 180f });
                var combineNode = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "combine", PositionX = -900f, PositionY = 360f });

                OpenShaderGraphWindow(assetPath);

                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = tilingNode.Node!.ObjectId,
                        TilingAndOffset = new ShaderGraphTilingAndOffsetNodeSettingsUpdateInput
                        {
                            Tiling = new ShaderGraphVector2ValueUpdateInput { X = 2f, Y = 3f },
                            Offset = new ShaderGraphVector2ValueUpdateInput { X = 0.25f, Y = 0.75f }
                        }
                    });

                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = branchNode.Node!.ObjectId,
                        Branch = new ShaderGraphBranchNodeSettingsUpdateInput
                        {
                            Predicate = true,
                            TrueValue = new ShaderGraphVector4ValueUpdateInput { X = 1f, Y = 0.5f, Z = 0.25f, W = 1f },
                            FalseValue = new ShaderGraphVector4ValueUpdateInput { X = 0.1f, Y = 0.2f, Z = 0.3f, W = 0.4f }
                        }
                    });

                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = combineNode.Node!.ObjectId,
                        Combine = new ShaderGraphCombineNodeSettingsUpdateInput
                        {
                            R = 0.11f,
                            G = 0.22f,
                            B = 0.33f,
                            A = 0.44f
                        }
                    });

                var tilingSlot = ResolveOpenShaderGraphInputSlot(assetPath, "TilingAndOffsetNode", "Tiling");
                var offsetSlot = ResolveOpenShaderGraphInputSlot(assetPath, "TilingAndOffsetNode", "Offset");
                var predicateSlot = ResolveOpenShaderGraphInputSlot(assetPath, "BranchNode", "Predicate");
                var trueSlot = ResolveOpenShaderGraphInputSlot(assetPath, "BranchNode", "True");
                var combineRSlot = ResolveOpenShaderGraphInputSlot(assetPath, "CombineNode", "R");

                AssertPrivateVector2Field(tilingSlot, "m_Value", 2f, 3f, "Tiling value");
                AssertPrivateVector2Field(tilingSlot, "m_DefaultValue", 2f, 3f, "Tiling defaultValue");
                AssertPrivateVector2Field(offsetSlot, "m_Value", 0.25f, 0.75f, "Offset value");
                AssertPrivateVector2Field(offsetSlot, "m_DefaultValue", 0.25f, 0.75f, "Offset defaultValue");

                Assert.AreEqual(true, ReadPrivateField<bool>(predicateSlot, "m_Value"), "Unexpected Branch.Predicate value.");
                Assert.AreEqual(true, ReadPrivateField<bool>(predicateSlot, "m_DefaultValue"), "Unexpected Branch.Predicate defaultValue.");

                AssertPrivateVector4Field(trueSlot, "m_Value", 1f, 0.5f, 0.25f, 1f, "Branch.True value");
                AssertPrivateVector4Field(trueSlot, "m_DefaultValue", 1f, 0.5f, 0.25f, 1f, "Branch.True defaultValue");
                Assert.IsTrue(ReadPrivateField<bool>(trueSlot, "m_LiteralMode"), "Expected Branch.True to stay in literal mode.");
                Assert.AreEqual("Vector4", ReadPrivateField<object>(trueSlot, "m_ConcreteValueType").ToString(), "Expected Branch.True to keep a Vector4 concrete type.");

                Assert.AreEqual(0.11f, ReadPrivateField<float>(combineRSlot, "m_Value"), 0.0001f, "Unexpected Combine.R value.");
                Assert.AreEqual(0.11f, ReadPrivateField<float>(combineRSlot, "m_DefaultValue"), 0.0001f, "Unexpected Combine.R defaultValue.");
                Assert.IsTrue(ReadPrivateField<bool>(combineRSlot, "m_LiteralMode"), "Expected Combine.R to stay in literal mode.");
            }
            finally
            {
                CloseShaderGraphWindows(assetPath);
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateNodeSettings_UpdatesExpandedNodeDefaultSlots()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodeSettings_Expanded.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var tilingNode = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "tilingAndOffset", PositionX = -900f, PositionY = 0f });
                var branchNode = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "branch", PositionX = -900f, PositionY = 180f });
                var splitNode = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "split", PositionX = -900f, PositionY = 360f });
                var combineNode = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "combine", PositionX = -900f, PositionY = 540f });
                var addNode = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "add", PositionX = -620f, PositionY = 0f });
                var subtractNode = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "subtract", PositionX = -620f, PositionY = 180f });
                var divideNode = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "divide", PositionX = -620f, PositionY = 360f });
                var lerpNode = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "lerp", PositionX = -620f, PositionY = 540f });
                var oneMinusNode = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "oneMinus", PositionX = -620f, PositionY = 720f });

                var tilingResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = tilingNode.Node!.ObjectId,
                        TilingAndOffset = new ShaderGraphTilingAndOffsetNodeSettingsUpdateInput
                        {
                            Tiling = new ShaderGraphVector2ValueUpdateInput { X = 2f, Y = 3f },
                            Offset = new ShaderGraphVector2ValueUpdateInput { X = 0.25f, Y = 0.75f }
                        }
                    },
                    includeMessages: true,
                    includeProperties: true);

                var branchResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = branchNode.Node!.ObjectId,
                        Branch = new ShaderGraphBranchNodeSettingsUpdateInput
                        {
                            Predicate = true,
                            TrueValue = new ShaderGraphVector4ValueUpdateInput { X = 1f, Y = 0.5f, Z = 0.25f, W = 1f },
                            FalseValue = new ShaderGraphVector4ValueUpdateInput { X = 0.1f, Y = 0.2f, Z = 0.3f, W = 0.4f }
                        }
                    },
                    includeMessages: true,
                    includeProperties: true);

                var splitResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = splitNode.Node!.ObjectId,
                        Split = new ShaderGraphSplitNodeSettingsUpdateInput
                        {
                            Input = new ShaderGraphVector4ValueUpdateInput { X = 0.9f, Y = 0.8f, Z = 0.7f, W = 0.6f }
                        }
                    });

                var combineResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = combineNode.Node!.ObjectId,
                        Combine = new ShaderGraphCombineNodeSettingsUpdateInput
                        {
                            R = 0.11f,
                            G = 0.22f,
                            B = 0.33f,
                            A = 0.44f
                        }
                    });

                var addResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = addNode.Node!.ObjectId,
                        Add = new ShaderGraphBinaryVectorNodeSettingsUpdateInput
                        {
                            A = new ShaderGraphVector4ValueUpdateInput { X = 1f, Y = 2f, Z = 3f, W = 4f },
                            B = new ShaderGraphVector4ValueUpdateInput { X = 5f, Y = 6f, Z = 7f, W = 8f }
                        }
                    });

                var subtractResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = subtractNode.Node!.ObjectId,
                        Subtract = new ShaderGraphBinaryVectorNodeSettingsUpdateInput
                        {
                            A = new ShaderGraphVector4ValueUpdateInput { X = 8f, Y = 7f, Z = 6f, W = 5f },
                            B = new ShaderGraphVector4ValueUpdateInput { X = 4f, Y = 3f, Z = 2f, W = 1f }
                        }
                    });

                var divideResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = divideNode.Node!.ObjectId,
                        Divide = new ShaderGraphBinaryVectorNodeSettingsUpdateInput
                        {
                            A = new ShaderGraphVector4ValueUpdateInput { X = 16f, Y = 12f, Z = 8f, W = 4f },
                            B = new ShaderGraphVector4ValueUpdateInput { X = 2f, Y = 3f, Z = 4f, W = 5f }
                        }
                    });

                var lerpResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = lerpNode.Node!.ObjectId,
                        Lerp = new ShaderGraphLerpNodeSettingsUpdateInput
                        {
                            A = new ShaderGraphVector4ValueUpdateInput { X = 0f, Y = 0.25f, Z = 0.5f, W = 0.75f },
                            B = new ShaderGraphVector4ValueUpdateInput { X = 1f, Y = 0.75f, Z = 0.5f, W = 0.25f },
                            T = new ShaderGraphVector4ValueUpdateInput { X = 0.4f, Y = 0.4f, Z = 0.4f, W = 0.4f }
                        }
                    });

                var oneMinusResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = oneMinusNode.Node!.ObjectId,
                        OneMinus = new ShaderGraphOneMinusNodeSettingsUpdateInput
                        {
                            Input = new ShaderGraphVector4ValueUpdateInput { X = 0.2f, Y = 0.4f, Z = 0.6f, W = 0.8f }
                        }
                    },
                    includeMessages: true,
                    includeProperties: true);

                AssertSlotVector2(tilingResult.Node!, "Tiling", 2f, 3f);
                AssertSlotVector2(tilingResult.Node!, "Offset", 0.25f, 0.75f);

                AssertSlotBool(branchResult.Node!, "Predicate", true);
                AssertSlotVector4(branchResult.Node!, "True", 1f, 0.5f, 0.25f, 1f);
                AssertSlotVector4(branchResult.Node!, "False", 0.1f, 0.2f, 0.3f, 0.4f);

                AssertSlotVector4(splitResult.Node!, "In", 0.9f, 0.8f, 0.7f, 0.6f);
                AssertSlotFloat(combineResult.Node!, "R", 0.11f);
                AssertSlotFloat(combineResult.Node!, "G", 0.22f);
                AssertSlotFloat(combineResult.Node!, "B", 0.33f);
                AssertSlotFloat(combineResult.Node!, "A", 0.44f);

                AssertSlotVector4(addResult.Node!, "A", 1f, 2f, 3f, 4f);
                AssertSlotVector4(addResult.Node!, "B", 5f, 6f, 7f, 8f);
                AssertSlotVector4(subtractResult.Node!, "A", 8f, 7f, 6f, 5f);
                AssertSlotVector4(subtractResult.Node!, "B", 4f, 3f, 2f, 1f);
                AssertSlotVector4(divideResult.Node!, "A", 16f, 12f, 8f, 4f);
                AssertSlotVector4(divideResult.Node!, "B", 2f, 3f, 4f, 5f);

                AssertSlotVector4(lerpResult.Node!, "A", 0f, 0.25f, 0.5f, 0.75f);
                AssertSlotVector4(lerpResult.Node!, "B", 1f, 0.75f, 0.5f, 0.25f);
                AssertSlotVector4(lerpResult.Node!, "T", 0.4f, 0.4f, 0.4f, 0.4f);
                AssertSlotVector4(oneMinusResult.Node!, "In", 0.2f, 0.4f, 0.6f, 0.8f);

                Assert.IsNotNull(oneMinusResult.Graph);
                Assert.IsTrue(oneMinusResult.Graph!.ShaderResolved, "Updating supported node default slots should keep the Shader Graph import valid.");
                Assert.IsFalse(oneMinusResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Updating supported node default slots should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateNodeSettings_UpdatesMultiplyType()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodeSettings_Multiply.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var multiplyNodeResult = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "multiply", PositionX = -620f, PositionY = 40f });

                var result = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = multiplyNodeResult.Node!.ObjectId,
                        Multiply = new ShaderGraphMultiplyNodeSettingsUpdateInput
                        {
                            MultiplyType = "matrix"
                        }
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(result);
                Assert.IsTrue(result.ChangedFields!.Contains("node.multiply.multiplyType"));
                Assert.IsNotNull(result.Node);
                Assert.IsNotNull(result.Node!.Multiply);
                Assert.AreEqual("matrix", result.Node.Multiply!.MultiplyType);

                Assert.IsNotNull(result.Graph);
                Assert.IsTrue(result.Graph!.ShaderResolved, "Updating Multiply settings should keep the Shader Graph import valid.");
                Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Updating Multiply settings should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateNodeSettings_UnsupportedNodeFamily_Throws()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodeSettings_Unsupported.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var structure = tool.GetStructure(new AssetObjectRef(shader));
                var multiplyNode = structure.Nodes!
                    .First(n => n.Name == "Multiply");

                Assert.Throws<InvalidOperationException>(() => tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = multiplyNode.ObjectId,
                        SampleTexture2D = new ShaderGraphSampleTexture2DNodeSettingsUpdateInput
                        {
                            TextureType = "normal"
                        }
                    }));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateNodePosition_MovesExistingNodes()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodePosition.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Glow Strength",
                        OverrideReferenceName = "_GlowStrength",
                        FloatValue = 0.75f
                    });

                var structureBeforeMove = tool.GetStructure(new AssetObjectRef(shader));
                var existingColorNode = structureBeforeMove.Nodes!
                    .First(n => n.Type == "UnityEditor.ShaderGraph.PropertyNode"
                        && n.PropertyReferenceName == "_BaseColor");

                var glowStrengthNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_GlowStrength",
                        PositionX = -720f,
                        PositionY = 260f
                    });

                var movedColorNode = tool.UpdateNodePosition(
                    new AssetObjectRef(assetPath),
                    new ShaderGraphUpdateNodePositionInput
                    {
                        NodeObjectId = existingColorNode.ObjectId,
                        PositionX = -240f,
                        PositionY = 120f
                    },
                    includeMessages: true,
                    includeProperties: true);

                var movedFloatNode = tool.UpdateNodePosition(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodePositionInput
                    {
                        NodeObjectId = glowStrengthNodeResult.Node!.ObjectId,
                        PositionX = -240f,
                        PositionY = 220f
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(movedColorNode);
                Assert.AreEqual("updatePosition", movedColorNode.Operation);
                Assert.IsNotNull(movedColorNode.Node);
                Assert.AreEqual(baseColorNode.ObjectId, movedColorNode.NodeObjectId);
                Assert.AreEqual("UnityEditor.ShaderGraph.PropertyNode", movedColorNode.NodeType);
                Assert.IsTrue(movedColorNode.ChangedFields!.Contains("node.positionX"));
                Assert.IsTrue(movedColorNode.ChangedFields.Contains("node.positionY"));
                Assert.AreEqual(-240f, movedColorNode.Node!.PositionX);
                Assert.AreEqual(120f, movedColorNode.Node.PositionY);

                Assert.IsNotNull(movedFloatNode);
                Assert.IsNotNull(movedFloatNode.Node);
                Assert.AreEqual(-240f, movedFloatNode.Node!.PositionX);
                Assert.AreEqual(220f, movedFloatNode.Node.PositionY);
                Assert.AreEqual("_GlowStrength", movedFloatNode.Node.PropertyReferenceName);

                Assert.IsNotNull(movedFloatNode.Structure);
                Assert.IsTrue(movedFloatNode.Structure!.Nodes!.Any(n =>
                    n.ObjectId == existingColorNode.ObjectId
                    && n.PositionX == -240f
                    && n.PositionY == 120f));
                Assert.IsTrue(movedFloatNode.Structure.Nodes.Any(n =>
                    n.ObjectId == glowStrengthNodeResult.Node.ObjectId
                    && n.PositionX == -240f
                    && n.PositionY == 220f));
                Assert.AreEqual(1, movedFloatNode.Structure.Nodes.Count(n =>
                        n.Type == "UnityEditor.ShaderGraph.PropertyNode"
                        && n.PropertyReferenceName == "_BaseColor"),
                    "The move-node validation setup should keep a single _BaseColor PropertyNode.");
                Assert.AreEqual(1, movedFloatNode.Structure.Nodes.Count(n =>
                        n.Type == "UnityEditor.ShaderGraph.PropertyNode"
                        && n.PropertyReferenceName == "_GlowStrength"),
                    "The move-node validation setup should keep a single _GlowStrength PropertyNode.");

                Assert.IsNotNull(movedFloatNode.Graph);
                Assert.IsTrue(movedFloatNode.Graph!.ShaderResolved, "Moving existing nodes should keep the shader import valid.");
                Assert.IsFalse(movedFloatNode.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Moving nodes should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateNodePosition_NodeNotFound_Throws()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodePosition_NotFound.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                Assert.Throws<InvalidOperationException>(() => tool.UpdateNodePosition(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodePositionInput
                    {
                        NodeObjectId = "missing-node-id",
                        PositionX = 100f
                    }));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ConnectAndDisconnectEdge_ReroutesMultiplyColorInput()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateEdge.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "color",
                        DisplayName = "Accent",
                        OverrideReferenceName = "_AccentColor",
                        ColorHex = "#44CC88FF"
                    });

                var accentNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_AccentColor",
                        PositionX = -720f,
                        PositionY = 120f
                    });

                var structureBeforeReroute = tool.GetStructure(new AssetObjectRef(shader));
                var multiplyNode = structureBeforeReroute.Nodes!
                    .First(n => n.Name == "Multiply");
                var multiplyInputB = multiplyNode.Slots!
                    .First(s => s.DisplayName == "B");
                var baseColorNode = structureBeforeReroute.Nodes
                    .First(n => n.Type == "UnityEditor.ShaderGraph.PropertyNode"
                        && n.PropertyReferenceName == "_BaseColor");
                var baseColorOutput = baseColorNode.Slots!.Single();
                var accentOutput = accentNodeResult.Node!.Slots!.Single();

                var disconnectResult = tool.DisconnectEdge(
                    new AssetObjectRef(assetPath),
                    new ShaderGraphDisconnectEdgeInput
                    {
                        OutputNodeObjectId = baseColorNode.ObjectId,
                        OutputSlotObjectId = baseColorOutput.ObjectId,
                        InputNodeObjectId = multiplyNode.ObjectId,
                        InputSlotObjectId = multiplyInputB.ObjectId
                    },
                    includeMessages: true,
                    includeProperties: true);

                var connectResult = tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = accentNodeResult.Node.ObjectId,
                        OutputSlotObjectId = accentOutput.ObjectId,
                        InputNodeObjectId = multiplyNode.ObjectId,
                        InputSlotObjectId = multiplyInputB.ObjectId
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(disconnectResult);
                Assert.IsTrue(disconnectResult.ChangedFields!.Contains("edge.disconnected"));
                Assert.IsNotNull(disconnectResult.Edge);
                Assert.AreEqual(baseColorNode.ObjectId, disconnectResult.Edge!.OutputNodeId);
                Assert.AreEqual(multiplyNode.ObjectId, disconnectResult.Edge.InputNodeId);
                Assert.AreEqual(multiplyInputB.SlotId, disconnectResult.Edge.InputSlotId);

                Assert.IsNotNull(connectResult);
                Assert.IsTrue(connectResult.ChangedFields!.Contains("edge.connected"));
                Assert.IsNotNull(connectResult.Edge);
                Assert.AreEqual(accentNodeResult.Node.ObjectId, connectResult.Edge!.OutputNodeId);
                Assert.AreEqual(multiplyNode.ObjectId, connectResult.Edge.InputNodeId);
                Assert.AreEqual(multiplyInputB.SlotId, connectResult.Edge.InputSlotId);

                Assert.IsNotNull(connectResult.Structure);
                Assert.IsTrue(connectResult.Structure!.Edges!.Any(e =>
                    e.OutputNodeId == accentNodeResult.Node.ObjectId
                    && e.OutputSlotId == accentOutput.SlotId
                    && e.InputNodeId == multiplyNode.ObjectId
                    && e.InputSlotId == multiplyInputB.SlotId));
                Assert.IsFalse(connectResult.Structure.Edges.Any(e =>
                    e.OutputNodeId == baseColorNode.ObjectId
                    && e.OutputSlotId == baseColorOutput.SlotId
                    && e.InputNodeId == multiplyNode.ObjectId
                    && e.InputSlotId == multiplyInputB.SlotId),
                    "The previous _BaseColor connection into Multiply.B should be removed.");

                Assert.IsNotNull(connectResult.Graph);
                Assert.IsTrue(connectResult.Graph!.ShaderResolved, "Rerouting a compatible edge should keep the shader import valid.");
                Assert.IsFalse(connectResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Disconnecting and reconnecting a compatible edge should not introduce import errors.");
                Assert.IsTrue(connectResult.Graph.Properties!.Any(p => p.Name == "_AccentColor"),
                    "Compiled shader properties should still include the added Accent color property.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ReconnectEdge_RetargetsMultiplyColorInputToAccentPropertyNode()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_ReconnectEdge_Output.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "color",
                        DisplayName = "Accent",
                        OverrideReferenceName = "_AccentColor",
                        ColorHex = "#44CC88FF"
                    });

                var accentNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_AccentColor",
                        PositionX = -720f,
                        PositionY = 120f
                    });

                var structureBeforeReconnect = tool.GetStructure(new AssetObjectRef(shader));
                var multiplyNode = structureBeforeReconnect.Nodes!
                    .First(n => n.Name == "Multiply");
                var multiplyInputB = multiplyNode.Slots!
                    .First(s => s.DisplayName == "B");
                var baseColorNode = structureBeforeReconnect.Nodes
                    .First(n => n.Type == "UnityEditor.ShaderGraph.PropertyNode"
                        && n.PropertyReferenceName == "_BaseColor");
                var baseColorOutput = baseColorNode.Slots!.Single();
                var accentOutput = accentNodeResult.Node!.Slots!.Single();

                var reconnectResult = tool.ReconnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphReconnectEdgeInput
                    {
                        ExistingOutputNodeObjectId = baseColorNode.ObjectId,
                        ExistingOutputSlotObjectId = baseColorOutput.ObjectId,
                        ExistingInputNodeObjectId = multiplyNode.ObjectId,
                        ExistingInputSlotObjectId = multiplyInputB.ObjectId,
                        NewOutputNodeObjectId = accentNodeResult.Node.ObjectId,
                        NewOutputSlotObjectId = accentOutput.ObjectId
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(reconnectResult);
                Assert.IsTrue(reconnectResult.ChangedFields!.Contains("edge.disconnected"));
                Assert.IsTrue(reconnectResult.ChangedFields.Contains("edge.reconnected"));
                Assert.IsTrue(reconnectResult.ChangedFields.Contains("edge.connected"));
                Assert.IsNotNull(reconnectResult.RemovedEdge);
                Assert.AreEqual(baseColorNode.ObjectId, reconnectResult.RemovedEdge!.OutputNodeId);
                Assert.AreEqual(baseColorOutput.SlotId, reconnectResult.RemovedEdge.OutputSlotId);
                Assert.AreEqual(multiplyNode.ObjectId, reconnectResult.RemovedEdge.InputNodeId);
                Assert.AreEqual(multiplyInputB.SlotId, reconnectResult.RemovedEdge.InputSlotId);

                Assert.IsNotNull(reconnectResult.RemovedEdges);
                Assert.AreEqual(1, reconnectResult.RemovedEdges!.Count);
                Assert.IsNotNull(reconnectResult.Edge);
                Assert.AreEqual(accentNodeResult.Node.ObjectId, reconnectResult.Edge!.OutputNodeId);
                Assert.AreEqual(accentOutput.SlotId, reconnectResult.Edge.OutputSlotId);
                Assert.AreEqual(multiplyNode.ObjectId, reconnectResult.Edge.InputNodeId);
                Assert.AreEqual(multiplyInputB.SlotId, reconnectResult.Edge.InputSlotId);

                Assert.IsNotNull(reconnectResult.Structure);
                Assert.IsTrue(reconnectResult.Structure!.Edges!.Any(e =>
                    e.OutputNodeId == accentNodeResult.Node.ObjectId
                    && e.OutputSlotId == accentOutput.SlotId
                    && e.InputNodeId == multiplyNode.ObjectId
                    && e.InputSlotId == multiplyInputB.SlotId));
                Assert.IsFalse(reconnectResult.Structure.Edges.Any(e =>
                    e.OutputNodeId == baseColorNode.ObjectId
                    && e.OutputSlotId == baseColorOutput.SlotId
                    && e.InputNodeId == multiplyNode.ObjectId
                    && e.InputSlotId == multiplyInputB.SlotId),
                    "The previous _BaseColor connection into Multiply.B should be removed during reconnect.");

                Assert.IsNotNull(reconnectResult.Graph);
                Assert.IsTrue(reconnectResult.Graph!.ShaderResolved, "Reconnecting an existing edge to a new output should keep the shader import valid.");
                Assert.IsFalse(reconnectResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Reconnecting an existing edge to a new output should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ReconnectEdge_RetargetsMultiplyOutputToEmissionBlock()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_ReconnectEdge_Input.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var structureBeforeReconnect = tool.GetStructure(new AssetObjectRef(shader));
                var multiplyNode = structureBeforeReconnect.Nodes!
                    .First(n => n.Name == "Multiply");
                var multiplyOutput = multiplyNode.Slots!
                    .First(s => s.DisplayName == "Out");
                var baseColorBlock = structureBeforeReconnect.Nodes
                    .First(n => n.Name == "SurfaceDescription.BaseColor");
                var baseColorInput = baseColorBlock.Slots!.Single();
                var emissionBlock = structureBeforeReconnect.Nodes
                    .First(n => n.Name == "SurfaceDescription.Emission");
                var emissionInput = emissionBlock.Slots!.Single();

                var reconnectResult = tool.ReconnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphReconnectEdgeInput
                    {
                        ExistingOutputNodeObjectId = multiplyNode.ObjectId,
                        ExistingOutputSlotObjectId = multiplyOutput.ObjectId,
                        ExistingInputNodeObjectId = baseColorBlock.ObjectId,
                        ExistingInputSlotObjectId = baseColorInput.ObjectId,
                        NewInputNodeObjectId = emissionBlock.ObjectId,
                        NewInputSlotObjectId = emissionInput.ObjectId
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(reconnectResult);
                Assert.IsTrue(reconnectResult.ChangedFields!.Contains("edge.disconnected"));
                Assert.IsTrue(reconnectResult.ChangedFields.Contains("edge.reconnected"));
                Assert.IsTrue(reconnectResult.ChangedFields.Contains("edge.connected"));

                Assert.IsNotNull(reconnectResult.Edge);
                Assert.AreEqual(multiplyNode.ObjectId, reconnectResult.Edge!.OutputNodeId);
                Assert.AreEqual(multiplyOutput.SlotId, reconnectResult.Edge.OutputSlotId);
                Assert.AreEqual(emissionBlock.ObjectId, reconnectResult.Edge.InputNodeId);
                Assert.AreEqual(emissionInput.SlotId, reconnectResult.Edge.InputSlotId);

                Assert.IsNotNull(reconnectResult.RemovedEdges);
                Assert.AreEqual(1, reconnectResult.RemovedEdges!.Count);
                Assert.AreEqual(baseColorBlock.ObjectId, reconnectResult.RemovedEdges[0].InputNodeId);
                Assert.AreEqual(baseColorInput.SlotId, reconnectResult.RemovedEdges[0].InputSlotId);

                Assert.IsNotNull(reconnectResult.Structure);
                Assert.IsTrue(reconnectResult.Structure!.Edges!.Any(e =>
                    e.OutputNodeId == multiplyNode.ObjectId
                    && e.OutputSlotId == multiplyOutput.SlotId
                    && e.InputNodeId == emissionBlock.ObjectId
                    && e.InputSlotId == emissionInput.SlotId));
                Assert.IsFalse(reconnectResult.Structure.Edges.Any(e =>
                    e.OutputNodeId == multiplyNode.ObjectId
                    && e.OutputSlotId == multiplyOutput.SlotId
                    && e.InputNodeId == baseColorBlock.ObjectId
                    && e.InputSlotId == baseColorInput.SlotId),
                    "The previous Multiply.Out -> Base Color edge should be removed during reconnect.");

                Assert.IsNotNull(reconnectResult.Graph);
                Assert.IsTrue(reconnectResult.Graph!.ShaderResolved, "Reconnecting an existing edge to a new input should keep the shader import valid.");
                Assert.IsFalse(reconnectResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Reconnecting an existing edge to a new input should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ReconnectEdge_RetargetsSampleTextureInputToTexturePropertyNode()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_ReconnectEdge_Texture.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "texture2D",
                        DisplayName = "Detail Map",
                        OverrideReferenceName = "_DetailMap",
                        TextureDefaultType = "black"
                    });

                var detailNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_DetailMap",
                        PositionX = -760f,
                        PositionY = 20f
                    });

                var structureBeforeReconnect = tool.GetStructure(new AssetObjectRef(shader));
                var sampleTextureNode = structureBeforeReconnect.Nodes!
                    .First(n => n.Name == "Sample Texture 2D");
                var sampleTextureInput = sampleTextureNode.Slots!
                    .First(s => s.DisplayName == "Texture");
                var baseMapNode = structureBeforeReconnect.Nodes
                    .First(n => n.Type == "UnityEditor.ShaderGraph.PropertyNode"
                        && n.PropertyReferenceName == "_BaseMap");
                var baseMapOutput = baseMapNode.Slots!.Single();
                var detailOutput = detailNodeResult.Node!.Slots!.Single();

                var reconnectResult = tool.ReconnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphReconnectEdgeInput
                    {
                        ExistingOutputNodeObjectId = baseMapNode.ObjectId,
                        ExistingOutputSlotObjectId = baseMapOutput.ObjectId,
                        ExistingInputNodeObjectId = sampleTextureNode.ObjectId,
                        ExistingInputSlotObjectId = sampleTextureInput.ObjectId,
                        NewOutputNodeObjectId = detailNodeResult.Node.ObjectId,
                        NewOutputSlotObjectId = detailOutput.ObjectId
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(reconnectResult);
                Assert.IsTrue(reconnectResult.ChangedFields!.Contains("edge.disconnected"));
                Assert.IsTrue(reconnectResult.ChangedFields.Contains("edge.reconnected"));
                Assert.IsTrue(reconnectResult.ChangedFields.Contains("edge.connected"));

                Assert.IsNotNull(reconnectResult.RemovedEdge);
                Assert.AreEqual(baseMapNode.ObjectId, reconnectResult.RemovedEdge!.OutputNodeId);
                Assert.AreEqual(baseMapOutput.SlotId, reconnectResult.RemovedEdge.OutputSlotId);
                Assert.AreEqual(sampleTextureNode.ObjectId, reconnectResult.RemovedEdge.InputNodeId);
                Assert.AreEqual(sampleTextureInput.SlotId, reconnectResult.RemovedEdge.InputSlotId);

                Assert.IsNotNull(reconnectResult.Edge);
                Assert.AreEqual(detailNodeResult.Node.ObjectId, reconnectResult.Edge!.OutputNodeId);
                Assert.AreEqual(detailOutput.SlotId, reconnectResult.Edge.OutputSlotId);
                Assert.AreEqual(sampleTextureNode.ObjectId, reconnectResult.Edge.InputNodeId);
                Assert.AreEqual(sampleTextureInput.SlotId, reconnectResult.Edge.InputSlotId);

                Assert.IsNotNull(reconnectResult.Structure);
                Assert.IsTrue(reconnectResult.Structure!.Edges!.Any(e =>
                    e.OutputNodeId == detailNodeResult.Node.ObjectId
                    && e.OutputSlotId == detailOutput.SlotId
                    && e.InputNodeId == sampleTextureNode.ObjectId
                    && e.InputSlotId == sampleTextureInput.SlotId));
                Assert.IsFalse(reconnectResult.Structure.Edges.Any(e =>
                    e.OutputNodeId == baseMapNode.ObjectId
                    && e.OutputSlotId == baseMapOutput.SlotId
                    && e.InputNodeId == sampleTextureNode.ObjectId
                    && e.InputSlotId == sampleTextureInput.SlotId),
                    "The previous _BaseMap connection into Sample Texture 2D.Texture should be removed during reconnect.");

                Assert.IsNotNull(reconnectResult.Graph);
                Assert.IsTrue(reconnectResult.Graph!.ShaderResolved, "Reconnecting a Texture2D edge should keep the Shader Graph import valid.");
                Assert.IsFalse(reconnectResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Reconnecting a Texture2D property into a Texture2D input should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_RerouteOutputSlot_MovesAllConsumersToNewOutput()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_RerouteOutputSlot.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "color",
                        DisplayName = "Accent",
                        OverrideReferenceName = "_AccentColor",
                        ColorHex = "#44CC88FF"
                    });

                var accentNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_AccentColor",
                        PositionX = -760f,
                        PositionY = 120f
                    });

                var addNodeResult = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "add",
                        PositionX = -120f,
                        PositionY = 420f
                    });

                var structureBeforeExtraEdges = tool.GetStructure(new AssetObjectRef(shader));
                var baseColorNode = structureBeforeExtraEdges.Nodes!
                    .First(n => n.Type == "UnityEditor.ShaderGraph.PropertyNode"
                        && n.PropertyReferenceName == "_BaseColor");
                var baseColorOutput = baseColorNode.Slots!.Single();
                var addNode = structureBeforeExtraEdges.Nodes
                    .First(n => n.ObjectId == addNodeResult.Node!.ObjectId);
                var addInputA = addNode.Slots!.First(s => s.DisplayName == "A");
                var addInputB = addNode.Slots!.First(s => s.DisplayName == "B");

                tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = baseColorNode.ObjectId,
                        OutputSlotObjectId = baseColorOutput.ObjectId,
                        InputNodeObjectId = addNode.ObjectId,
                        InputSlotObjectId = addInputA.ObjectId
                    });

                tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = baseColorNode.ObjectId,
                        OutputSlotObjectId = baseColorOutput.ObjectId,
                        InputNodeObjectId = addNode.ObjectId,
                        InputSlotObjectId = addInputB.ObjectId
                    });

                var structureBeforeReroute = tool.GetStructure(new AssetObjectRef(shader));
                var multiplyNode = structureBeforeReroute.Nodes!
                    .First(n => n.Name == "Multiply");
                var multiplyInputB = multiplyNode.Slots!
                    .First(s => s.DisplayName == "B");
                var accentOutput = accentNodeResult.Node!.Slots!.Single();
                var existingBaseColorConsumers = structureBeforeReroute.Edges!
                    .Where(e => e.OutputNodeId == baseColorNode.ObjectId
                        && e.OutputSlotId == baseColorOutput.SlotId)
                    .ToList();

                Assert.AreEqual(3, existingBaseColorConsumers.Count,
                    "Expected _BaseColor to feed Multiply.B plus the two added Add inputs before reroute.");

                var rerouteResult = tool.RerouteOutputSlot(
                    new AssetObjectRef(shader),
                    new ShaderGraphRerouteOutputSlotInput
                    {
                        ExistingOutputNodeObjectId = baseColorNode.ObjectId,
                        ExistingOutputSlotObjectId = baseColorOutput.ObjectId,
                        NewOutputNodeObjectId = accentNodeResult.Node.ObjectId,
                        NewOutputSlotObjectId = accentOutput.ObjectId
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(rerouteResult);
                Assert.IsTrue(rerouteResult.ChangedFields!.Contains("edge.disconnected"));
                Assert.IsTrue(rerouteResult.ChangedFields.Contains("edge.rerouted"));
                Assert.IsTrue(rerouteResult.ChangedFields.Contains("edge.connected"));

                Assert.IsNotNull(rerouteResult.RemovedEdges);
                Assert.AreEqual(3, rerouteResult.RemovedEdges!.Count);
                Assert.IsNotNull(rerouteResult.Edges);
                Assert.AreEqual(3, rerouteResult.Edges!.Count);

                Assert.IsNotNull(rerouteResult.Structure);
                Assert.IsFalse(rerouteResult.Structure!.Edges!.Any(e =>
                        e.OutputNodeId == baseColorNode.ObjectId
                        && e.OutputSlotId == baseColorOutput.SlotId),
                    "The old _BaseColor output should not feed any consumers after reroute.");

                Assert.IsTrue(rerouteResult.Structure.Edges.Any(e =>
                    e.OutputNodeId == accentNodeResult.Node.ObjectId
                    && e.OutputSlotId == accentOutput.SlotId
                    && e.InputNodeId == multiplyNode.ObjectId
                    && e.InputSlotId == multiplyInputB.SlotId));
                Assert.IsTrue(rerouteResult.Structure.Edges.Any(e =>
                    e.OutputNodeId == accentNodeResult.Node.ObjectId
                    && e.OutputSlotId == accentOutput.SlotId
                    && e.InputNodeId == addNode.ObjectId
                    && e.InputSlotId == addInputA.SlotId));
                Assert.IsTrue(rerouteResult.Structure.Edges.Any(e =>
                    e.OutputNodeId == accentNodeResult.Node.ObjectId
                    && e.OutputSlotId == accentOutput.SlotId
                    && e.InputNodeId == addNode.ObjectId
                    && e.InputSlotId == addInputB.SlotId));
                Assert.AreEqual(structureBeforeReroute.Edges.Count, rerouteResult.Structure.Edges.Count,
                    "Rerouting should preserve total edge count.");

                Assert.IsNotNull(rerouteResult.Graph);
                Assert.IsTrue(rerouteResult.Graph!.ShaderResolved, "Rerouting all consumers to a compatible output should keep the shader import valid.");
                Assert.IsFalse(rerouteResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Rerouting all consumers to a compatible output should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_RerouteOutputSlot_NoOutgoingEdges_Throws()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_RerouteOutputSlot_Empty.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "color",
                        DisplayName = "Accent",
                        OverrideReferenceName = "_AccentColor",
                        ColorHex = "#44CC88FF"
                    });

                var accentNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_AccentColor",
                        PositionX = -760f,
                        PositionY = 120f
                    });

                var structure = tool.GetStructure(new AssetObjectRef(shader));
                var baseColorNode = structure.Nodes!
                    .First(n => n.Type == "UnityEditor.ShaderGraph.PropertyNode"
                        && n.PropertyReferenceName == "_BaseColor");
                var baseColorOutput = baseColorNode.Slots!.Single();
                var accentOutput = accentNodeResult.Node!.Slots!.Single();

                Assert.Throws<InvalidOperationException>(() => tool.RerouteOutputSlot(
                    new AssetObjectRef(shader),
                    new ShaderGraphRerouteOutputSlotInput
                    {
                        ExistingOutputNodeObjectId = accentNodeResult.Node.ObjectId,
                        ExistingOutputSlotObjectId = accentOutput.ObjectId,
                        NewOutputNodeObjectId = baseColorNode.ObjectId,
                        NewOutputSlotObjectId = baseColorOutput.ObjectId
                    }));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ConnectEdge_ReplaceExistingInputConnection_ReroutesMultiplyColorInput()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateEdge_Replace.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "color",
                        DisplayName = "Accent",
                        OverrideReferenceName = "_AccentColor",
                        ColorHex = "#44CC88FF"
                    });

                var accentNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_AccentColor",
                        PositionX = -720f,
                        PositionY = 120f
                    });

                var structureBeforeReroute = tool.GetStructure(new AssetObjectRef(shader));
                var multiplyNode = structureBeforeReroute.Nodes!
                    .First(n => n.Name == "Multiply");
                var multiplyInputB = multiplyNode.Slots!
                    .First(s => s.DisplayName == "B");
                var baseColorNode = structureBeforeReroute.Nodes
                    .First(n => n.Type == "UnityEditor.ShaderGraph.PropertyNode"
                        && n.PropertyReferenceName == "_BaseColor");
                var baseColorOutput = baseColorNode.Slots!.Single();
                var accentOutput = accentNodeResult.Node!.Slots!.Single();

                var replaceResult = tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = accentNodeResult.Node.ObjectId,
                        OutputSlotObjectId = accentOutput.ObjectId,
                        InputNodeObjectId = multiplyNode.ObjectId,
                        InputSlotObjectId = multiplyInputB.ObjectId,
                        ReplaceExistingInputConnection = true
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(replaceResult);
                Assert.IsTrue(replaceResult.ChangedFields!.Contains("edge.disconnected"));
                Assert.IsTrue(replaceResult.ChangedFields.Contains("edge.connected"));
                Assert.IsTrue(replaceResult.ChangedFields.Contains("edge.replaced"));

                Assert.IsNotNull(replaceResult.RemovedEdge);
                Assert.AreEqual(baseColorNode.ObjectId, replaceResult.RemovedEdge!.OutputNodeId);
                Assert.AreEqual(baseColorOutput.SlotId, replaceResult.RemovedEdge.OutputSlotId);
                Assert.AreEqual(multiplyNode.ObjectId, replaceResult.RemovedEdge.InputNodeId);
                Assert.AreEqual(multiplyInputB.SlotId, replaceResult.RemovedEdge.InputSlotId);

                Assert.IsNotNull(replaceResult.Edge);
                Assert.AreEqual(accentNodeResult.Node.ObjectId, replaceResult.Edge!.OutputNodeId);
                Assert.AreEqual(accentOutput.SlotId, replaceResult.Edge.OutputSlotId);
                Assert.AreEqual(multiplyNode.ObjectId, replaceResult.Edge.InputNodeId);
                Assert.AreEqual(multiplyInputB.SlotId, replaceResult.Edge.InputSlotId);

                Assert.IsNotNull(replaceResult.Structure);
                Assert.IsTrue(replaceResult.Structure!.Edges!.Any(e =>
                    e.OutputNodeId == accentNodeResult.Node.ObjectId
                    && e.OutputSlotId == accentOutput.SlotId
                    && e.InputNodeId == multiplyNode.ObjectId
                    && e.InputSlotId == multiplyInputB.SlotId));
                Assert.IsFalse(replaceResult.Structure.Edges.Any(e =>
                    e.OutputNodeId == baseColorNode.ObjectId
                    && e.OutputSlotId == baseColorOutput.SlotId
                    && e.InputNodeId == multiplyNode.ObjectId
                    && e.InputSlotId == multiplyInputB.SlotId),
                    "The previous _BaseColor connection into Multiply.B should be removed during replacement.");

                Assert.IsNotNull(replaceResult.Graph);
                Assert.IsTrue(replaceResult.Graph!.ShaderResolved, "Replacing a compatible incoming edge should keep the shader import valid.");
                Assert.IsFalse(replaceResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Replacing a compatible incoming edge should not introduce import errors.");
                Assert.IsTrue(replaceResult.Graph.Properties!.Any(p => p.Name == "_AccentColor"),
                    "Compiled shader properties should still include the added Accent color property.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ConnectEdge_ReplaceExistingInputConnection_ReroutesSampleTextureInput()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateEdge_ReplaceTexture.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "texture2D",
                        DisplayName = "Detail Map",
                        OverrideReferenceName = "_DetailMap",
                        TextureDefaultType = "black"
                    });

                var detailNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_DetailMap",
                        PositionX = -760f,
                        PositionY = 20f
                    });

                var structureBeforeReplace = tool.GetStructure(new AssetObjectRef(shader));
                var sampleTextureNode = structureBeforeReplace.Nodes!
                    .First(n => n.Name == "Sample Texture 2D");
                var sampleTextureInput = sampleTextureNode.Slots!
                    .First(s => s.DisplayName == "Texture");
                var baseMapNode = structureBeforeReplace.Nodes
                    .First(n => n.Type == "UnityEditor.ShaderGraph.PropertyNode"
                        && n.PropertyReferenceName == "_BaseMap");
                var baseMapOutput = baseMapNode.Slots!.Single();
                var detailOutput = detailNodeResult.Node!.Slots!.Single();

                var replaceResult = tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = detailNodeResult.Node.ObjectId,
                        OutputSlotObjectId = detailOutput.ObjectId,
                        InputNodeObjectId = sampleTextureNode.ObjectId,
                        InputSlotObjectId = sampleTextureInput.ObjectId,
                        ReplaceExistingInputConnection = true
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(replaceResult);
                Assert.IsTrue(replaceResult.ChangedFields!.Contains("edge.disconnected"));
                Assert.IsTrue(replaceResult.ChangedFields.Contains("edge.connected"));
                Assert.IsTrue(replaceResult.ChangedFields.Contains("edge.replaced"));

                Assert.IsNotNull(replaceResult.RemovedEdge);
                Assert.AreEqual(baseMapNode.ObjectId, replaceResult.RemovedEdge!.OutputNodeId);
                Assert.AreEqual(baseMapOutput.SlotId, replaceResult.RemovedEdge.OutputSlotId);
                Assert.AreEqual(sampleTextureNode.ObjectId, replaceResult.RemovedEdge.InputNodeId);
                Assert.AreEqual(sampleTextureInput.SlotId, replaceResult.RemovedEdge.InputSlotId);

                Assert.IsNotNull(replaceResult.Edge);
                Assert.AreEqual(detailNodeResult.Node.ObjectId, replaceResult.Edge!.OutputNodeId);
                Assert.AreEqual(detailOutput.SlotId, replaceResult.Edge.OutputSlotId);
                Assert.AreEqual(sampleTextureNode.ObjectId, replaceResult.Edge.InputNodeId);
                Assert.AreEqual(sampleTextureInput.SlotId, replaceResult.Edge.InputSlotId);

                Assert.IsNotNull(replaceResult.Structure);
                Assert.IsTrue(replaceResult.Structure!.Edges!.Any(e =>
                    e.OutputNodeId == detailNodeResult.Node.ObjectId
                    && e.OutputSlotId == detailOutput.SlotId
                    && e.InputNodeId == sampleTextureNode.ObjectId
                    && e.InputSlotId == sampleTextureInput.SlotId));
                Assert.IsFalse(replaceResult.Structure.Edges.Any(e =>
                    e.OutputNodeId == baseMapNode.ObjectId
                    && e.OutputSlotId == baseMapOutput.SlotId
                    && e.InputNodeId == sampleTextureNode.ObjectId
                    && e.InputSlotId == sampleTextureInput.SlotId),
                    "The previous _BaseMap connection into Sample Texture 2D.Texture should be removed during replacement.");

                Assert.IsNotNull(replaceResult.Graph);
                Assert.IsTrue(replaceResult.Graph!.ShaderResolved, "Replacing a Texture2D edge should keep the Shader Graph import valid.");
                Assert.IsFalse(replaceResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Replacing a Texture2D property into a Texture2D input should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ConnectEdge_ConnectedInputWithoutReplace_Throws()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateEdge_ConnectedInput.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "color",
                        DisplayName = "Accent",
                        OverrideReferenceName = "_AccentColor",
                        ColorHex = "#44CC88FF"
                    });

                var accentNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_AccentColor",
                        PositionX = -720f,
                        PositionY = 120f
                    });

                var structure = tool.GetStructure(new AssetObjectRef(shader));
                var multiplyNode = structure.Nodes!
                    .First(n => n.Name == "Multiply");
                var multiplyInputB = multiplyNode.Slots!
                    .First(s => s.DisplayName == "B");
                var accentOutput = accentNodeResult.Node!.Slots!.Single();

                Assert.Throws<InvalidOperationException>(() => tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = accentNodeResult.Node.ObjectId,
                        OutputSlotObjectId = accentOutput.ObjectId,
                        InputNodeObjectId = multiplyNode.ObjectId,
                        InputSlotObjectId = multiplyInputB.ObjectId
                    }));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ConnectEdge_AllowsVector2SlotsToFeedUvSlots()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateEdge_Vector2Uv.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "vector2",
                        DisplayName = "UV Seed",
                        OverrideReferenceName = "_UvSeed",
                        VectorX = 0.125f,
                        VectorY = 0.875f
                    });

                var uvSeedNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_UvSeed",
                        PositionX = -900f,
                        PositionY = 40f
                    });
                var tilingNodeResult = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "tilingAndOffset",
                        PositionX = -640f,
                        PositionY = 40f
                    });

                var structureBeforeConnect = tool.GetStructure(new AssetObjectRef(shader));
                var tilingNode = structureBeforeConnect.Nodes!
                    .First(n => n.ObjectId == tilingNodeResult.Node!.ObjectId);
                var tilingUvInput = tilingNode.Slots!
                    .First(s => s.DisplayName == "UV");
                var tilingOut = tilingNode.Slots
                    .First(s => s.DisplayName == "Out");
                var sampleTextureNode = structureBeforeConnect.Nodes
                    .First(n => n.Name == "Sample Texture 2D");
                var sampleUvInput = sampleTextureNode.Slots!
                    .First(s => s.DisplayName == "UV");
                var uvSeedOutput = uvSeedNodeResult.Node!.Slots!.Single();

                var connectSeedToUv = tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = uvSeedNodeResult.Node.ObjectId,
                        OutputSlotObjectId = uvSeedOutput.ObjectId,
                        InputNodeObjectId = tilingNode.ObjectId,
                        InputSlotObjectId = tilingUvInput.ObjectId
                    },
                    includeMessages: true,
                    includeProperties: true);

                var connectUvToSample = tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = tilingNode.ObjectId,
                        OutputSlotObjectId = tilingOut.ObjectId,
                        InputNodeObjectId = sampleTextureNode.ObjectId,
                        InputSlotObjectId = sampleUvInput.ObjectId
                    },
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(connectSeedToUv);
                Assert.IsTrue(connectSeedToUv.ChangedFields!.Contains("edge.connected"));
                Assert.IsNotNull(connectUvToSample);
                Assert.IsTrue(connectUvToSample.ChangedFields!.Contains("edge.connected"));

                Assert.IsNotNull(connectUvToSample.Structure);
                Assert.IsTrue(connectUvToSample.Structure!.Edges!.Any(e =>
                    e.OutputNodeId == uvSeedNodeResult.Node.ObjectId
                    && e.OutputSlotId == uvSeedOutput.SlotId
                    && e.InputNodeId == tilingNode.ObjectId
                    && e.InputSlotId == tilingUvInput.SlotId));
                Assert.IsTrue(connectUvToSample.Structure.Edges.Any(e =>
                    e.OutputNodeId == tilingNode.ObjectId
                    && e.OutputSlotId == tilingOut.SlotId
                    && e.InputNodeId == sampleTextureNode.ObjectId
                    && e.InputSlotId == sampleUvInput.SlotId));

                Assert.IsNotNull(connectUvToSample.Graph);
                Assert.IsTrue(connectUvToSample.Graph!.ShaderResolved, "UV/vector2-compatible edge connections should keep the Shader Graph import valid.");
                Assert.IsFalse(connectUvToSample.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Connecting vector2-compatible outputs into UV inputs should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ConnectEdge_IncompatibleSlots_Throws()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateEdge_Incompatible.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var structure = tool.GetStructure(new AssetObjectRef(shader));
                var baseMapNode = structure.Nodes!
                    .First(n => n.Type == "UnityEditor.ShaderGraph.PropertyNode"
                        && n.PropertyReferenceName == "_BaseMap");
                var baseMapOutput = baseMapNode.Slots!.Single();
                var multiplyNode = structure.Nodes
                    .First(n => n.Name == "Multiply");
                var multiplyInputB = multiplyNode.Slots!
                    .First(s => s.DisplayName == "B");

                Assert.Throws<InvalidOperationException>(() => tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = baseMapNode.ObjectId,
                        OutputSlotObjectId = baseMapOutput.ObjectId,
                        InputNodeObjectId = multiplyNode.ObjectId,
                        InputSlotObjectId = multiplyInputB.ObjectId
                    }));
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_Create_ClonesTemplateAndImportsShader()
        {
            var assetPath = $"{TestFolder}/Validation_Create.shadergraph";
            try
            {
                var result = new Tool_Assets_ShaderGraph().Create(
                    assetPath: assetPath,
                    templateAssetPath: TemplateAssetPath,
                    overwrite: true);

                Assert.IsNotNull(result);
                Assert.AreEqual(assetPath, result.AssetPath);
                Assert.IsTrue(result.ShaderResolved, "Created Shader Graph should resolve to a Shader asset.");
                Assert.IsTrue(result.SourceParsed, "Created Shader Graph source should parse successfully.");
                Assert.IsFalse(string.IsNullOrEmpty(result.ShaderName), "Created Shader Graph should expose a shader name.");

                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");
                Assert.AreEqual(shader!.name, result.ShaderName, "Returned shader name should match the imported asset.");
                Assert.IsFalse(result.Diagnostics!.Any(d => d.Severity == "Error"),
                    "A created template-derived graph should not report error diagnostics.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_CreateMaterial_CreatesMaterialFromGraphShader()
        {
            var graphAssetPath = CreateShaderGraphAssetCopy("Validation_CreateMaterial.shadergraph");
            var materialAssetPath = $"{TestFolder}/Validation_CreateMaterial.mat";
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(graphAssetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{graphAssetPath}'.");

                var result = new Tool_Assets_ShaderGraph().CreateMaterial(
                    assetRef: new AssetObjectRef(shader!),
                    materialAssetPath: materialAssetPath,
                    overwrite: true);

                Assert.IsNotNull(result);
                Assert.AreEqual(materialAssetPath, result.AssetPath);

                var material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
                Assert.IsNotNull(material, $"Expected Material asset to resolve at '{materialAssetPath}'.");
                Assert.AreEqual(shader!.name, material!.shader.name,
                    "Created material should use the Shader Graph's imported shader.");
            }
            finally
            {
                CleanupTestAsset(materialAssetPath);
                CleanupTestAsset(graphAssetPath);
            }
        }

        [Test]
        public void ShaderGraph_CreateFromStyleRecipe_CreatesAssetsAndAppliesBaseColor()
        {
            const string recipeJson = @"{
                ""styleName"": ""Recipe Validation"",
                ""renderPipeline"": ""URP"",
                ""graphTemplate"": ""toon-unlit"",
                ""palette"": {
                    ""baseColors"": [""#80A0C0""]
                }
            }";

            var graphAssetPath = $"{TestFolder}/Validation_Recipe.shadergraph";
            var materialAssetPath = $"{TestFolder}/Validation_Recipe.mat";
            try
            {
                var result = new Tool_Assets_ShaderGraph().CreateFromStyleRecipe(
                    styleRecipe: recipeJson,
                    graphAssetPath: graphAssetPath,
                    materialAssetPath: materialAssetPath,
                    overwrite: true);

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.Graph);
                Assert.AreEqual(graphAssetPath, result.Graph!.AssetPath);
                Assert.AreEqual(materialAssetPath, result.MaterialAssetPath);
                Assert.AreEqual("unlit-simple", result.ResolvedTemplateId);
                Assert.IsTrue(result.Warnings!.Any(w => w.Contains("falls back to the safe 'unlit-simple' template")),
                    "Expected a warning explaining the temporary template fallback.");

                var material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
                Assert.IsNotNull(material, $"Expected Material asset to resolve at '{materialAssetPath}'.");
                Assert.IsTrue(material!.HasColor("_BaseColor"), "Generated material should expose _BaseColor.");

                Assert.IsTrue(ColorUtility.TryParseHtmlString("#80A0C0", out var expectedColor));
                var actualColor = material.GetColor("_BaseColor");
                Assert.That(actualColor.r, Is.EqualTo(expectedColor.r).Within(0.0001f));
                Assert.That(actualColor.g, Is.EqualTo(expectedColor.g).Within(0.0001f));
                Assert.That(actualColor.b, Is.EqualTo(expectedColor.b).Within(0.0001f));
            }
            finally
            {
                CleanupTestAsset(materialAssetPath);
                CleanupTestAsset(graphAssetPath);
            }
        }

        [Test]
        public void ShaderGraph_CreateFromStyleRecipe_AppliesReferenceTexture()
        {
            var textureAssetPath = CreateTextureAsset("Validation_Recipe_Texture.png", new Color(0.15f, 0.7f, 0.35f, 1f));
            var graphAssetPath = $"{TestFolder}/Validation_Recipe_Texture.shadergraph";
            var materialAssetPath = $"{TestFolder}/Validation_Recipe_Texture.mat";
            try
            {
                var recipeJson = $@"{{
                    ""styleName"": ""Recipe Texture Validation"",
                    ""renderPipeline"": ""URP"",
                    ""graphTemplate"": ""unlit-simple"",
                    ""texture"": {{
                        ""useReferenceTexture"": true,
                        ""referenceTextureAssetPath"": ""{textureAssetPath}""
                    }}
                }}";

                var result = new Tool_Assets_ShaderGraph().CreateFromStyleRecipe(
                    styleRecipe: recipeJson,
                    graphAssetPath: graphAssetPath,
                    materialAssetPath: materialAssetPath,
                    overwrite: true);

                Assert.IsNotNull(result);
                Assert.IsNotNull(result.AppliedMaterialProperties);
                Assert.Contains("_BaseMap", result.AppliedMaterialProperties!,
                    "The generated material should report that the reference texture was applied to _BaseMap.");
                Assert.IsFalse(result.Warnings?.Any(w => w.Contains("texture.", StringComparison.OrdinalIgnoreCase)) ?? false,
                    "Reference texture assignment should not be reported as deferred when only the texture asset path is used.");

                var material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAssetPath);
                Assert.IsNotNull(material, $"Expected Material asset to resolve at '{materialAssetPath}'.");
                Assert.IsNotNull(texture, $"Expected Texture2D asset to resolve at '{textureAssetPath}'.");
                Assert.IsTrue(material!.HasTexture("_BaseMap"), "Generated material should expose _BaseMap.");
                Assert.AreEqual(texture, material.GetTexture("_BaseMap"),
                    "Generated material should reference the texture asset provided by the style recipe.");
            }
            finally
            {
                CleanupTestAsset(materialAssetPath);
                CleanupTestAsset(graphAssetPath);
                CleanupTestAsset(textureAssetPath);
            }
        }

        [Test]
        public void ShaderGraph_CreateFromStyleRecipe_InvalidColor_Throws()
        {
            const string recipeJson = @"{
                ""palette"": {
                    ""baseColors"": [""not-a-color""]
                }
            }";

            Assert.Throws<ArgumentException>(() => new Tool_Assets_ShaderGraph().CreateFromStyleRecipe(
                styleRecipe: recipeJson,
                graphAssetPath: $"{TestFolder}/InvalidColor.shadergraph",
                materialAssetPath: $"{TestFolder}/InvalidColor.mat",
                overwrite: true));
        }

        [Test]
        public void ShaderGraph_CreateFromStyleRecipe_UnsupportedRenderPipeline_Throws()
        {
            const string recipeJson = @"{
                ""renderPipeline"": ""HDRP"",
                ""palette"": {
                    ""baseColors"": [""#FFFFFF""]
                }
            }";

            Assert.Throws<ArgumentException>(() => new Tool_Assets_ShaderGraph().CreateFromStyleRecipe(
                styleRecipe: recipeJson,
                graphAssetPath: $"{TestFolder}/UnsupportedPipeline.shadergraph",
                materialAssetPath: $"{TestFolder}/UnsupportedPipeline.mat",
                overwrite: true));
        }

        [Test]
        public void ShaderGraph_CreateFromStyleRecipe_UnknownTemplate_Throws()
        {
            const string recipeJson = @"{
                ""graphTemplate"": ""unknown-template"",
                ""palette"": {
                    ""baseColors"": [""#FFFFFF""]
                }
            }";

            Assert.Throws<ArgumentException>(() => new Tool_Assets_ShaderGraph().CreateFromStyleRecipe(
                styleRecipe: recipeJson,
                graphAssetPath: $"{TestFolder}/UnknownTemplate.shadergraph",
                materialAssetPath: $"{TestFolder}/UnknownTemplate.mat",
                overwrite: true));
        }

        static string CreateShaderGraphAssetCopy(string fileName)
        {
            var destinationPath = $"{TestFolder}/{fileName}";
            EnsureFolder(TestFolder);

            var packageInfo = PackageInfo.FindForAssetPath(TemplateAssetPath);
            Assert.IsNotNull(packageInfo, $"Expected package info for '{TemplateAssetPath}'.");

            var packageRoot = $"Packages/{packageInfo!.name}";
            var relativeTemplatePath = TemplateAssetPath.Substring(packageRoot.Length).TrimStart('/');
            var sourcePath = Path.Combine(packageInfo.resolvedPath, relativeTemplatePath);
            Assert.IsTrue(File.Exists(sourcePath), $"Expected template source to exist at '{sourcePath}'.");

            File.Copy(sourcePath, destinationPath, overwrite: true);
            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            return destinationPath;
        }

        static string CreateTextureAsset(string fileName, Color color)
        {
            var destinationPath = $"{TestFolder}/{fileName}";
            EnsureFolder(TestFolder);

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, mipChain: false);
            try
            {
                var pixels = Enumerable.Repeat(color, 4).ToArray();
                texture.SetPixels(pixels);
                texture.Apply(updateMipmaps: false, makeNoLongerReadable: false);
                File.WriteAllBytes(destinationPath, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            return destinationPath;
        }

        static void EnsureFolder(string folderPath)
        {
            var directoryPath = Path.GetDirectoryName(folderPath);
            if (!string.IsNullOrEmpty(directoryPath) && !Directory.Exists(directoryPath))
                Directory.CreateDirectory(directoryPath);

            if (!Directory.Exists(folderPath))
                Directory.CreateDirectory(folderPath);

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static void CleanupTestAsset(string assetPath)
        {
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                AssetDatabase.DeleteAsset(assetPath);

            var physicalFolderPath = Path.GetFullPath(TestFolder);
            if (Directory.Exists(physicalFolderPath) &&
                !Directory.EnumerateFileSystemEntries(physicalFolderPath).Any())
            {
                AssetDatabase.DeleteAsset(TestFolder);
            }

            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static ShaderGraphSlotDefinitionData FindSlot(ShaderGraphNodeDefinitionData node, string displayName)
        {
            Assert.IsNotNull(node.Slots, $"Expected resolved slots for node '{node.Name ?? node.ObjectId}'.");

            var slot = node.Slots!.FirstOrDefault(s => string.Equals(s.DisplayName, displayName, StringComparison.Ordinal));
            Assert.IsNotNull(slot, $"Expected slot '{displayName}' on node '{node.Name ?? node.ObjectId}'.");
            return slot!;
        }

        static void AssertSlotFloat(ShaderGraphNodeDefinitionData node, string displayName, float expectedValue)
        {
            var slot = FindSlot(node, displayName);
            Assert.AreEqual(expectedValue, ParseScalarValue(slot.ValueJson), 0.0001f, $"Unexpected ValueJson for slot '{displayName}'.");
            Assert.AreEqual(expectedValue, ParseScalarValue(slot.DefaultValueJson), 0.0001f, $"Unexpected DefaultValueJson for slot '{displayName}'.");
        }

        static void AssertSlotBool(ShaderGraphNodeDefinitionData node, string displayName, bool expectedValue)
        {
            var slot = FindSlot(node, displayName);
            Assert.AreEqual(expectedValue, ParseBoolValue(slot.ValueJson), $"Unexpected ValueJson for slot '{displayName}'.");
            Assert.AreEqual(expectedValue, ParseBoolValue(slot.DefaultValueJson), $"Unexpected DefaultValueJson for slot '{displayName}'.");
        }

        static void AssertSlotVector2(ShaderGraphNodeDefinitionData node, string displayName, float x, float y)
        {
            var slot = FindSlot(node, displayName);
            AssertVectorComponent(slot.ValueJson, "x", x, displayName, "ValueJson");
            AssertVectorComponent(slot.ValueJson, "y", y, displayName, "ValueJson");
            AssertVectorComponent(slot.DefaultValueJson, "x", x, displayName, "DefaultValueJson");
            AssertVectorComponent(slot.DefaultValueJson, "y", y, displayName, "DefaultValueJson");
        }

        static void AssertSlotVector4(ShaderGraphNodeDefinitionData node, string displayName, float x, float y, float z, float w)
        {
            var slot = FindSlot(node, displayName);
            AssertVectorComponent(slot.ValueJson, "x", x, displayName, "ValueJson");
            AssertVectorComponent(slot.ValueJson, "y", y, displayName, "ValueJson");
            AssertVectorComponent(slot.ValueJson, "z", z, displayName, "ValueJson");
            AssertVectorComponent(slot.ValueJson, "w", w, displayName, "ValueJson");
            AssertVectorComponent(slot.DefaultValueJson, "x", x, displayName, "DefaultValueJson");
            AssertVectorComponent(slot.DefaultValueJson, "y", y, displayName, "DefaultValueJson");
            AssertVectorComponent(slot.DefaultValueJson, "z", z, displayName, "DefaultValueJson");
            AssertVectorComponent(slot.DefaultValueJson, "w", w, displayName, "DefaultValueJson");
        }

        static float ParseScalarValue(string? json)
        {
            Assert.IsNotNull(json, "Expected slot scalar JSON to be present.");
            using var document = JsonDocument.Parse(json!);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.Number when document.RootElement.TryGetSingle(out var singleValue) => singleValue,
                JsonValueKind.Number when document.RootElement.TryGetDouble(out var doubleValue) => (float)doubleValue,
                _ => throw new AssertionException($"Expected scalar numeric JSON but received '{json}'.")
            };
        }

        static bool ParseBoolValue(string? json)
        {
            Assert.IsNotNull(json, "Expected slot bool JSON to be present.");
            using var document = JsonDocument.Parse(json!);
            return document.RootElement.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => throw new AssertionException($"Expected boolean JSON but received '{json}'.")
            };
        }

        static void AssertVectorComponent(string? json, string componentName, float expectedValue, string slotDisplayName, string jsonKind)
        {
            Assert.IsNotNull(json, $"Expected {jsonKind} to be present for slot '{slotDisplayName}'.");
            using var document = JsonDocument.Parse(json!);
            Assert.IsTrue(document.RootElement.TryGetProperty(componentName, out var property),
                $"Expected component '{componentName}' in {jsonKind} for slot '{slotDisplayName}'.");

            var actualValue = property.TryGetSingle(out var singleValue)
                ? singleValue
                : (property.TryGetDouble(out var doubleValue)
                    ? (float)doubleValue
                    : throw new AssertionException($"Expected numeric component '{componentName}' in {jsonKind} for slot '{slotDisplayName}'."));

            Assert.AreEqual(expectedValue, actualValue, 0.0001f, $"Unexpected component '{componentName}' in {jsonKind} for slot '{slotDisplayName}'.");
        }

        sealed class SampleTexture2DWindowState
        {
            public string TextureType { get; set; } = string.Empty;
            public string NormalMapSpace { get; set; } = string.Empty;
            public bool UseGlobalMipBias { get; set; }
            public string MipSamplingMode { get; set; } = string.Empty;
        }

        static void OpenShaderGraphWindow(string assetPath)
        {
            var shaderGraphAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .First(assembly => assembly.GetName().Name == "Unity.ShaderGraph.Editor");
            var importerEditorType = shaderGraphAssembly.GetType("UnityEditor.ShaderGraph.ShaderGraphImporterEditor", throwOnError: true)!;
            var showGraphEditWindowMethod = importerEditorType.GetMethod(
                "ShowGraphEditWindow",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(string) },
                modifiers: null);

            Assert.IsNotNull(showGraphEditWindowMethod, "Expected ShaderGraphImporterEditor.ShowGraphEditWindow(string) to be available.");

            var opened = showGraphEditWindowMethod!.Invoke(null, new object[] { assetPath });
            Assert.AreEqual(true, opened, $"Expected Shader Graph window to open for '{assetPath}'.");
        }

        static void CloseShaderGraphWindows(string assetPath)
        {
            foreach (var window in FindOpenShaderGraphWindows(assetPath))
                window.Close();
        }

        static SampleTexture2DWindowState? ReadOpenSampleTexture2DWindowState(string assetPath)
        {
            var window = FindOpenShaderGraphWindows(assetPath).FirstOrDefault();
            if (window == null)
                return null;

            var shaderGraphAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .First(assembly => assembly.GetName().Name == "Unity.ShaderGraph.Editor");
            var windowType = window.GetType();
            var graphObjectProperty = windowType.GetProperty("graphObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(graphObjectProperty, "Expected MaterialGraphEditWindow.graphObject to be available.");

            var graphObject = graphObjectProperty!.GetValue(window);
            Assert.IsNotNull(graphObject, "Expected an initialized GraphObject for the open Shader Graph window.");

            var graphObjectType = shaderGraphAssembly.GetType("UnityEditor.Graphing.GraphObject", throwOnError: true)!;
            var graphProperty = graphObjectType.GetProperty("graph", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(graphProperty, "Expected GraphObject.graph to be available.");

            var graph = graphProperty!.GetValue(graphObject);
            Assert.IsNotNull(graph, "Expected GraphObject.graph to be available for the open Shader Graph window.");

            var sampleTextureNodeType = shaderGraphAssembly.GetType("UnityEditor.ShaderGraph.SampleTexture2DNode", throwOnError: true)!;
            var getNodesMethod = graph!.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(method => method.Name == "GetNodes"
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 0)
                .MakeGenericMethod(sampleTextureNodeType);

            var sampleTextureNode = ((System.Collections.IEnumerable)getNodesMethod.Invoke(graph, null)!)
                .Cast<object>()
                .FirstOrDefault();
            Assert.IsNotNull(sampleTextureNode, "Expected the validation graph window to contain a Sample Texture 2D node.");

            var textureTypeProperty = sampleTextureNodeType.GetProperty("textureType", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var normalMapSpaceProperty = sampleTextureNodeType.GetProperty("normalMapSpace", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var useGlobalMipBiasProperty = sampleTextureNodeType.GetProperty("enableGlobalMipBias", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            var mipSamplingModeProperty = sampleTextureNodeType.GetProperty("mipSamplingMode", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            Assert.IsNotNull(textureTypeProperty);
            Assert.IsNotNull(normalMapSpaceProperty);
            Assert.IsNotNull(useGlobalMipBiasProperty);
            Assert.IsNotNull(mipSamplingModeProperty);

            return new SampleTexture2DWindowState
            {
                TextureType = textureTypeProperty!.GetValue(sampleTextureNode)!.ToString()!,
                NormalMapSpace = normalMapSpaceProperty!.GetValue(sampleTextureNode)!.ToString()!,
                UseGlobalMipBias = (bool)useGlobalMipBiasProperty!.GetValue(sampleTextureNode)!,
                MipSamplingMode = mipSamplingModeProperty!.GetValue(sampleTextureNode)!.ToString()!
            };
        }

        static object ResolveOpenShaderGraphInputSlot(string assetPath, string nodeTypeName, string slotDisplayName)
        {
            var window = FindOpenShaderGraphWindows(assetPath).FirstOrDefault();
            Assert.IsNotNull(window, $"Expected a Shader Graph editor window to be open for '{assetPath}'.");

            var shaderGraphAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .First(assembly => assembly.GetName().Name == "Unity.ShaderGraph.Editor");
            var graphObjectType = shaderGraphAssembly.GetType("UnityEditor.Graphing.GraphObject", throwOnError: true)!;
            var abstractNodeType = shaderGraphAssembly.GetType("UnityEditor.ShaderGraph.AbstractMaterialNode", throwOnError: true)!;
            var materialSlotType = shaderGraphAssembly.GetType("UnityEditor.ShaderGraph.MaterialSlot", throwOnError: true)!;

            var graphObjectProperty = window!.GetType().GetProperty("graphObject", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(graphObjectProperty, "Expected MaterialGraphEditWindow.graphObject to be available.");

            var graphObject = graphObjectProperty!.GetValue(window);
            Assert.IsNotNull(graphObject, "Expected an initialized GraphObject for the open Shader Graph window.");

            var graphProperty = graphObjectType.GetProperty("graph", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(graphProperty, "Expected GraphObject.graph to be available.");

            var graph = graphProperty!.GetValue(graphObject);
            Assert.IsNotNull(graph, "Expected GraphObject.graph to be available for the open Shader Graph window.");

            var getNodesMethod = graph!.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(method => method.Name == "GetNodes"
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 0)
                .MakeGenericMethod(abstractNodeType);

            var node = ((System.Collections.IEnumerable)getNodesMethod.Invoke(graph, null)!)
                .Cast<object>()
                .FirstOrDefault(candidate => string.Equals(candidate.GetType().Name, nodeTypeName, StringComparison.Ordinal));
            Assert.IsNotNull(node, $"Expected the open Shader Graph window to contain a '{nodeTypeName}' node.");

            var slotListType = typeof(List<>).MakeGenericType(materialSlotType);
            var slotList = Activator.CreateInstance(slotListType);
            Assert.IsNotNull(slotList, "Expected a temporary MaterialSlot list instance.");

            var getInputSlotsMethod = node!.GetType()
                .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .First(method => method.Name == "GetInputSlots"
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 1)
                .MakeGenericMethod(materialSlotType);

            getInputSlotsMethod.Invoke(node, new[] { slotList });

            var slot = ((System.Collections.IEnumerable)slotList!).Cast<object>()
                .FirstOrDefault(candidate =>
                {
                    var displayNameField = candidate.GetType().GetField("m_DisplayName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    var fieldValue = displayNameField?.GetValue(candidate) as string;
                    if (string.IsNullOrEmpty(fieldValue))
                    {
                        var displayNameProperty = candidate.GetType().GetProperty("displayName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                        fieldValue = displayNameProperty?.GetValue(candidate) as string;
                    }

                    if (!string.IsNullOrEmpty(fieldValue))
                    {
                        var suffixIndex = fieldValue.IndexOf('(');
                        if (suffixIndex > 0)
                            fieldValue = fieldValue[..suffixIndex];
                    }

                    return string.Equals(fieldValue, slotDisplayName, StringComparison.Ordinal);
                });

            Assert.IsNotNull(slot, $"Expected node '{nodeTypeName}' to expose an input slot named '{slotDisplayName}'.");
            return slot!;
        }

        static T ReadPrivateField<T>(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Expected field '{target.GetType().FullName}.{fieldName}' to be available.");

            var value = field!.GetValue(target);
            Assert.IsNotNull(value, $"Expected field '{target.GetType().FullName}.{fieldName}' to have a value.");
            return (T)value!;
        }

        static void AssertPrivateVector2Field(object target, string fieldName, float x, float y, string label)
        {
            var value = ReadPrivateField<Vector2>(target, fieldName);
            Assert.AreEqual(x, value.x, 0.0001f, $"Unexpected X component for {label}.");
            Assert.AreEqual(y, value.y, 0.0001f, $"Unexpected Y component for {label}.");
        }

        static void AssertPrivateVector4Field(object target, string fieldName, float x, float y, float z, float w, string label)
        {
            var value = ReadPrivateField<Vector4>(target, fieldName);
            Assert.AreEqual(x, value.x, 0.0001f, $"Unexpected X component for {label}.");
            Assert.AreEqual(y, value.y, 0.0001f, $"Unexpected Y component for {label}.");
            Assert.AreEqual(z, value.z, 0.0001f, $"Unexpected Z component for {label}.");
            Assert.AreEqual(w, value.w, 0.0001f, $"Unexpected W component for {label}.");
        }

        static EditorWindow[] FindOpenShaderGraphWindows(string assetPath)
        {
            var shaderGraphAssembly = AppDomain.CurrentDomain.GetAssemblies()
                .First(assembly => assembly.GetName().Name == "Unity.ShaderGraph.Editor");
            var windowType = shaderGraphAssembly.GetType("UnityEditor.ShaderGraph.Drawing.MaterialGraphEditWindow", throwOnError: true)!;
            var selectedGuidProperty = windowType.GetProperty("selectedGuid", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotNull(selectedGuidProperty, "Expected MaterialGraphEditWindow.selectedGuid to be available.");

            var findObjectsMethod = typeof(Resources)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Single(method => method.Name == "FindObjectsOfTypeAll"
                    && method.IsGenericMethodDefinition
                    && method.GetParameters().Length == 0)
                .MakeGenericMethod(windowType);

            var assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            return ((Array)findObjectsMethod.Invoke(null, null)!)
                .Cast<EditorWindow>()
                .Where(window => string.Equals(
                    selectedGuidProperty!.GetValue(window) as string,
                    assetGuid,
                    StringComparison.Ordinal))
                .ToArray();
        }
    }
}
