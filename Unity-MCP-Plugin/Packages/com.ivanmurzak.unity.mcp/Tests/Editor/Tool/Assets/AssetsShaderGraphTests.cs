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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
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
        const string LitFullTemplateAssetPath =
            "Packages/com.unity.shadergraph/GraphTemplates/Cross Pipeline/1_Lit Full.shadergraph";
        const string MinionsArtWaterTrialAssetPath =
            "Assets/ShaderGraphValidation/MinionsArtWaterTrial/StylizedWaterInteractiveUpdate.shadergraph";
        const string MinionsArtWaterRecreatedTrialAssetPath =
            "Assets/ShaderGraphValidation/MinionsArtWaterTrial/Codex_StylizedWaterInteractiveUpdate_Recreated.shadergraph";
        const string TestFolder = "Assets/Unity-MCP-Test/ShaderGraphs";
        static readonly JsonSerializerOptions ShaderGraphTestJsonWriteOptions = new()
        {
            WriteIndented = true
        };

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
        public void ShaderGraph_GetStructure_ReadsMinionsArtWaterBehaviorNodes()
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(MinionsArtWaterTrialAssetPath);
            Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{MinionsArtWaterTrialAssetPath}'.");

            var structure = new Tool_Assets_ShaderGraph().GetStructure(new AssetObjectRef(shader));

            Assert.IsTrue(structure.SourceParsed, "The MinionsArt water reference graph should parse successfully.");

            var comparisonNode = structure.Nodes!.Single(node => node.Type == "UnityEditor.ShaderGraph.ComparisonNode");
            Assert.IsNotNull(comparisonNode.Comparison);
            Assert.AreEqual("less", comparisonNode.Comparison!.ComparisonType);
            Assert.AreEqual("UnityEditor.ShaderGraph.BooleanMaterialSlot", FindSlot(comparisonNode, "Out").Type);

            var sceneColorNode = structure.Nodes.Single(node => node.Type == "UnityEditor.ShaderGraph.SceneColorNode");
            Assert.AreEqual("UnityEditor.ShaderGraph.ScreenPositionMaterialSlot", FindSlot(sceneColorNode, "UV").Type);
            Assert.AreEqual("UnityEditor.ShaderGraph.Vector3MaterialSlot", FindSlot(sceneColorNode, "Out").Type);

            var normalFromHeightNode = structure.Nodes.Single(node => node.Type == "UnityEditor.ShaderGraph.NormalFromHeightNode");
            Assert.IsNotNull(normalFromHeightNode.NormalFromHeight);
            Assert.AreEqual("world", normalFromHeightNode.NormalFromHeight!.OutputSpace);
            Assert.AreEqual(0.01f, normalFromHeightNode.NormalFromHeight.Strength ?? 0f, 0.0001f);

            var swizzleNode = structure.Nodes.Single(node => node.Type == "UnityEditor.ShaderGraph.SwizzleNode");
            Assert.IsNotNull(swizzleNode.Swizzle);
            Assert.AreEqual("xz", swizzleNode.Swizzle!.Mask);
            Assert.AreEqual("xz", swizzleNode.Swizzle.NormalizedMask);
            Assert.AreEqual("UnityEditor.ShaderGraph.Vector3MaterialSlot", FindSlot(swizzleNode, "In").Type);
            Assert.AreEqual("UnityEditor.ShaderGraph.Vector2MaterialSlot", FindSlot(swizzleNode, "Out").Type);

            var remapNode = structure.Nodes.Single(node => node.Type == "UnityEditor.ShaderGraph.RemapNode");
            Assert.AreEqual("UnityEditor.ShaderGraph.Vector2MaterialSlot", FindSlot(remapNode, "In Min Max").Type);
            Assert.AreEqual("UnityEditor.ShaderGraph.Vector2MaterialSlot", FindSlot(remapNode, "Out Min Max").Type);

            var blendNode = structure.Nodes.Single(node => node.Type == "UnityEditor.ShaderGraph.BlendNode");
            Assert.IsNotNull(blendNode.Blend);
            Assert.AreEqual("screen", blendNode.Blend!.BlendMode);
            Assert.AreEqual("UnityEditor.ShaderGraph.Vector1MaterialSlot", FindSlot(blendNode, "Opacity").Type);

            var redirectNodeCount = structure.Nodes.Count(node => node.Type == "UnityEditor.ShaderGraph.RedirectNodeData");
            Assert.AreEqual(2, redirectNodeCount, "The reference graph should retain its non-behavior redirect nodes for readability.");
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
                    includeGraph: true,
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
        public void ShaderGraph_SetBlocks_AddsAndOrdersFragmentBlocks()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_SetBlocks.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var result = new Tool_Assets_ShaderGraph().SetBlocks(
                    new AssetObjectRef(shader),
                    new ShaderGraphSetBlocksInput
                    {
                        Context = "fragment",
                        Blocks = new()
                        {
                            "baseColor",
                            "emission",
                            "alpha",
                            "alphaClipThreshold"
                        }
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual("setBlocks", result.Operation);
                Assert.AreEqual("fragment", result.Context);
                Assert.IsTrue(result.ChangedFields!.Contains("fragmentContext.blocks"));
                Assert.IsTrue(result.ChangedFields.Contains("block.created"));
                Assert.IsNotEmpty(result.CreatedBlockNodeIds);
                Assert.IsEmpty(result.RemovedBlockNodeIds);
                CollectionAssert.AreEqual(
                    new[]
                    {
                        "SurfaceDescription.BaseColor",
                        "SurfaceDescription.Emission",
                        "SurfaceDescription.Alpha",
                        "SurfaceDescription.AlphaClipThreshold"
                    },
                    result.BlockDescriptors);

                Assert.IsNotNull(result.Structure);
                var nodesById = result.Structure!.Nodes!.ToDictionary(node => node.ObjectId);
                var fragmentDescriptors = result.Structure.FragmentContext!.BlockNodeIds!
                    .Select(id => nodesById[id].SerializedDescriptor)
                    .ToArray();
                CollectionAssert.AreEqual(result.BlockDescriptors, fragmentDescriptors);

                var alphaClipBlock = result.Structure.Nodes!.First(node =>
                    node.SerializedDescriptor == "SurfaceDescription.AlphaClipThreshold");
                Assert.AreEqual("UnityEditor.ShaderGraph.BlockNode", alphaClipBlock.Type);
                Assert.IsNotEmpty(alphaClipBlock.Slots);
                Assert.AreEqual("Alpha Clip Threshold", alphaClipBlock.Slots!.Single().DisplayName);

                Assert.IsNotNull(result.Graph);
                Assert.IsTrue(result.Graph!.SourceParsed, "Updated Shader Graph source should parse successfully.");
                Assert.IsTrue(result.Graph.ShaderResolved, "Updated Shader Graph should still resolve a compiled shader.");
                Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Adding a stable built-in stack block should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_SetBlocks_HandlesLitFragmentBlocks()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_SetBlocks_Lit.shadergraph", LitFullTemplateAssetPath);
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var result = new Tool_Assets_ShaderGraph().SetBlocks(
                    new AssetObjectRef(shader),
                    new ShaderGraphSetBlocksInput
                    {
                        Context = "fragment",
                        Blocks = new()
                        {
                            "baseColor",
                            "normalTS",
                            "metallic",
                            "specular",
                            "smoothness",
                            "occlusion",
                            "emission",
                            "alpha",
                            "alphaClipThreshold",
                            "bentNormal"
                        }
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                CollectionAssert.AreEqual(
                    new[]
                    {
                        "SurfaceDescription.BaseColor",
                        "SurfaceDescription.NormalTS",
                        "SurfaceDescription.Metallic",
                        "SurfaceDescription.Specular",
                        "SurfaceDescription.Smoothness",
                        "SurfaceDescription.Occlusion",
                        "SurfaceDescription.Emission",
                        "SurfaceDescription.Alpha",
                        "SurfaceDescription.AlphaClipThreshold",
                        "SurfaceDescription.BentNormal"
                    },
                    result.BlockDescriptors);
                Assert.IsEmpty(result.RemovedBlockNodeIds);

                Assert.IsNotNull(result.Graph);
                Assert.IsTrue(result.Graph!.SourceParsed, "Updated Lit Shader Graph source should parse successfully.");
                Assert.IsTrue(result.Graph.ShaderResolved, "Updated Lit Shader Graph should still resolve a compiled shader.");
                Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Reordering common Lit fragment blocks should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddProperty_AssignsTexture2DAssetReference()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddTextureAsset.shadergraph");
            var textureAssetPath = CreateTextureAsset("Validation_Add_Texture.png", new Color(0.15f, 0.7f, 0.35f, 1f));
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var textureGuid = AssetDatabase.AssetPathToGUID(textureAssetPath);
                var result = new Tool_Assets_ShaderGraph().AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "texture2D",
                        DisplayName = "Reference Texture",
                        OverrideReferenceName = "_CodexReferenceTexture",
                        TextureAssetPath = textureAssetPath,
                        TextureDefaultType = "black",
                        TextureUseTilingAndOffset = true
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual("add", result.Operation);
                Assert.IsNotNull(result.Property);
                Assert.AreEqual("texture2D", result.Property!.PropertyKind);
                Assert.AreEqual(textureGuid, result.Property.TextureAssetGuid);
                Assert.AreEqual(textureAssetPath, result.Property.TextureAssetPath);
                Assert.AreEqual("black", result.Property.TextureDefaultType);
                Assert.IsTrue(result.Property.TextureUseTilingAndOffset ?? false);

                var structureProperty = result.Structure!.Properties!
                    .Single(property => property.EffectiveReferenceName == "_CodexReferenceTexture");
                Assert.AreEqual(textureGuid, structureProperty.TextureAssetGuid);
                Assert.AreEqual(textureAssetPath, structureProperty.TextureAssetPath);

                Assert.IsNotNull(result.Graph);
                Assert.IsTrue(result.Graph!.SourceParsed, "Updated Shader Graph source should parse successfully.");
                Assert.IsTrue(result.Graph.ShaderResolved, "Updated Shader Graph should still resolve a compiled shader.");
                Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Assigning a Texture2D asset reference should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
                CleanupTestAsset(textureAssetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateProperty_AssignsAndClearsTexture2DAssetReference()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateTextureAsset.shadergraph");
            var textureAssetPath = CreateTextureAsset("Validation_Update_Texture.png", new Color(0.9f, 0.2f, 0.1f, 1f));
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var textureGuid = AssetDatabase.AssetPathToGUID(textureAssetPath);
                var updateResult = new Tool_Assets_ShaderGraph().UpdateProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphPropertyUpdateInput
                    {
                        PropertyReferenceName = "_BaseMap",
                        TextureAssetPath = textureAssetPath
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual("update", updateResult.Operation);
                Assert.IsTrue(updateResult.ChangedFields!.Contains("property.texture.assetGuid"));
                Assert.AreEqual(textureGuid, updateResult.Property!.TextureAssetGuid);
                Assert.AreEqual(textureAssetPath, updateResult.Property.TextureAssetPath);
                Assert.IsTrue(updateResult.Graph!.ShaderResolved, "Assigned texture reference should keep Shader Graph import valid.");
                Assert.IsFalse(updateResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Assigning a Texture2D asset reference should not introduce import errors.");

                var clearResult = new Tool_Assets_ShaderGraph().UpdateProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphPropertyUpdateInput
                    {
                        PropertyReferenceName = "_BaseMap",
                        TextureAssetPath = string.Empty
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsTrue(clearResult.ChangedFields!.Contains("property.texture.assetGuid"));
                Assert.IsNull(clearResult.Property!.TextureAssetGuid);
                Assert.IsNull(clearResult.Property.TextureAssetPath);
                Assert.IsTrue(clearResult.Graph!.ShaderResolved, "Clearing texture reference should keep Shader Graph import valid.");
                Assert.IsFalse(clearResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Clearing a Texture2D asset reference should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
                CleanupTestAsset(textureAssetPath);
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
        public void ShaderGraph_AddProperty_GeneratedReferenceNameCanBeUsedForLookup()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_GeneratedReferenceName.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var addResult = tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Generated Lookup",
                        FloatValue = 0.25f
                    });

                Assert.IsNotNull(addResult.Property);
                Assert.AreEqual("_Generated_Lookup", addResult.Property!.EffectiveReferenceName);

                var updateResult = tool.UpdateProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphPropertyUpdateInput
                    {
                        PropertyReferenceName = "_Generated_Lookup",
                        Hidden = true
                    });

                Assert.IsNotNull(updateResult.Property);
                Assert.IsTrue(updateResult.Property!.Hidden);

                var propertyNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_Generated_Lookup",
                        PositionX = -720f,
                        PositionY = 420f
                    });

                Assert.IsNotNull(propertyNodeResult.Node);
                Assert.AreEqual("_Generated_Lookup", propertyNodeResult.Node!.PropertyReferenceName);

                Assert.Throws<InvalidOperationException>(() => tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Generated-Lookup",
                        FloatValue = 0.5f
                    }));
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
        public void ShaderGraph_DeleteProperty_MinionsArtWaterDeletesAllProperties()
        {
            var assetPath = CreateProjectShaderGraphAssetCopy(
                "Validation_DeleteMinionsArtWaterProperties.shadergraph",
                MinionsArtWaterRecreatedTrialAssetPath);
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var graphRef = new AssetObjectRef(assetPath);
                var initialStructure = tool.GetStructure(graphRef);
                Assert.IsTrue(initialStructure.SourceParsed, "The copied MinionsArt water graph should parse before deletion.");
                Assert.IsNotNull(initialStructure.Properties, "Expected the copied MinionsArt water graph to expose properties.");

                var propertiesToDelete = initialStructure.Properties!
                    .Select(property => new
                    {
                        property.ObjectId,
                        property.EffectiveReferenceName
                    })
                    .ToList();

                Assert.Greater(propertiesToDelete.Count, 0, "Expected at least one MinionsArt water property to delete.");
                Assert.IsFalse(propertiesToDelete.Any(property => string.IsNullOrWhiteSpace(property.ObjectId)),
                    "Every MinionsArt water property should expose a stable object id for deletion.");
                Assert.IsTrue(propertiesToDelete.Any(property =>
                        string.Equals(property.EffectiveReferenceName, "_GlobalEffectRT", StringComparison.Ordinal)),
                    "The regression graph must include the _GlobalEffectRT property that previously produced a null response.");

                foreach (var propertyToDelete in propertiesToDelete)
                {
                    var deleteResult = tool.DeleteProperty(
                        graphRef,
                        new ShaderGraphDeletePropertyInput
                        {
                            PropertyObjectId = propertyToDelete.ObjectId
                        },
                        includeStructure: true,
                        includeGraph: true,
                        includeMessages: true,
                        includeProperties: true);

                    Assert.IsNotNull(deleteResult,
                        $"Deleting '{propertyToDelete.EffectiveReferenceName ?? propertyToDelete.ObjectId}' should return a mutation response.");
                    Assert.AreEqual("delete", deleteResult.Operation);
                    Assert.AreEqual(propertyToDelete.ObjectId, deleteResult.PropertyObjectId);
                    Assert.AreEqual(propertyToDelete.EffectiveReferenceName, deleteResult.PropertyReferenceName);
                    Assert.IsNotNull(deleteResult.Graph,
                        $"Deleting '{propertyToDelete.EffectiveReferenceName ?? propertyToDelete.ObjectId}' should return graph diagnostics.");
                    Assert.IsFalse(deleteResult.Graph!.Diagnostics?.Any(d => d.Severity == "Error") ?? false,
                        $"Deleting '{propertyToDelete.EffectiveReferenceName ?? propertyToDelete.ObjectId}' should not emit Shader Graph diagnostic errors.");

                    if (deleteResult.Structure?.Properties != null)
                    {
                        Assert.IsFalse(deleteResult.Structure.Properties.Any(property =>
                                string.Equals(property.ObjectId, propertyToDelete.ObjectId, StringComparison.Ordinal)),
                            $"Deleted property '{propertyToDelete.EffectiveReferenceName ?? propertyToDelete.ObjectId}' should be absent from post-delete readback.");
                    }
                }

                var finalStructure = tool.GetStructure(graphRef);
                Assert.IsTrue(finalStructure.SourceParsed, "The MinionsArt water graph should parse after deleting every property.");
                Assert.Zero(finalStructure.Properties?.Count ?? 0, "Every original MinionsArt water property should be deleted.");
                Assert.IsFalse(finalStructure.Nodes?.Any(node => !string.IsNullOrWhiteSpace(node.PropertyReferenceName)) ?? false,
                    "Deleting every property should remove dependent PropertyNode instances from the graph.");
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
        public void ShaderGraph_AddProperty_WithoutCategorySelectorUsesEmptyDefaultCategory()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_DefaultCategoryFallback.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var categoryResult = tool.CreateCategory(
                    new AssetObjectRef(shader),
                    new ShaderGraphCreateCategoryInput
                    {
                        CategoryName = "Named First"
                    });

                MoveCategoryReferenceToFront(assetPath, categoryResult.CategoryObjectId!);

                var propertyResult = tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Default Category Strength",
                        FloatValue = 0.6f
                    });

                Assert.IsNotNull(propertyResult.Property);
                Assert.AreEqual(string.Empty, propertyResult.Property!.CategoryName);

                var namedCategory = propertyResult.Structure!.Categories!
                    .First(c => c.Name == "Named First");
                var defaultCategory = propertyResult.Structure.Categories!
                    .First(c => string.IsNullOrEmpty(c.Name));

                CollectionAssert.DoesNotContain(namedCategory.PropertyObjectIds!, propertyResult.PropertyObjectId);
                CollectionAssert.Contains(defaultCategory.PropertyObjectIds!, propertyResult.PropertyObjectId);
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
        public void ShaderGraph_AddNode_AddsReflectionOutlineCoreNodes()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddNode_Epic7A.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var nodesToCreate = new[]
                {
                    new { ApiName = "viewDirection", TypeName = "UnityEditor.ShaderGraph.ViewDirectionNode", DisplayName = "View Direction", SlotName = "Out" },
                    new { ApiName = "viewVector", TypeName = "UnityEditor.ShaderGraph.ViewVectorNode", DisplayName = "View Vector", SlotName = "Out" },
                    new { ApiName = "normalVector", TypeName = "UnityEditor.ShaderGraph.NormalVectorNode", DisplayName = "Normal Vector", SlotName = "Out" },
                    new { ApiName = "position", TypeName = "UnityEditor.ShaderGraph.PositionNode", DisplayName = "Position", SlotName = "Out" },
                    new { ApiName = "object", TypeName = "UnityEditor.ShaderGraph.ObjectNode", DisplayName = "Object", SlotName = "Scale" },
                    new { ApiName = "transform", TypeName = "UnityEditor.ShaderGraph.TransformNode", DisplayName = "Transform", SlotName = "In" },
                    new { ApiName = "gradientNoise", TypeName = "UnityEditor.ShaderGraph.GradientNoiseNode", DisplayName = "Gradient Noise", SlotName = "Scale" },
                    new { ApiName = "sine", TypeName = "UnityEditor.ShaderGraph.SineNode", DisplayName = "Sine", SlotName = "In" },
                    new { ApiName = "cosine", TypeName = "UnityEditor.ShaderGraph.CosineNode", DisplayName = "Cosine", SlotName = "In" },
                    new { ApiName = "negate", TypeName = "UnityEditor.ShaderGraph.NegateNode", DisplayName = "Negate", SlotName = "In" }
                };

                ShaderGraphNodeMutationResultData? transformResult = null;
                for (var i = 0; i < nodesToCreate.Length; i++)
                {
                    var nodeToCreate = nodesToCreate[i];
                    var result = tool.AddNode(
                        new AssetObjectRef(shader),
                        new ShaderGraphAddNodeInput
                        {
                            NodeType = nodeToCreate.ApiName,
                            PositionX = -1200f + i * 120f,
                            PositionY = 60f + i * 40f
                        },
                        includeGraph: i == nodesToCreate.Length - 1,
                        includeMessages: i == nodesToCreate.Length - 1,
                        includeProperties: i == nodesToCreate.Length - 1);

                    Assert.AreEqual("add", result.Operation);
                    Assert.IsTrue(result.ChangedFields!.Contains("node.added"));
                    Assert.IsNotNull(result.Node);
                    Assert.AreEqual(nodeToCreate.TypeName, result.Node!.Type);
                    Assert.AreEqual(nodeToCreate.DisplayName, result.Node.Name);
                    Assert.IsNotEmpty(result.Node.Slots, $"Expected '{nodeToCreate.DisplayName}' to expose slots.");
                    Assert.IsTrue(result.Node.Slots!.Any(slot => slot.DisplayName == nodeToCreate.SlotName),
                        $"Expected '{nodeToCreate.DisplayName}' to expose slot '{nodeToCreate.SlotName}'.");

                    if (nodeToCreate.ApiName == "transform")
                        transformResult = result;

                    if (i == nodesToCreate.Length - 1)
                    {
                        Assert.IsNotNull(result.Graph);
                        Assert.IsTrue(result.Graph!.ShaderResolved, "Adding Epic 7A nodes should keep the Shader Graph import valid.");
                        Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                            "Adding Epic 7A nodes should not introduce import errors.");
                    }
                }

                Assert.IsNotNull(transformResult, "Expected the Transform node to be created for duplicate/move/delete validation.");
                var duplicateResult = tool.DuplicateNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphDuplicateNodeInput
                    {
                        NodeObjectId = transformResult!.Node!.ObjectId,
                        PositionOffsetX = 88f,
                        PositionOffsetY = 44f
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(duplicateResult.Node);
                Assert.AreEqual("UnityEditor.ShaderGraph.TransformNode", duplicateResult.Node!.Type);
                Assert.AreNotEqual(transformResult.Node.ObjectId, duplicateResult.Node.ObjectId);
                Assert.AreEqual(transformResult.Node.PositionX + 88f, duplicateResult.Node.PositionX, 0.001f);
                Assert.AreEqual(transformResult.Node.PositionY + 44f, duplicateResult.Node.PositionY, 0.001f);

                var moveResult = tool.UpdateNodePosition(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodePositionInput
                    {
                        NodeObjectId = duplicateResult.Node.ObjectId,
                        PositionX = -320f,
                        PositionY = 480f
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual(-320f, moveResult.Node!.PositionX);
                Assert.AreEqual(480f, moveResult.Node.PositionY);

                var deleteResult = tool.DeleteNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphDeleteNodeInput
                    {
                        NodeObjectId = duplicateResult.Node.ObjectId
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual("delete", deleteResult.Operation);
                Assert.IsFalse(deleteResult.Structure!.Nodes!.Any(node => node.ObjectId == duplicateResult.Node.ObjectId));
                Assert.IsTrue(deleteResult.Graph!.ShaderResolved, "Deleting a duplicated Epic 7A node should keep the Shader Graph import valid.");
                Assert.IsFalse(deleteResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Deleting a duplicated Epic 7A node should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddNode_AddsWaterCoreNodes()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddNode_WaterCore.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var nodesToCreate = new[]
                {
                    new
                    {
                        ApiName = "screenPosition",
                        TypeName = "UnityEditor.ShaderGraph.ScreenPositionNode",
                        DisplayName = "Screen Position",
                        SlotNames = new[] { "Out" }
                    },
                    new
                    {
                        ApiName = "sceneDepth",
                        TypeName = "UnityEditor.ShaderGraph.SceneDepthNode",
                        DisplayName = "Scene Depth",
                        SlotNames = new[] { "UV", "Out" }
                    },
                    new
                    {
                        ApiName = "time",
                        TypeName = "UnityEditor.ShaderGraph.TimeNode",
                        DisplayName = "Time",
                        SlotNames = new[] { "Time", "Sine Time", "Cosine Time", "Delta Time", "Smooth Delta" }
                    },
                    new
                    {
                        ApiName = "smoothstep",
                        TypeName = "UnityEditor.ShaderGraph.SmoothstepNode",
                        DisplayName = "Smoothstep",
                        SlotNames = new[] { "Edge1", "Edge2", "In", "Out" }
                    },
                    new
                    {
                        ApiName = "saturate",
                        TypeName = "UnityEditor.ShaderGraph.SaturateNode",
                        DisplayName = "Saturate",
                        SlotNames = new[] { "In", "Out" }
                    },
                    new
                    {
                        ApiName = "vector2",
                        TypeName = "UnityEditor.ShaderGraph.Vector2Node",
                        DisplayName = "Vector 2",
                        SlotNames = new[] { "X", "Y", "Out" }
                    }
                };

                ShaderGraphNodeMutationResultData? sceneDepthResult = null;
                for (var i = 0; i < nodesToCreate.Length; i++)
                {
                    var nodeToCreate = nodesToCreate[i];
                    var result = tool.AddNode(
                        new AssetObjectRef(shader),
                        new ShaderGraphAddNodeInput
                        {
                            NodeType = nodeToCreate.ApiName,
                            PositionX = -1180f + i * 180f,
                            PositionY = 520f + i * 30f
                        },
                        includeGraph: i == nodesToCreate.Length - 1,
                        includeMessages: i == nodesToCreate.Length - 1,
                        includeProperties: i == nodesToCreate.Length - 1);

                    Assert.AreEqual("add", result.Operation);
                    Assert.IsTrue(result.ChangedFields!.Contains("node.added"));
                    Assert.IsNotNull(result.Node);
                    Assert.AreEqual(nodeToCreate.TypeName, result.Node!.Type);
                    Assert.AreEqual(nodeToCreate.DisplayName, result.Node.Name);
                    Assert.IsNotEmpty(result.Node.Slots, $"Expected '{nodeToCreate.DisplayName}' to expose slots.");
                    foreach (var slotName in nodeToCreate.SlotNames)
                    {
                        Assert.IsTrue(result.Node.Slots!.Any(slot => slot.DisplayName == slotName),
                            $"Expected '{nodeToCreate.DisplayName}' to expose slot '{slotName}'.");
                    }

                    if (nodeToCreate.ApiName == "sceneDepth")
                        sceneDepthResult = result;

                    if (i == nodesToCreate.Length - 1)
                    {
                        Assert.IsNotNull(result.Graph);
                        Assert.IsTrue(result.Graph!.ShaderResolved, "Adding water-core nodes should keep the Shader Graph import valid.");
                        Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                            "Adding water-core nodes should not introduce import errors.");
                    }
                }

                Assert.IsNotNull(sceneDepthResult, "Expected Scene Depth to be created for lifecycle validation.");
                var duplicateResult = tool.DuplicateNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphDuplicateNodeInput
                    {
                        NodeObjectId = sceneDepthResult!.Node!.ObjectId,
                        PositionOffsetX = 72f,
                        PositionOffsetY = 36f
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(duplicateResult.Node);
                Assert.AreEqual("UnityEditor.ShaderGraph.SceneDepthNode", duplicateResult.Node!.Type);
                Assert.AreNotEqual(sceneDepthResult.Node.ObjectId, duplicateResult.Node.ObjectId);

                var moveResult = tool.UpdateNodePosition(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodePositionInput
                    {
                        NodeObjectId = duplicateResult.Node.ObjectId,
                        PositionX = -260f,
                        PositionY = 620f
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual(-260f, moveResult.Node!.PositionX);
                Assert.AreEqual(620f, moveResult.Node.PositionY);

                var deleteResult = tool.DeleteNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphDeleteNodeInput
                    {
                        NodeObjectId = duplicateResult.Node.ObjectId
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual("delete", deleteResult.Operation);
                Assert.IsFalse(deleteResult.Structure!.Nodes!.Any(node => node.ObjectId == duplicateResult.Node.ObjectId));
                Assert.IsTrue(deleteResult.Graph!.ShaderResolved, "Deleting a duplicated water-core node should keep the Shader Graph import valid.");
                Assert.IsFalse(deleteResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Deleting a duplicated water-core node should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddNode_AddsDissolveTrialNodes()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddNode_DissolveTrial.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var nodesToCreate = new[]
                {
                    new
                    {
                        ApiName = "fraction",
                        TypeName = "UnityEditor.ShaderGraph.FractionNode",
                        DisplayName = "Fraction",
                        SlotNames = new[] { "In", "Out" }
                    },
                    new
                    {
                        ApiName = "step",
                        TypeName = "UnityEditor.ShaderGraph.StepNode",
                        DisplayName = "Step",
                        SlotNames = new[] { "Edge", "In", "Out" }
                    },
                    new
                    {
                        ApiName = "invertColors",
                        TypeName = "UnityEditor.ShaderGraph.InvertColorsNode",
                        DisplayName = "Invert Colors",
                        SlotNames = new[] { "In", "Out" }
                    }
                };

                ShaderGraphNodeMutationResultData? invertColorsResult = null;
                for (var i = 0; i < nodesToCreate.Length; i++)
                {
                    var nodeToCreate = nodesToCreate[i];
                    var result = tool.AddNode(
                        new AssetObjectRef(shader),
                        new ShaderGraphAddNodeInput
                        {
                            NodeType = nodeToCreate.ApiName,
                            PositionX = -1020f + i * 220f,
                            PositionY = 420f + i * 40f
                        },
                        includeGraph: i == nodesToCreate.Length - 1,
                        includeMessages: i == nodesToCreate.Length - 1,
                        includeProperties: i == nodesToCreate.Length - 1);

                    Assert.AreEqual("add", result.Operation);
                    Assert.IsTrue(result.ChangedFields!.Contains("node.added"));
                    Assert.IsNotNull(result.Node);
                    Assert.AreEqual(nodeToCreate.TypeName, result.Node!.Type);
                    Assert.AreEqual(nodeToCreate.DisplayName, result.Node.Name);
                    Assert.IsNotEmpty(result.Node.Slots, $"Expected '{nodeToCreate.DisplayName}' to expose slots.");
                    foreach (var slotName in nodeToCreate.SlotNames)
                    {
                        Assert.IsTrue(result.Node.Slots!.Any(slot => slot.DisplayName == slotName),
                            $"Expected '{nodeToCreate.DisplayName}' to expose slot '{slotName}'.");
                    }

                    if (nodeToCreate.ApiName == "invertColors")
                        invertColorsResult = result;

                    if (i == nodesToCreate.Length - 1)
                    {
                        Assert.IsNotNull(result.Graph);
                        Assert.IsTrue(result.Graph!.ShaderResolved, "Adding dissolve-trial nodes should keep the Shader Graph import valid.");
                        Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                            "Adding dissolve-trial nodes should not introduce import errors.");
                    }
                }

                Assert.IsNotNull(invertColorsResult, "Expected Invert Colors to be created for lifecycle validation.");
                var duplicateResult = tool.DuplicateNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphDuplicateNodeInput
                    {
                        NodeObjectId = invertColorsResult!.Node!.ObjectId,
                        PositionOffsetX = 64f,
                        PositionOffsetY = 48f
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(duplicateResult.Node);
                Assert.AreEqual("UnityEditor.ShaderGraph.InvertColorsNode", duplicateResult.Node!.Type);
                Assert.AreNotEqual(invertColorsResult.Node.ObjectId, duplicateResult.Node.ObjectId);

                var moveResult = tool.UpdateNodePosition(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodePositionInput
                    {
                        NodeObjectId = duplicateResult.Node.ObjectId,
                        PositionX = -340f,
                        PositionY = 520f
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual(-340f, moveResult.Node!.PositionX);
                Assert.AreEqual(520f, moveResult.Node.PositionY);

                var deleteResult = tool.DeleteNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphDeleteNodeInput
                    {
                        NodeObjectId = duplicateResult.Node.ObjectId
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual("delete", deleteResult.Operation);
                Assert.IsFalse(deleteResult.Structure!.Nodes!.Any(node => node.ObjectId == duplicateResult.Node.ObjectId));
                Assert.IsTrue(deleteResult.Graph!.ShaderResolved, "Deleting a duplicated dissolve-trial node should keep the Shader Graph import valid.");
                Assert.IsFalse(deleteResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Deleting a duplicated dissolve-trial node should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddNode_AddsMinionsArtWaterBehaviorNodes()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddNode_MinionsArtWaterBehavior.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var nodesToCreate = new[]
                {
                    new
                    {
                        ApiName = "sceneColor",
                        TypeName = "UnityEditor.ShaderGraph.SceneColorNode",
                        DisplayName = "Scene Color",
                        SlotNames = new[] { "UV", "Out" }
                    },
                    new
                    {
                        ApiName = "comparison",
                        TypeName = "UnityEditor.ShaderGraph.ComparisonNode",
                        DisplayName = "Comparison",
                        SlotNames = new[] { "A", "B", "Out" }
                    },
                    new
                    {
                        ApiName = "normalFromHeight",
                        TypeName = "UnityEditor.ShaderGraph.NormalFromHeightNode",
                        DisplayName = "Normal From Height",
                        SlotNames = new[] { "In", "Strength", "Out" }
                    },
                    new
                    {
                        ApiName = "blend",
                        TypeName = "UnityEditor.ShaderGraph.BlendNode",
                        DisplayName = "Blend",
                        SlotNames = new[] { "Base", "Blend", "Opacity", "Out" }
                    },
                    new
                    {
                        ApiName = "remap",
                        TypeName = "UnityEditor.ShaderGraph.RemapNode",
                        DisplayName = "Remap",
                        SlotNames = new[] { "In", "In Min Max", "Out Min Max", "Out" }
                    },
                    new
                    {
                        ApiName = "swizzle",
                        TypeName = "UnityEditor.ShaderGraph.SwizzleNode",
                        DisplayName = "Swizzle",
                        SlotNames = new[] { "In", "Out" }
                    }
                };

                ShaderGraphNodeMutationResultData? blendResult = null;
                for (var i = 0; i < nodesToCreate.Length; i++)
                {
                    var nodeToCreate = nodesToCreate[i];
                    var result = tool.AddNode(
                        new AssetObjectRef(shader),
                        new ShaderGraphAddNodeInput
                        {
                            NodeType = nodeToCreate.ApiName,
                            PositionX = -1180f + i * 180f,
                            PositionY = 700f + i * 28f
                        },
                        includeGraph: i == nodesToCreate.Length - 1,
                        includeMessages: i == nodesToCreate.Length - 1,
                        includeProperties: i == nodesToCreate.Length - 1);

                    Assert.AreEqual("add", result.Operation);
                    Assert.IsTrue(result.ChangedFields!.Contains("node.added"));
                    Assert.IsNotNull(result.Node);
                    Assert.AreEqual(nodeToCreate.TypeName, result.Node!.Type);
                    Assert.AreEqual(nodeToCreate.DisplayName, result.Node.Name);
                    foreach (var slotName in nodeToCreate.SlotNames)
                    {
                        Assert.IsTrue(result.Node.Slots!.Any(slot => slot.DisplayName == slotName),
                            $"Expected '{nodeToCreate.DisplayName}' to expose slot '{slotName}'.");
                    }

                    if (nodeToCreate.ApiName == "blend")
                        blendResult = result;

                    if (i == nodesToCreate.Length - 1)
                    {
                        Assert.IsNotNull(result.Graph);
                        Assert.IsTrue(result.Graph!.ShaderResolved, "Adding the MinionsArt behavior nodes should keep the Shader Graph import valid.");
                        Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                            "Adding the MinionsArt behavior nodes should not introduce import errors.");
                    }
                }

                Assert.IsNotNull(blendResult, "Expected Blend to be created for lifecycle validation.");
                var duplicateResult = tool.DuplicateNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphDuplicateNodeInput
                    {
                        NodeObjectId = blendResult!.Node!.ObjectId,
                        PositionOffsetX = 84f,
                        PositionOffsetY = 48f
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(duplicateResult.Node);
                Assert.AreEqual("UnityEditor.ShaderGraph.BlendNode", duplicateResult.Node!.Type);
                Assert.AreNotEqual(blendResult.Node.ObjectId, duplicateResult.Node.ObjectId);

                var moveResult = tool.UpdateNodePosition(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodePositionInput
                    {
                        NodeObjectId = duplicateResult.Node.ObjectId,
                        PositionX = -320f,
                        PositionY = 920f
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual(-320f, moveResult.Node!.PositionX);
                Assert.AreEqual(920f, moveResult.Node.PositionY);

                var deleteResult = tool.DeleteNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphDeleteNodeInput
                    {
                        NodeObjectId = duplicateResult.Node.ObjectId
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual("delete", deleteResult.Operation);
                Assert.IsFalse(deleteResult.Structure!.Nodes!.Any(node => node.ObjectId == duplicateResult.Node.ObjectId));
                Assert.IsTrue(deleteResult.Graph!.ShaderResolved, "Deleting a duplicated MinionsArt behavior node should keep the Shader Graph import valid.");
                Assert.IsFalse(deleteResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Deleting a duplicated MinionsArt behavior node should not introduce import errors.");
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
        public void ShaderGraph_UpdateNodeSettings_AssignsSampleTexture2DTextureSlotAsset()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodeTextureSlot.shadergraph");
            var textureAssetPath = CreateTextureAsset("Validation_Node_Texture.png", new Color(0.2f, 0.8f, 0.95f, 1f));
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var sampleNodeResult = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "sampleTexture2D",
                        PositionX = -640f,
                        PositionY = 40f
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                var textureGuid = AssetDatabase.AssetPathToGUID(textureAssetPath);
                var result = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = sampleNodeResult.NodeObjectId,
                        SampleTexture2D = new ShaderGraphSampleTexture2DNodeSettingsUpdateInput
                        {
                            TextureSlotAssetPath = textureAssetPath,
                            TextureSlotDefaultType = "red"
                        }
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(result);
                Assert.IsTrue(result.ChangedFields!.Contains("node.sampleTexture2D.textureSlotAssetPath"));
                Assert.IsTrue(result.ChangedFields.Contains("node.sampleTexture2D.textureSlotDefaultType"));

                Assert.IsNotNull(result.Node);
                var textureSlot = result.Node!.Slots!
                    .Single(slot => slot.DisplayName == "Texture");
                Assert.AreEqual("UnityEditor.ShaderGraph.Texture2DInputMaterialSlot", textureSlot.Type);
                Assert.AreEqual(textureGuid, textureSlot.TextureAssetGuid);
                Assert.AreEqual(textureAssetPath, textureSlot.TextureAssetPath);
                Assert.AreEqual("red", textureSlot.TextureDefaultType);

                Assert.IsNotNull(result.Graph);
                Assert.IsTrue(result.Graph!.ShaderResolved, "Assigning a direct Sample Texture 2D slot texture should keep the Shader Graph import valid.");
                Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Assigning a direct Sample Texture 2D slot texture should not introduce import errors.");

                var clearResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = sampleNodeResult.NodeObjectId,
                        SampleTexture2D = new ShaderGraphSampleTexture2DNodeSettingsUpdateInput
                        {
                            TextureSlotAssetPath = string.Empty,
                            TextureSlotDefaultType = "black"
                        }
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                var clearedTextureSlot = clearResult.Node!.Slots!
                    .Single(slot => slot.DisplayName == "Texture");
                Assert.IsNull(clearedTextureSlot.TextureAssetGuid);
                Assert.IsNull(clearedTextureSlot.TextureAssetPath);
                Assert.AreEqual("black", clearedTextureSlot.TextureDefaultType);
                Assert.IsTrue(clearResult.Graph!.ShaderResolved, "Clearing a direct Sample Texture 2D slot texture should keep the Shader Graph import valid.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
                CleanupTestAsset(textureAssetPath);
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
        public void ShaderGraph_UpdateNodeSettings_UpdatesReflectionOutlineCoreSettings()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodeSettings_Epic7A.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var viewDirection = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "viewDirection", PositionX = -1100f, PositionY = 0f });
                var viewVector = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "viewVector", PositionX = -1100f, PositionY = 120f });
                var normalVector = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "normalVector", PositionX = -1100f, PositionY = 240f });
                var position = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "position", PositionX = -1100f, PositionY = 360f });
                var transform = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "transform", PositionX = -820f, PositionY = 0f });
                var gradientNoise = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "gradientNoise", PositionX = -820f, PositionY = 160f });
                var sine = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "sine", PositionX = -540f, PositionY = 0f });
                var cosine = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "cosine", PositionX = -540f, PositionY = 120f });
                var negate = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "negate", PositionX = -540f, PositionY = 240f });

                var viewDirectionResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = viewDirection.Node!.ObjectId,
                        ViewDirection = new ShaderGraphSpaceNodeSettingsUpdateInput { Space = "object" }
                    });
                var viewVectorResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = viewVector.Node!.ObjectId,
                        ViewVector = new ShaderGraphSpaceNodeSettingsUpdateInput { Space = "tangent" }
                    });
                var normalVectorResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = normalVector.Node!.ObjectId,
                        NormalVector = new ShaderGraphSpaceNodeSettingsUpdateInput { Space = "view" }
                    });
                var positionResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = position.Node!.ObjectId,
                        Position = new ShaderGraphPositionNodeSettingsUpdateInput
                        {
                            Space = "absoluteWorld",
                            PositionSource = "predisplacement"
                        }
                    });
                var transformResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = transform.Node!.ObjectId,
                        Transform = new ShaderGraphTransformNodeSettingsUpdateInput
                        {
                            InputSpace = "tangent",
                            OutputSpace = "absoluteWorld",
                            TransformType = "normal",
                            Normalize = false
                        }
                    });
                var gradientNoiseResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = gradientNoise.Node!.ObjectId,
                        GradientNoise = new ShaderGraphGradientNoiseNodeSettingsUpdateInput
                        {
                            Scale = 23.5f,
                            HashType = "legacyMod"
                        }
                    });
                var sineResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = sine.Node!.ObjectId,
                        Sine = new ShaderGraphUnaryVectorNodeSettingsUpdateInput
                        {
                            Input = new ShaderGraphVector4ValueUpdateInput { X = 0.1f, Y = 0.2f, Z = 0.3f, W = 0.4f }
                        }
                    });
                var cosineResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = cosine.Node!.ObjectId,
                        Cosine = new ShaderGraphUnaryVectorNodeSettingsUpdateInput
                        {
                            Input = new ShaderGraphVector4ValueUpdateInput { X = 0.5f, Y = 0.6f, Z = 0.7f, W = 0.8f }
                        }
                    });
                var negateResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = negate.Node!.ObjectId,
                        Negate = new ShaderGraphUnaryVectorNodeSettingsUpdateInput
                        {
                            Input = new ShaderGraphVector4ValueUpdateInput { X = -0.1f, Y = -0.2f, Z = -0.3f, W = -0.4f }
                        }
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual("object", viewDirectionResult.Node!.SourceVector!.Space);
                Assert.AreEqual("tangent", viewVectorResult.Node!.SourceVector!.Space);
                Assert.AreEqual("view", normalVectorResult.Node!.SourceVector!.Space);

                Assert.AreEqual("absoluteWorld", positionResult.Node!.Position!.Space);
                Assert.AreEqual("predisplacement", positionResult.Node.Position.PositionSource);

                Assert.AreEqual("tangent", transformResult.Node!.Transform!.InputSpace);
                Assert.AreEqual("absoluteWorld", transformResult.Node.Transform.OutputSpace);
                Assert.AreEqual("normal", transformResult.Node.Transform.TransformType);
                Assert.IsFalse(transformResult.Node.Transform.Normalize ?? true);

                Assert.AreEqual("legacyMod", gradientNoiseResult.Node!.GradientNoise!.HashType);
                Assert.AreEqual(23.5f, gradientNoiseResult.Node.GradientNoise.Scale ?? 0f, 0.0001f);
                AssertSlotFloat(gradientNoiseResult.Node, "Scale", 23.5f);

                AssertSlotVector4(sineResult.Node!, "In", 0.1f, 0.2f, 0.3f, 0.4f);
                AssertSlotVector4(cosineResult.Node!, "In", 0.5f, 0.6f, 0.7f, 0.8f);
                AssertSlotVector4(negateResult.Node!, "In", -0.1f, -0.2f, -0.3f, -0.4f);

                Assert.IsNotNull(negateResult.Graph);
                Assert.IsTrue(negateResult.Graph!.ShaderResolved, "Updating Epic 7A node settings should keep the Shader Graph import valid.");
                Assert.IsFalse(negateResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Updating Epic 7A node settings should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateNodeSettings_UpdatesWaterCoreSettings()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodeSettings_WaterCore.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var screenPosition = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "screenPosition", PositionX = -980f, PositionY = 0f });
                var sceneDepth = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "sceneDepth", PositionX = -720f, PositionY = 0f });
                var vector2 = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "vector2", PositionX = -980f, PositionY = 180f });

                var screenPositionResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = screenPosition.Node!.ObjectId,
                        ScreenPosition = new ShaderGraphScreenPositionNodeSettingsUpdateInput { Mode = "raw" }
                    });
                var sceneDepthResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = sceneDepth.Node!.ObjectId,
                        SceneDepth = new ShaderGraphSceneDepthNodeSettingsUpdateInput { SamplingMode = "eye" }
                    });
                var vector2Result = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = vector2.Node!.ObjectId,
                        Vector2 = new ShaderGraphVector2NodeSettingsUpdateInput
                        {
                            X = 0.125f,
                            Y = -0.25f
                        }
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual("raw", screenPositionResult.Node!.ScreenPosition!.Mode);
                Assert.AreEqual("eye", sceneDepthResult.Node!.SceneDepth!.SamplingMode);
                Assert.AreEqual(0.125f, vector2Result.Node!.Vector2!.X ?? 0f, 0.0001f);
                Assert.AreEqual(-0.25f, vector2Result.Node.Vector2.Y ?? 0f, 0.0001f);
                AssertSlotFloat(vector2Result.Node, "X", 0.125f);
                AssertSlotFloat(vector2Result.Node, "Y", -0.25f);

                var unsupportedScreenPositionMode = Assert.Throws<ArgumentException>(() => tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = screenPosition.Node.ObjectId,
                        ScreenPosition = new ShaderGraphScreenPositionNodeSettingsUpdateInput { Mode = "center" }
                    }));
                StringAssert.Contains("Supported values: default, raw", unsupportedScreenPositionMode!.Message);

                Assert.IsNotNull(vector2Result.Graph);
                Assert.IsTrue(vector2Result.Graph!.ShaderResolved, "Updating water-core node settings should keep the Shader Graph import valid.");
                Assert.IsFalse(vector2Result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Updating water-core node settings should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddNode_AddsWorldSpaceDepthFadeNodes()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddNode_WorldSpaceDepthFade.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var camera = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "camera", PositionX = -880f, PositionY = 100f });
                var exponential = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "exponential", PositionX = -620f, PositionY = 100f },
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual("UnityEditor.ShaderGraph.CameraNode", camera.Node!.Type);
                Assert.AreEqual("Camera", camera.Node.Name);
                foreach (var slotName in new[] { "Position", "Direction", "Orthographic", "Near Plane", "Far Plane", "Z Buffer Sign", "Width", "Height" })
                {
                    Assert.IsTrue(camera.Node.Slots!.Any(slot => slot.DisplayName == slotName),
                        $"Expected Camera node to expose slot '{slotName}'.");
                }

                Assert.AreEqual("UnityEditor.ShaderGraph.ExponentialNode", exponential.Node!.Type);
                Assert.AreEqual("Exponential", exponential.Node.Name);
                Assert.IsTrue(exponential.Node.Slots!.Any(slot => slot.DisplayName == "In"));
                Assert.IsTrue(exponential.Node.Slots.Any(slot => slot.DisplayName == "Out"));
                Assert.AreEqual("baseE", exponential.Node.Exponential!.Base);

                var duplicateResult = tool.DuplicateNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphDuplicateNodeInput
                    {
                        NodeObjectId = exponential.Node.ObjectId,
                        PositionOffsetX = 80f,
                        PositionOffsetY = 40f
                    },
                    includeStructure: true);
                Assert.AreEqual("UnityEditor.ShaderGraph.ExponentialNode", duplicateResult.Node!.Type);
                Assert.AreNotEqual(exponential.Node.ObjectId, duplicateResult.Node.ObjectId);

                var moveResult = tool.UpdateNodePosition(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodePositionInput
                    {
                        NodeObjectId = duplicateResult.Node.ObjectId,
                        PositionX = -300f,
                        PositionY = 260f
                    });
                Assert.AreEqual(-300f, moveResult.Node!.PositionX);
                Assert.AreEqual(260f, moveResult.Node.PositionY);

                var deleteResult = tool.DeleteNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphDeleteNodeInput { NodeObjectId = duplicateResult.Node.ObjectId },
                    includeStructure: true);
                Assert.IsFalse(deleteResult.Structure!.Nodes!.Any(node => node.ObjectId == duplicateResult.Node.ObjectId),
                    "Deleted Exponential duplicate should not remain in the graph.");

                Assert.IsNotNull(exponential.Graph);
                Assert.IsTrue(exponential.Graph!.ShaderResolved, "Adding WorldSpaceDepthFade nodes should keep the Shader Graph import valid.");
                Assert.IsFalse(exponential.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Adding WorldSpaceDepthFade nodes should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateNodeSettings_UpdatesWorldSpaceDepthFadeSettings()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodeSettings_WorldSpaceDepthFade.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var exponential = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "exponential", PositionX = -620f, PositionY = 100f });

                var base2Result = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = exponential.Node!.ObjectId,
                        Exponential = new ShaderGraphExponentialNodeSettingsUpdateInput
                        {
                            Base = "base2",
                            Input = new ShaderGraphVector4ValueUpdateInput { X = -0.5f, Y = 0f, Z = 0f, W = 0f }
                        }
                    });
                Assert.AreEqual("base2", base2Result.Node!.Exponential!.Base);
                Assert.AreEqual(-0.5f, base2Result.Node.Exponential.Input!.X ?? 0f, 0.0001f);
                AssertSlotFloat(base2Result.Node, "In", -0.5f);

                var baseEResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = exponential.Node.ObjectId,
                        Exponential = new ShaderGraphExponentialNodeSettingsUpdateInput { Base = "baseE" }
                    },
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);
                Assert.AreEqual("baseE", baseEResult.Node!.Exponential!.Base);

                var unsupportedBase = Assert.Throws<ArgumentException>(() => tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = exponential.Node.ObjectId,
                        Exponential = new ShaderGraphExponentialNodeSettingsUpdateInput { Base = "base10" }
                    }));
                StringAssert.Contains("Supported values: baseE, base2", unsupportedBase!.Message);

                Assert.IsNotNull(baseEResult.Graph);
                Assert.IsTrue(baseEResult.Graph!.ShaderResolved, "Updating Exponential settings should keep the Shader Graph import valid.");
                Assert.IsFalse(baseEResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Updating Exponential settings should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddNode_AddsReciprocalNode()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddNode_Reciprocal.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var reciprocal = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "reciprocal", PositionX = -620f, PositionY = 100f },
                    includeStructure: true);

                Assert.AreEqual("UnityEditor.ShaderGraph.ReciprocalNode", reciprocal.Node!.Type);
                Assert.AreEqual("Reciprocal", reciprocal.Node.Name);
                Assert.IsTrue(reciprocal.Node.Slots!.Any(slot => slot.DisplayName == "In"));
                Assert.IsTrue(reciprocal.Node.Slots.Any(slot => slot.DisplayName == "Out"));
                Assert.AreEqual("default", reciprocal.Node.Reciprocal!.Method);

                var fastResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = reciprocal.Node.ObjectId,
                        Reciprocal = new ShaderGraphReciprocalNodeSettingsUpdateInput
                        {
                            Method = "fast",
                            Input = new ShaderGraphVector4ValueUpdateInput { X = 2f, Y = 0f, Z = 0f, W = 0f }
                        }
                    });
                Assert.AreEqual("fast", fastResult.Node!.Reciprocal!.Method);
                Assert.AreEqual(2f, fastResult.Node.Reciprocal.Input!.X ?? 0f, 0.0001f);
                AssertSlotFloat(fastResult.Node, "In", 2f);

                var defaultResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = reciprocal.Node.ObjectId,
                        Reciprocal = new ShaderGraphReciprocalNodeSettingsUpdateInput { Method = "default" }
                    },
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);
                Assert.AreEqual("default", defaultResult.Node!.Reciprocal!.Method);

                var unsupportedMethod = Assert.Throws<ArgumentException>(() => tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = reciprocal.Node.ObjectId,
                        Reciprocal = new ShaderGraphReciprocalNodeSettingsUpdateInput { Method = "precise" }
                    }));
                StringAssert.Contains("Supported values: default, fast", unsupportedMethod!.Message);

                var divide = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "divide", PositionX = -900f, PositionY = 100f },
                    includeStructure: true);
                var divideOut = divide.Node!.Slots!.First(s => s.DisplayName == "Out");
                var reciprocalIn = reciprocal.Node.Slots!.First(s => s.DisplayName == "In");

                var edgeResult = tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = divide.Node.ObjectId,
                        OutputSlotObjectId = divideOut.ObjectId,
                        InputNodeObjectId = reciprocal.Node.ObjectId,
                        InputSlotObjectId = reciprocalIn.ObjectId,
                        ReplaceExistingInputConnection = true
                    },
                    includeGraph: true);

                Assert.IsNotNull(edgeResult.Graph);
                Assert.IsTrue(edgeResult.Graph!.ShaderResolved, "Connecting an edge to Reciprocal node should keep the Shader Graph import valid.");
                Assert.IsFalse(edgeResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Connecting an edge to Reciprocal should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void SubGraph_Phase1_FullFlow_CreateMutateAndReferenceFromMainGraph()
        {
            var subGraphPath = $"{TestFolder}/Validation_SubGraph_Phase1.shadersubgraph";
            var mainGraphPath = CreateShaderGraphAssetCopy("Validation_SubGraph_Phase1_Main.shadergraph");
            try
            {
                var tool = new Tool_Assets_ShaderGraph();

                // --- Create sub-graph ---
                var createResult = tool.CreateSubGraph(subGraphPath);
                Assert.IsNotNull(createResult, "CreateSubGraph should return ShaderGraphData.");
                Assert.IsTrue(createResult.IsSubGraph, "Created asset should be flagged as a sub-graph.");
                Assert.IsFalse(createResult.ShaderResolved, "Sub-graphs do not produce a Shader, so ShaderResolved should be false.");
                Assert.IsFalse(createResult.HasErrors, "Freshly created sub-graph should not have errors.");

                // --- Get structure of the blank sub-graph ---
                var subGraphRef = new AssetObjectRef(subGraphPath);
                var structure = tool.GetStructure(subGraphRef);
                Assert.IsNotNull(structure, "GetStructure should return data for a sub-graph.");
                Assert.IsTrue(structure.Nodes!.Any(n => n.Type == "UnityEditor.ShaderGraph.SubGraphOutputNode"),
                    "Blank sub-graph should contain a SubGraphOutputNode.");

                // --- Add 3 nodes to the sub-graph ---
                var addNode = tool.AddNode(subGraphRef,
                    new ShaderGraphAddNodeInput { NodeType = "add", PositionX = -400f, PositionY = 0f },
                    includeStructure: true);
                Assert.AreEqual("UnityEditor.ShaderGraph.AddNode", addNode.Node!.Type);

                var multiplyNode = tool.AddNode(subGraphRef,
                    new ShaderGraphAddNodeInput { NodeType = "multiply", PositionX = -200f, PositionY = 0f },
                    includeStructure: true);
                Assert.AreEqual("UnityEditor.ShaderGraph.MultiplyNode", multiplyNode.Node!.Type);

                var saturateNode = tool.AddNode(subGraphRef,
                    new ShaderGraphAddNodeInput { NodeType = "saturate", PositionX = 0f, PositionY = 0f },
                    includeStructure: true);
                Assert.AreEqual("UnityEditor.ShaderGraph.SaturateNode", saturateNode.Node!.Type);

                // --- Connect 2 edges: Add→Multiply, Multiply→Saturate ---
                var addOut = addNode.Node.Slots!.First(s => s.DisplayName == "Out");
                var multiplyInA = multiplyNode.Node.Slots!.First(s => s.DisplayName == "A");
                tool.ConnectEdge(subGraphRef, new ShaderGraphConnectEdgeInput
                {
                    OutputNodeObjectId = addNode.Node.ObjectId,
                    OutputSlotObjectId = addOut.ObjectId,
                    InputNodeObjectId = multiplyNode.Node.ObjectId,
                    InputSlotObjectId = multiplyInA.ObjectId
                });

                var multiplyOut = multiplyNode.Node.Slots!.First(s => s.DisplayName == "Out");
                var saturateIn = saturateNode.Node.Slots!.First(s => s.DisplayName == "In");
                tool.ConnectEdge(subGraphRef, new ShaderGraphConnectEdgeInput
                {
                    OutputNodeObjectId = multiplyNode.Node.ObjectId,
                    OutputSlotObjectId = multiplyOut.ObjectId,
                    InputNodeObjectId = saturateNode.Node.ObjectId,
                    InputSlotObjectId = saturateIn.ObjectId
                });

                // --- Add 1 property to the sub-graph ---
                var prop = tool.AddProperty(subGraphRef, new ShaderGraphAddPropertyInput
                {
                    PropertyType = "float",
                    DisplayName = "Intensity",
                    OverrideReferenceName = "_Intensity"
                });
                Assert.IsNotNull(prop.Property, "AddProperty should return the created property.");

                // --- Verify sub-graph structure after mutations ---
                var mutatedStructure = tool.GetStructure(subGraphRef);
                Assert.IsTrue(mutatedStructure.Nodes!.Count >= 4, "Sub-graph should have at least 4 nodes (output + 3 added).");
                Assert.IsTrue(mutatedStructure.Edges!.Count >= 2, "Sub-graph should have at least 2 edges.");
                Assert.IsTrue(mutatedStructure.Properties!.Any(p => p.Name == "Intensity"),
                    "Sub-graph should contain the Intensity property.");

                // --- Verify set-blocks is rejected on sub-graphs ---
                var setBlocksEx = Assert.Throws<InvalidOperationException>(() =>
                    tool.SetBlocks(subGraphRef, new ShaderGraphSetBlocksInput
                    {
                        Context = "fragment",
                        Blocks = new List<string> { "baseColor" }
                    }));
                StringAssert.Contains("Sub Graphs do not use the master block stack", setBlocksEx!.Message);

                // --- Reference the sub-graph from a main graph ---
                var mainShader = AssetDatabase.LoadAssetAtPath<Shader>(mainGraphPath);
                Assert.IsNotNull(mainShader, "Main graph shader should resolve.");
                var mainRef = new AssetObjectRef(mainShader);

                var subGraphNode = tool.AddNode(mainRef,
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "subGraph",
                        SubGraphAssetPath = subGraphPath,
                        PositionX = -300f,
                        PositionY = 200f
                    },
                    includeStructure: true,
                    includeGraph: true);

                Assert.AreEqual("UnityEditor.ShaderGraph.SubGraphNode", subGraphNode.Node!.Type);
                Assert.IsNotNull(subGraphNode.Graph, "Main graph data should be returned.");
                Assert.IsTrue(subGraphNode.Graph!.ShaderResolved,
                    "Main graph should still resolve a Shader after adding a SubGraphNode.");
                Assert.IsFalse(subGraphNode.Graph.HasErrors,
                    "Main graph should not have errors after adding a SubGraphNode referencing the sub-graph.");
            }
            finally
            {
                CleanupTestAsset(subGraphPath);
                CleanupTestAsset(mainGraphPath);
            }
        }

        [Test]
        public void SubGraph_Phase2_SetOutputs_SetsAndReconciles()
        {
            var subGraphPath = $"{TestFolder}/Validation_SubGraph_Phase2_SetOutputs.shadersubgraph";
            var mainGraphPath = CreateShaderGraphAssetCopy("Validation_SubGraph_Phase2_Main.shadergraph");
            try
            {
                var tool = new Tool_Assets_ShaderGraph();

                // Create sub-graph with empty preset (no output slots)
                var createResult = tool.CreateSubGraph(subGraphPath, outputPreset: "empty");
                Assert.IsNotNull(createResult);
                Assert.IsTrue(createResult.IsSubGraph);

                // --- First setOutputs: declare Tint (Color) + Mask (Float) ---
                var subGraphRef = new AssetObjectRef(subGraphPath);
                var setResult1 = tool.SetOutputs(subGraphRef,
                    new ShaderGraphSetSubGraphOutputsInput
                    {
                        Outputs = new List<ShaderGraphSubGraphOutputSlotInput>
                        {
                            new() { Name = "Tint", Type = "Color", X = 1, Y = 1, Z = 1, W = 1 },
                            new() { Name = "Mask", Type = "Float", FloatValue = 1.0f }
                        },
                        RemoveMissing = true
                    },
                    includeStructure: true);

                Assert.AreEqual("setOutputs", setResult1.Operation);
                Assert.AreEqual(2, setResult1.OutputResults!.Count);
                Assert.AreEqual("added", setResult1.OutputResults[0].Action);
                Assert.AreEqual("Tint", setResult1.OutputResults[0].Name);
                Assert.AreEqual("added", setResult1.OutputResults[1].Action);
                Assert.AreEqual("Mask", setResult1.OutputResults[1].Name);

                var tintSlotId = setResult1.OutputResults[0].SlotId;
                var maskSlotId = setResult1.OutputResults[1].SlotId;

                // --- Second setOutputs: keep Mask (preserved), change Tint to Vector3 (replaced), add WorldOffset (new) ---
                var setResult2 = tool.SetOutputs(subGraphRef,
                    new ShaderGraphSetSubGraphOutputsInput
                    {
                        Outputs = new List<ShaderGraphSubGraphOutputSlotInput>
                        {
                            new() { Name = "Mask", Type = "Float" },
                            new() { Name = "Tint", Type = "Vector3" },
                            new() { Name = "WorldOffset", Type = "Vector3", X = 0, Y = 1, Z = 0 }
                        },
                        RemoveMissing = true
                    },
                    includeStructure: true);

                Assert.AreEqual(3, setResult2.OutputResults!.Count);

                var maskResult = setResult2.OutputResults.First(r => r.Name == "Mask");
                Assert.AreEqual("kept", maskResult.Action);
                Assert.AreEqual(maskSlotId, maskResult.SlotId, "Unchanged slot ID should survive reconciliation.");

                var tintResult = setResult2.OutputResults.First(r => r.Name == "Tint");
                Assert.AreEqual("replaced", tintResult.Action);

                var offsetResult = setResult2.OutputResults.First(r => r.Name == "WorldOffset");
                Assert.AreEqual("added", offsetResult.Action);

                // --- Reference the mutated sub-graph from a main graph and verify it compiles ---
                var mainShader = AssetDatabase.LoadAssetAtPath<Shader>(mainGraphPath);
                Assert.IsNotNull(mainShader, "Main graph shader should resolve.");
                var mainRef = new AssetObjectRef(mainShader);

                var subGraphNode = tool.AddNode(mainRef,
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "subGraph",
                        SubGraphAssetPath = subGraphPath,
                        PositionX = -300f,
                        PositionY = 200f
                    },
                    includeStructure: true,
                    includeGraph: true);

                Assert.AreEqual("UnityEditor.ShaderGraph.SubGraphNode", subGraphNode.Node!.Type);
                Assert.IsTrue(subGraphNode.Graph!.ShaderResolved,
                    "Main graph should compile after adding a SubGraphNode referencing the mutated sub-graph.");
            }
            finally
            {
                CleanupTestAsset(subGraphPath);
                CleanupTestAsset(mainGraphPath);
            }
        }

        [Test]
        public void SubGraph_Phase2_SetOutputs_RejectsShaderGraph()
        {
            var mainGraphPath = CreateShaderGraphAssetCopy("Validation_SubGraph_Phase2_RejectMain.shadergraph");
            try
            {
                var tool = new Tool_Assets_ShaderGraph();
                var mainShader = AssetDatabase.LoadAssetAtPath<Shader>(mainGraphPath);
                Assert.IsNotNull(mainShader);
                var mainRef = new AssetObjectRef(mainShader);

                var ex = Assert.Throws<InvalidOperationException>(() =>
                    tool.SetOutputs(mainRef, new ShaderGraphSetSubGraphOutputsInput
                    {
                        Outputs = new List<ShaderGraphSubGraphOutputSlotInput>
                        {
                            new() { Name = "Out", Type = "Float" }
                        }
                    }));
                StringAssert.Contains("Sub Graph outputs are only mutable on .shadersubgraph assets", ex!.Message);
            }
            finally
            {
                CleanupTestAsset(mainGraphPath);
            }
        }

        [Test]
        public void SubGraph_Phase2_SetOutputs_RejectsUnsupportedTypes()
        {
            var subGraphPath = $"{TestFolder}/Validation_SubGraph_Phase2_UnsupportedType.shadersubgraph";
            try
            {
                var tool = new Tool_Assets_ShaderGraph();
                tool.CreateSubGraph(subGraphPath, outputPreset: "empty");

                var subGraphRef = new AssetObjectRef(subGraphPath);
                var ex = Assert.Throws<ArgumentException>(() =>
                    tool.SetOutputs(subGraphRef, new ShaderGraphSetSubGraphOutputsInput
                    {
                        Outputs = new List<ShaderGraphSubGraphOutputSlotInput>
                        {
                            new() { Name = "Tex", Type = "Texture2D" }
                        }
                    }));
                StringAssert.Contains("Phase 3", ex!.Message);
            }
            finally
            {
                CleanupTestAsset(subGraphPath);
            }
        }

        [Test]
        public void SubGraph_Phase2_NestedSubGraphs_CompileEndToEnd()
        {
            var innerPath = $"{TestFolder}/Validation_SubGraph_Phase2_Inner.shadersubgraph";
            var outerPath = $"{TestFolder}/Validation_SubGraph_Phase2_Outer.shadersubgraph";
            var mainGraphPath = CreateShaderGraphAssetCopy("Validation_SubGraph_Phase2_NestedMain.shadergraph");
            try
            {
                var tool = new Tool_Assets_ShaderGraph();

                // Create inner sub-graph with one Float output
                tool.CreateSubGraph(innerPath, outputPreset: "single-float");
                var innerRef = new AssetObjectRef(innerPath);

                // Add an Add node and connect to the output
                var addNode = tool.AddNode(innerRef,
                    new ShaderGraphAddNodeInput { NodeType = "add", PositionX = -200f, PositionY = 0f },
                    includeStructure: true);

                var innerStructure = tool.GetStructure(innerRef);
                var outputNode = innerStructure.Nodes!.First(n => n.Type == "UnityEditor.ShaderGraph.SubGraphOutputNode");
                var outputSlot = outputNode.Slots!.First();
                var addOut = addNode.Node!.Slots!.First(s => s.DisplayName == "Out");

                tool.ConnectEdge(innerRef, new ShaderGraphConnectEdgeInput
                {
                    OutputNodeObjectId = addNode.Node.ObjectId,
                    OutputSlotObjectId = addOut.ObjectId,
                    InputNodeObjectId = outputNode.ObjectId,
                    InputSlotObjectId = outputSlot.ObjectId
                });

                // Create outer sub-graph with one Float output
                tool.CreateSubGraph(outerPath, outputPreset: "single-float");
                var outerRef = new AssetObjectRef(outerPath);

                // Add a SubGraphNode referencing inner
                var innerNode = tool.AddNode(outerRef,
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "subGraph",
                        SubGraphAssetPath = innerPath,
                        PositionX = -200f,
                        PositionY = 0f
                    },
                    includeStructure: true);

                // Connect inner's output to outer's output node
                var outerStructure = tool.GetStructure(outerRef);
                var outerOutputNode = outerStructure.Nodes!.First(n => n.Type == "UnityEditor.ShaderGraph.SubGraphOutputNode");
                var outerOutputSlot = outerOutputNode.Slots!.First();
                var innerOutSlot = innerNode.Node!.Slots!.First(s => s.DisplayName == "Out");

                tool.ConnectEdge(outerRef, new ShaderGraphConnectEdgeInput
                {
                    OutputNodeObjectId = innerNode.Node.ObjectId,
                    OutputSlotObjectId = innerOutSlot.ObjectId,
                    InputNodeObjectId = outerOutputNode.ObjectId,
                    InputSlotObjectId = outerOutputSlot.ObjectId
                });

                // Reference outer from main graph
                var mainShader = AssetDatabase.LoadAssetAtPath<Shader>(mainGraphPath);
                Assert.IsNotNull(mainShader, "Main graph shader should resolve.");
                var mainRef = new AssetObjectRef(mainShader);

                var outerSubGraphNode = tool.AddNode(mainRef,
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "subGraph",
                        SubGraphAssetPath = outerPath,
                        PositionX = -300f,
                        PositionY = 200f
                    },
                    includeStructure: true,
                    includeGraph: true);

                Assert.AreEqual("UnityEditor.ShaderGraph.SubGraphNode", outerSubGraphNode.Node!.Type);
                Assert.IsTrue(outerSubGraphNode.Graph!.ShaderResolved,
                    "Main graph should compile with a nested sub-graph chain (inner → outer → main).");
                Assert.IsFalse(outerSubGraphNode.Graph.HasErrors,
                    "Main graph should have no errors with a nested sub-graph chain.");
            }
            finally
            {
                CleanupTestAsset(innerPath);
                CleanupTestAsset(outerPath);
                CleanupTestAsset(mainGraphPath);
            }
        }

        [Test]
        public void SubGraph_Phase2_CreateSubGraph_OutputPresets()
        {
            var singleColorPath = $"{TestFolder}/Validation_Preset_SingleColor.shadersubgraph";
            var singleFloatPath = $"{TestFolder}/Validation_Preset_SingleFloat.shadersubgraph";
            var singleVector3Path = $"{TestFolder}/Validation_Preset_SingleVector3.shadersubgraph";
            var emptyPath = $"{TestFolder}/Validation_Preset_Empty.shadersubgraph";
            try
            {
                var tool = new Tool_Assets_ShaderGraph();

                // single-color preset
                var colorResult = tool.CreateSubGraph(singleColorPath, outputPreset: "single-color");
                Assert.IsNotNull(colorResult);
                Assert.IsTrue(colorResult.IsSubGraph);
                var colorStructure = tool.GetStructure(new AssetObjectRef(singleColorPath));
                var colorOutputNode = colorStructure.Nodes!.First(n => n.Type == "UnityEditor.ShaderGraph.SubGraphOutputNode");
                Assert.AreEqual(1, colorOutputNode.Slots!.Count, "single-color preset should have 1 output slot.");

                // single-float preset
                var floatResult = tool.CreateSubGraph(singleFloatPath, outputPreset: "single-float");
                Assert.IsNotNull(floatResult);
                var floatStructure = tool.GetStructure(new AssetObjectRef(singleFloatPath));
                var floatOutputNode = floatStructure.Nodes!.First(n => n.Type == "UnityEditor.ShaderGraph.SubGraphOutputNode");
                Assert.AreEqual(1, floatOutputNode.Slots!.Count, "single-float preset should have 1 output slot.");

                // single-vector3 preset
                var vec3Result = tool.CreateSubGraph(singleVector3Path, outputPreset: "single-vector3");
                Assert.IsNotNull(vec3Result);
                var vec3Structure = tool.GetStructure(new AssetObjectRef(singleVector3Path));
                var vec3OutputNode = vec3Structure.Nodes!.First(n => n.Type == "UnityEditor.ShaderGraph.SubGraphOutputNode");
                Assert.AreEqual(1, vec3OutputNode.Slots!.Count, "single-vector3 preset should have 1 output slot.");

                // empty preset
                var emptyResult = tool.CreateSubGraph(emptyPath, outputPreset: "empty");
                Assert.IsNotNull(emptyResult);
                var emptyStructure = tool.GetStructure(new AssetObjectRef(emptyPath));
                var emptyOutputNode = emptyStructure.Nodes!.First(n => n.Type == "UnityEditor.ShaderGraph.SubGraphOutputNode");
                Assert.AreEqual(0, emptyOutputNode.Slots!.Count, "empty preset should have 0 output slots.");
            }
            finally
            {
                CleanupTestAsset(singleColorPath);
                CleanupTestAsset(singleFloatPath);
                CleanupTestAsset(singleVector3Path);
                CleanupTestAsset(emptyPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateNodeSettings_UpdatesDissolveTrialSettings()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodeSettings_DissolveTrial.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var invertColors = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "invertColors", PositionX = -820f, PositionY = 260f });

                var result = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = invertColors.Node!.ObjectId,
                        InvertColors = new ShaderGraphInvertColorsNodeSettingsUpdateInput
                        {
                            Red = true,
                            Green = false,
                            Blue = false
                        }
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsTrue(result.ChangedFields!.Contains("node.invertColors.red"));
                Assert.IsNotNull(result.Node!.InvertColors);
                Assert.IsTrue(result.Node.InvertColors!.Red ?? false, "Expected Red channel inversion to be enabled.");
                Assert.IsFalse(result.Node.InvertColors.Green ?? true, "Expected Green channel inversion to be disabled.");
                Assert.IsFalse(result.Node.InvertColors.Blue ?? true, "Expected Blue channel inversion to be disabled.");
                Assert.IsNull(result.Node.InvertColors.Alpha,
                    "Unity's current InvertColorsNode does not serialize m_AlphaChannel, so alpha readback should stay unavailable.");

                var alphaException = Assert.Throws<ArgumentException>(() => tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = invertColors.Node.ObjectId,
                        InvertColors = new ShaderGraphInvertColorsNodeSettingsUpdateInput
                        {
                            Alpha = true
                        }
                    }));
                StringAssert.Contains("invertColors.alpha is not safely writable", alphaException!.Message);

                Assert.IsNotNull(result.Graph);
                Assert.IsTrue(result.Graph!.ShaderResolved, "Updating dissolve-trial node settings should keep the Shader Graph import valid.");
                Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Updating dissolve-trial node settings should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateNodeSettings_UpdatesMinionsArtWaterBehaviorSettings()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodeSettings_MinionsArtWaterBehavior.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var comparison = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "comparison", PositionX = -1100f, PositionY = 0f });
                var normalFromHeight = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "normalFromHeight", PositionX = -820f, PositionY = 0f });
                var blend = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "blend", PositionX = -520f, PositionY = 0f });
                var swizzle = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "swizzle", PositionX = -260f, PositionY = 0f });

                var comparisonResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = comparison.Node!.ObjectId,
                        Comparison = new ShaderGraphComparisonNodeSettingsUpdateInput { ComparisonType = "greaterOrEqual" }
                    });
                var normalFromHeightResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = normalFromHeight.Node!.ObjectId,
                        NormalFromHeight = new ShaderGraphNormalFromHeightNodeSettingsUpdateInput { OutputSpace = "world" }
                    });
                var blendResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = blend.Node!.ObjectId,
                        Blend = new ShaderGraphBlendNodeSettingsUpdateInput { BlendMode = "screen" }
                    });
                var swizzleResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = swizzle.Node!.ObjectId,
                        Swizzle = new ShaderGraphSwizzleNodeSettingsUpdateInput { Mask = "xz" }
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual("greaterOrEqual", comparisonResult.Node!.Comparison!.ComparisonType);
                Assert.AreEqual("world", normalFromHeightResult.Node!.NormalFromHeight!.OutputSpace);
                Assert.AreEqual("screen", blendResult.Node!.Blend!.BlendMode);
                Assert.AreEqual("xz", swizzleResult.Node!.Swizzle!.Mask);
                Assert.AreEqual("xz", swizzleResult.Node.Swizzle.NormalizedMask);
                Assert.AreEqual("UnityEditor.ShaderGraph.Vector3MaterialSlot", FindSlot(swizzleResult.Node, "In").Type);
                Assert.AreEqual("UnityEditor.ShaderGraph.Vector2MaterialSlot", FindSlot(swizzleResult.Node, "Out").Type);

                var invalidSwizzleMask = Assert.Throws<ArgumentException>(() => tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = swizzle.Node.ObjectId,
                        Swizzle = new ShaderGraphSwizzleNodeSettingsUpdateInput { Mask = "xg" }
                    }));
                StringAssert.Contains("Mixed xyzw/rgba notation is not allowed", invalidSwizzleMask!.Message);

                Assert.IsNotNull(swizzleResult.Graph);
                Assert.IsTrue(swizzleResult.Graph!.ShaderResolved, "Updating the MinionsArt behavior node settings should keep the Shader Graph import valid.");
                Assert.IsFalse(swizzleResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Updating the MinionsArt behavior node settings should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ReflectionOutlineCorePath_CanBeWiredEndToEnd()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_ReflectionOutlinePath.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var position = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "position", PositionX = -1200f, PositionY = -160f });
                var normalVector = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "normalVector", PositionX = -1200f, PositionY = 20f });
                var viewDirection = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "viewDirection", PositionX = -1200f, PositionY = 200f });
                var viewVector = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "viewVector", PositionX = -1200f, PositionY = 380f });
                var viewVectorSplit = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "split", PositionX = -920f, PositionY = 380f });
                var viewVectorUv = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "combine", PositionX = -680f, PositionY = 520f });
                var sine = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "sine", PositionX = -920f, PositionY = 20f });
                var negate = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "negate", PositionX = -920f, PositionY = 200f });
                var cosine = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "cosine", PositionX = -680f, PositionY = 200f });
                var trigMultiply = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "multiply", PositionX = -460f, PositionY = 80f });
                var gradientNoise = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "gradientNoise", PositionX = -680f, PositionY = 380f });
                var displacementMultiply = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "multiply", PositionX = -220f, PositionY = 160f });
                var add = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "add", PositionX = 20f, PositionY = 20f });
                var transform = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "transform", PositionX = 260f, PositionY = 20f });

                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = position.Node!.ObjectId,
                        Position = new ShaderGraphPositionNodeSettingsUpdateInput { Space = "absoluteWorld" }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = transform.Node!.ObjectId,
                        Transform = new ShaderGraphTransformNodeSettingsUpdateInput
                        {
                            InputSpace = "absoluteWorld",
                            OutputSpace = "object",
                            TransformType = "position",
                            Normalize = false
                        }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = gradientNoise.Node!.ObjectId,
                        GradientNoise = new ShaderGraphGradientNoiseNodeSettingsUpdateInput { Scale = 18f }
                    });

                var structureBeforeWiring = tool.GetStructure(new AssetObjectRef(shader));
                var nodesById = structureBeforeWiring.Nodes!.ToDictionary(node => node.ObjectId);
                var positionNode = nodesById[position.Node!.ObjectId];
                var normalNode = nodesById[normalVector.Node!.ObjectId];
                var viewDirectionNode = nodesById[viewDirection.Node!.ObjectId];
                var viewVectorNode = nodesById[viewVector.Node!.ObjectId];
                var viewVectorSplitNode = nodesById[viewVectorSplit.Node!.ObjectId];
                var viewVectorUvNode = nodesById[viewVectorUv.Node!.ObjectId];
                var sineNode = nodesById[sine.Node!.ObjectId];
                var negateNode = nodesById[negate.Node!.ObjectId];
                var cosineNode = nodesById[cosine.Node!.ObjectId];
                var trigMultiplyNode = nodesById[trigMultiply.Node!.ObjectId];
                var gradientNoiseNode = nodesById[gradientNoise.Node!.ObjectId];
                var displacementMultiplyNode = nodesById[displacementMultiply.Node!.ObjectId];
                var addNode = nodesById[add.Node!.ObjectId];
                var transformNode = nodesById[transform.Node!.ObjectId];
                var vertexPositionBlock = structureBeforeWiring.Nodes!
                    .First(node => node.SerializedDescriptor == "VertexDescription.Position");

                var directViewVectorToUv = Assert.Throws<InvalidOperationException>(() =>
                    ConnectSlots(tool, shader, viewVectorNode, "Out", gradientNoiseNode, "UV"));
                StringAssert.Contains("Unsupported slot compatibility", directViewVectorToUv!.Message);

                ConnectSlots(tool, shader, normalNode, "Out", sineNode, "In");
                ConnectSlots(tool, shader, viewDirectionNode, "Out", negateNode, "In");
                ConnectSlots(tool, shader, negateNode, "Out", cosineNode, "In");
                ConnectSlots(tool, shader, viewVectorNode, "Out", viewVectorSplitNode, "In");
                ConnectSlots(tool, shader, viewVectorSplitNode, "R", viewVectorUvNode, "R");
                ConnectSlots(tool, shader, viewVectorSplitNode, "G", viewVectorUvNode, "G");
                ConnectSlots(tool, shader, viewVectorUvNode, "RG", gradientNoiseNode, "UV");
                ConnectSlots(tool, shader, sineNode, "Out", trigMultiplyNode, "A");
                ConnectSlots(tool, shader, cosineNode, "Out", trigMultiplyNode, "B");
                ConnectSlots(tool, shader, trigMultiplyNode, "Out", displacementMultiplyNode, "A");
                ConnectSlots(tool, shader, gradientNoiseNode, "Out", displacementMultiplyNode, "B");
                ConnectSlots(tool, shader, displacementMultiplyNode, "Out", addNode, "A");
                ConnectSlots(tool, shader, positionNode, "Out", addNode, "B");
                ConnectSlots(tool, shader, addNode, "Out", transformNode, "In");
                var finalConnectResult = ConnectSlots(tool, shader, transformNode, "Out", vertexPositionBlock, "Position", includeMessages: true, includeProperties: true);

                Assert.IsNotNull(finalConnectResult.Structure);
                Assert.IsTrue(finalConnectResult.Structure!.Edges!.Any(edge =>
                    edge.OutputNodeId == transformNode.ObjectId
                    && edge.OutputSlotId == FindSlot(transformNode, "Out").SlotId
                    && edge.InputNodeId == vertexPositionBlock.ObjectId
                    && edge.InputSlotId == FindSlot(vertexPositionBlock, "Position").SlotId));
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == viewVectorUvNode.ObjectId
                    && edge.OutputSlotId == FindSlot(viewVectorUvNode, "RG").SlotId
                    && edge.InputNodeId == gradientNoiseNode.ObjectId
                    && edge.InputSlotId == FindSlot(gradientNoiseNode, "UV").SlotId),
                    "Expected the narrowed View Vector UV path to feed Gradient Noise.UV.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == viewVectorNode.ObjectId
                    && edge.InputNodeId == viewVectorSplitNode.ObjectId),
                    "Expected View Vector to participate in the wired graph through the Split conversion node.");

                var sampleTextureNode = finalConnectResult.Structure.Nodes!
                    .First(node => node.Type == "UnityEditor.ShaderGraph.SampleTexture2DNode");
                var baseColorBlock = finalConnectResult.Structure.Nodes!
                    .First(node => node.SerializedDescriptor == "SurfaceDescription.BaseColor");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge => edge.InputNodeId == baseColorBlock.ObjectId),
                    "The template base texture/color path should remain wired into Base Color.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge => edge.OutputNodeId == sampleTextureNode.ObjectId),
                    "The template Sample Texture 2D node should remain wired into the base texture/color output path.");

                Assert.IsNotNull(finalConnectResult.Graph);
                Assert.IsTrue(finalConnectResult.Graph!.ShaderResolved, "Reflection-outline validation graph should resolve to a compiled Shader.");
                Assert.IsFalse(finalConnectResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Reflection-outline validation graph should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ObjectScaleOutlinePath_CanBeWiredEndToEnd()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_ObjectScaleOutlinePath.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var settingsResult = tool.SetSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphSettingsUpdateInput
                    {
                        UniversalTarget = new ShaderGraphUniversalTargetSettingsUpdateInput
                        {
                            SurfaceType = "opaque",
                            RenderFace = "back"
                        }
                    },
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(settingsResult.Settings);
                Assert.IsNotNull(settingsResult.Settings!.UniversalTarget);
                Assert.AreEqual("opaque", settingsResult.Settings.UniversalTarget!.SurfaceType);
                Assert.AreEqual("back", settingsResult.Settings.UniversalTarget.RenderFace);

                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Thickness",
                        OverrideReferenceName = "_Thickness",
                        FloatValue = 0.08f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "color",
                        DisplayName = "Outline Color",
                        OverrideReferenceName = "_OutlineColor",
                        ColorHex = "#00AAFFFF"
                    });

                var thicknessNode = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_Thickness",
                        PositionX = -1180f,
                        PositionY = 0f
                    });
                var outlineColorNode = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_OutlineColor",
                        PositionX = -480f,
                        PositionY = 360f
                    });
                var position = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "position", PositionX = -1180f, PositionY = 180f });
                var objectNode = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "object", PositionX = -1180f, PositionY = 360f });
                var divide = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "divide", PositionX = -860f, PositionY = 80f });
                var multiply = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "multiply", PositionX = -560f, PositionY = 120f });
                var add = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "add", PositionX = -260f, PositionY = 80f });

                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = position.Node!.ObjectId,
                        Position = new ShaderGraphPositionNodeSettingsUpdateInput { Space = "object" }
                    });

                var structureBeforeWiring = tool.GetStructure(new AssetObjectRef(shader));
                var nodesById = structureBeforeWiring.Nodes!.ToDictionary(node => node.ObjectId);
                var thicknessPropertyNode = nodesById[thicknessNode.Node!.ObjectId];
                var outlineColorPropertyNode = nodesById[outlineColorNode.Node!.ObjectId];
                var positionNode = nodesById[position.Node!.ObjectId];
                var objectNodeData = nodesById[objectNode.Node!.ObjectId];
                var divideNode = nodesById[divide.Node!.ObjectId];
                var multiplyNode = nodesById[multiply.Node!.ObjectId];
                var addNode = nodesById[add.Node!.ObjectId];
                var vertexPositionBlock = structureBeforeWiring.Nodes!
                    .First(node => node.SerializedDescriptor == "VertexDescription.Position");
                var baseColorBlock = structureBeforeWiring.Nodes!
                    .First(node => node.SerializedDescriptor == "SurfaceDescription.BaseColor");

                Assert.AreEqual("UnityEditor.ShaderGraph.ObjectNode", objectNodeData.Type);
                Assert.AreEqual("Object", objectNodeData.Name);
                Assert.IsTrue(objectNodeData.Slots!.Any(slot =>
                        slot.DisplayName == "Scale"
                        && slot.Type == "UnityEditor.ShaderGraph.Vector3MaterialSlot"),
                    "Expected Object node readback to expose a Vector3 Scale output slot.");

                ConnectSlots(tool, shader, thicknessPropertyNode, "Thickness", divideNode, "A");
                ConnectSlots(tool, shader, objectNodeData, "Scale", divideNode, "B");
                ConnectSlots(tool, shader, positionNode, "Out", multiplyNode, "A");
                ConnectSlots(tool, shader, divideNode, "Out", multiplyNode, "B");
                ConnectSlots(tool, shader, positionNode, "Out", addNode, "A");
                ConnectSlots(tool, shader, multiplyNode, "Out", addNode, "B");
                ConnectSlots(tool, shader, addNode, "Out", vertexPositionBlock, "Position");
                var finalConnectResult = ConnectSlots(
                    tool,
                    shader,
                    outlineColorPropertyNode,
                    "Outline Color",
                    baseColorBlock,
                    "Base Color",
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true,
                    replaceExistingInputConnection: true);

                Assert.IsNotNull(finalConnectResult.RemovedEdge,
                    "Replacing the template Base Color input should report the removed incoming edge.");
                Assert.IsNotNull(finalConnectResult.Structure);
                Assert.IsTrue(finalConnectResult.Structure!.Edges!.Any(edge =>
                    edge.OutputNodeId == objectNodeData.ObjectId
                    && edge.OutputSlotId == FindSlot(objectNodeData, "Scale").SlotId
                    && edge.InputNodeId == divideNode.ObjectId
                    && edge.InputSlotId == FindSlot(divideNode, "B").SlotId),
                    "Expected Object.Scale to feed Divide.B.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == addNode.ObjectId
                    && edge.OutputSlotId == FindSlot(addNode, "Out").SlotId
                    && edge.InputNodeId == vertexPositionBlock.ObjectId
                    && edge.InputSlotId == FindSlot(vertexPositionBlock, "Position").SlotId),
                    "Expected Add.Out to feed Vertex.Position.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == outlineColorPropertyNode.ObjectId
                    && edge.InputNodeId == baseColorBlock.ObjectId),
                    "Expected Outline Color to feed Fragment.Base Color.");

                Assert.IsNotNull(finalConnectResult.Graph);
                Assert.IsTrue(finalConnectResult.Graph!.ShaderResolved, "Object-scale outline validation graph should resolve to a compiled Shader.");
                Assert.IsFalse(finalConnectResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Object-scale outline validation graph should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_WaterCorePath_CanBeWiredEndToEnd()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_WaterCorePath.shadergraph", LitFullTemplateAssetPath);
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var blockResult = tool.SetBlocks(
                    new AssetObjectRef(shader),
                    new ShaderGraphSetBlocksInput
                    {
                        Context = "fragment",
                        Blocks = new()
                        {
                            "baseColor",
                            "normalTS",
                            "metallic",
                            "specular",
                            "smoothness",
                            "occlusion",
                            "emission",
                            "alpha",
                            "alphaClipThreshold",
                            "bentNormal"
                        }
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsTrue(blockResult.Graph!.ShaderResolved, "Lit template block setup should keep the Shader Graph import valid.");
                Assert.IsFalse(blockResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Lit template block setup should not introduce import errors.");

                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Water Depth Near",
                        OverrideReferenceName = "_WaterDepthNear",
                        FloatValue = 0.15f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Water Depth Far",
                        OverrideReferenceName = "_WaterDepthFar",
                        FloatValue = 1.35f
                    });

                var depthNearNode = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_WaterDepthNear",
                        PositionX = -1260f,
                        PositionY = 200f
                    });
                var depthFarNode = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_WaterDepthFar",
                        PositionX = -1260f,
                        PositionY = 320f
                    });

                var screenPosition = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "screenPosition", PositionX = -1260f, PositionY = -80f });
                var sceneDepth = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "sceneDepth", PositionX = -980f, PositionY = -80f });
                var smoothstep = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "smoothstep", PositionX = -700f, PositionY = 40f });
                var saturate = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "saturate", PositionX = -440f, PositionY = 40f });
                var time = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "time", PositionX = -700f, PositionY = 300f });
                var smoothnessMultiply = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "multiply", PositionX = -180f, PositionY = 240f });
                var vector2 = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "vector2", PositionX = -700f, PositionY = 520f });
                var sampleTexture = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "sampleTexture2D", PositionX = -380f, PositionY = 520f });

                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = screenPosition.Node!.ObjectId,
                        ScreenPosition = new ShaderGraphScreenPositionNodeSettingsUpdateInput { Mode = "raw" }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = sceneDepth.Node!.ObjectId,
                        SceneDepth = new ShaderGraphSceneDepthNodeSettingsUpdateInput { SamplingMode = "eye" }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = vector2.Node!.ObjectId,
                        Vector2 = new ShaderGraphVector2NodeSettingsUpdateInput
                        {
                            X = 0.25f,
                            Y = 0.75f
                        }
                    });

                var structureBeforeWiring = tool.GetStructure(new AssetObjectRef(shader));
                var nodesById = structureBeforeWiring.Nodes!.ToDictionary(node => node.ObjectId);
                var depthNearPropertyNode = nodesById[depthNearNode.Node!.ObjectId];
                var depthFarPropertyNode = nodesById[depthFarNode.Node!.ObjectId];
                var screenPositionNode = nodesById[screenPosition.Node!.ObjectId];
                var sceneDepthNode = nodesById[sceneDepth.Node!.ObjectId];
                var smoothstepNode = nodesById[smoothstep.Node!.ObjectId];
                var saturateNode = nodesById[saturate.Node!.ObjectId];
                var timeNode = nodesById[time.Node!.ObjectId];
                var smoothnessMultiplyNode = nodesById[smoothnessMultiply.Node!.ObjectId];
                var vector2Node = nodesById[vector2.Node!.ObjectId];
                var sampleTextureNode = nodesById[sampleTexture.Node!.ObjectId];
                var baseColorBlock = structureBeforeWiring.Nodes!
                    .First(node => node.SerializedDescriptor == "SurfaceDescription.BaseColor");
                var smoothnessBlock = structureBeforeWiring.Nodes!
                    .First(node => node.SerializedDescriptor == "SurfaceDescription.Smoothness");
                var alphaBlock = structureBeforeWiring.Nodes!
                    .First(node => node.SerializedDescriptor == "SurfaceDescription.Alpha");

                ConnectSlots(tool, shader, screenPositionNode, "Out", sceneDepthNode, "UV");
                ConnectSlots(tool, shader, depthNearPropertyNode, "Water Depth Near", smoothstepNode, "Edge1");
                ConnectSlots(tool, shader, depthFarPropertyNode, "Water Depth Far", smoothstepNode, "Edge2");
                ConnectSlots(tool, shader, sceneDepthNode, "Out", smoothstepNode, "In");
                ConnectSlots(tool, shader, smoothstepNode, "Out", saturateNode, "In");
                ConnectSlots(tool, shader, timeNode, "Sine Time", smoothnessMultiplyNode, "A");
                ConnectSlots(tool, shader, saturateNode, "Out", smoothnessMultiplyNode, "B");
                ConnectSlots(tool, shader, vector2Node, "Out", sampleTextureNode, "UV");
                ConnectSlots(
                    tool,
                    shader,
                    sampleTextureNode,
                    "RGBA",
                    baseColorBlock,
                    "Base Color",
                    replaceExistingInputConnection: true);
                ConnectSlots(
                    tool,
                    shader,
                    smoothnessMultiplyNode,
                    "Out",
                    smoothnessBlock,
                    "Smoothness",
                    replaceExistingInputConnection: true);
                var finalConnectResult = ConnectSlots(
                    tool,
                    shader,
                    saturateNode,
                    "Out",
                    alphaBlock,
                    "Alpha",
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true,
                    replaceExistingInputConnection: true);

                Assert.IsNotNull(finalConnectResult.Structure);
                Assert.IsTrue(finalConnectResult.Structure!.Edges!.Any(edge =>
                    edge.OutputNodeId == screenPositionNode.ObjectId
                    && edge.OutputSlotId == FindSlot(screenPositionNode, "Out").SlotId
                    && edge.InputNodeId == sceneDepthNode.ObjectId
                    && edge.InputSlotId == FindSlot(sceneDepthNode, "UV").SlotId),
                    "Expected Screen Position to feed Scene Depth UV.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == smoothnessMultiplyNode.ObjectId
                    && edge.InputNodeId == smoothnessBlock.ObjectId),
                    "Expected the water depth/time chain to drive Smoothness.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == saturateNode.ObjectId
                    && edge.InputNodeId == alphaBlock.ObjectId),
                    "Expected the saturated depth fade to drive Alpha.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == sampleTextureNode.ObjectId
                    && edge.InputNodeId == baseColorBlock.ObjectId),
                    "Expected the sampled texture path to drive Base Color.");

                Assert.IsNotNull(finalConnectResult.Graph);
                Assert.IsTrue(finalConnectResult.Graph!.ShaderResolved, "Water-core validation graph should resolve to a compiled Shader.");
                Assert.IsFalse(finalConnectResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Water-core validation graph should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_WorldSpaceDepthFadePath_CanBeWiredEndToEnd()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_WorldSpaceDepthFadePath.shadergraph", LitFullTemplateAssetPath);
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
                        DisplayName = "Depth Fade Distance",
                        OverrideReferenceName = "_DepthFadeDistance",
                        FloatValue = 1.5f
                    });

                var depthFadeDistanceNode = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_DepthFadeDistance",
                        PositionX = -520f,
                        PositionY = 520f
                    });

                var viewVector = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "viewVector", PositionX = -1540f, PositionY = -260f });
                var negateViewVector = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "negate", PositionX = -1280f, PositionY = -260f });
                var screenPosition = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "screenPosition", PositionX = -1540f, PositionY = -40f });
                var splitScreenPosition = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "split", PositionX = -1280f, PositionY = -40f });
                var viewDivide = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "divide", PositionX = -1020f, PositionY = -180f });
                var sceneDepth = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "sceneDepth", PositionX = -1020f, PositionY = 40f });
                var depthMultiply = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "multiply", PositionX = -760f, PositionY = -100f });
                var camera = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "camera", PositionX = -760f, PositionY = 160f });
                var sceneWorldPosition = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "add", PositionX = -500f, PositionY = -40f });
                var waterPosition = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "position", PositionX = -500f, PositionY = 260f });
                var depthSubtract = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "subtract", PositionX = -240f, PositionY = 80f });
                var splitDepth = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "split", PositionX = 20f, PositionY = 80f });
                var negateDepth = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "negate", PositionX = 280f, PositionY = 80f });
                var fadeDivide = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "divide", PositionX = 540f, PositionY = 160f });
                var exponential = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "exponential", PositionX = 800f, PositionY = 160f });
                var saturate = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "saturate", PositionX = 1060f, PositionY = 160f });

                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = viewVector.Node!.ObjectId,
                        ViewVector = new ShaderGraphSpaceNodeSettingsUpdateInput { Space = "world" }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = screenPosition.Node!.ObjectId,
                        ScreenPosition = new ShaderGraphScreenPositionNodeSettingsUpdateInput { Mode = "raw" }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = sceneDepth.Node!.ObjectId,
                        SceneDepth = new ShaderGraphSceneDepthNodeSettingsUpdateInput { SamplingMode = "eye" }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = waterPosition.Node!.ObjectId,
                        Position = new ShaderGraphPositionNodeSettingsUpdateInput { Space = "world" }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = exponential.Node!.ObjectId,
                        Exponential = new ShaderGraphExponentialNodeSettingsUpdateInput { Base = "baseE" }
                    });

                var structureBeforeWiring = tool.GetStructure(new AssetObjectRef(shader));
                var nodesById = structureBeforeWiring.Nodes!.ToDictionary(node => node.ObjectId);
                var depthFadeDistancePropertyNode = nodesById[depthFadeDistanceNode.Node!.ObjectId];
                var viewVectorNode = nodesById[viewVector.Node!.ObjectId];
                var negateViewVectorNode = nodesById[negateViewVector.Node!.ObjectId];
                var screenPositionNode = nodesById[screenPosition.Node!.ObjectId];
                var splitScreenPositionNode = nodesById[splitScreenPosition.Node!.ObjectId];
                var viewDivideNode = nodesById[viewDivide.Node!.ObjectId];
                var sceneDepthNode = nodesById[sceneDepth.Node!.ObjectId];
                var depthMultiplyNode = nodesById[depthMultiply.Node!.ObjectId];
                var cameraNode = nodesById[camera.Node!.ObjectId];
                var sceneWorldPositionNode = nodesById[sceneWorldPosition.Node!.ObjectId];
                var waterPositionNode = nodesById[waterPosition.Node!.ObjectId];
                var depthSubtractNode = nodesById[depthSubtract.Node!.ObjectId];
                var splitDepthNode = nodesById[splitDepth.Node!.ObjectId];
                var negateDepthNode = nodesById[negateDepth.Node!.ObjectId];
                var fadeDivideNode = nodesById[fadeDivide.Node!.ObjectId];
                var exponentialNode = nodesById[exponential.Node!.ObjectId];
                var saturateNode = nodesById[saturate.Node!.ObjectId];
                var alphaBlock = structureBeforeWiring.Nodes!
                    .First(node => node.SerializedDescriptor == "SurfaceDescription.Alpha");

                Assert.AreEqual("world", viewVectorNode.SourceVector!.Space);
                Assert.AreEqual("raw", screenPositionNode.ScreenPosition!.Mode);
                Assert.AreEqual("eye", sceneDepthNode.SceneDepth!.SamplingMode);
                Assert.AreEqual("world", waterPositionNode.Position!.Space);
                Assert.AreEqual("baseE", exponentialNode.Exponential!.Base);

                ConnectSlots(tool, shader, viewVectorNode, "Out", negateViewVectorNode, "In");
                ConnectSlots(tool, shader, negateViewVectorNode, "Out", viewDivideNode, "A");
                ConnectSlots(tool, shader, screenPositionNode, "Out", splitScreenPositionNode, "In");
                ConnectSlots(tool, shader, splitScreenPositionNode, "A", viewDivideNode, "B");
                ConnectSlots(tool, shader, screenPositionNode, "Out", sceneDepthNode, "UV");
                ConnectSlots(tool, shader, viewDivideNode, "Out", depthMultiplyNode, "A");
                ConnectSlots(tool, shader, sceneDepthNode, "Out", depthMultiplyNode, "B");
                ConnectSlots(tool, shader, depthMultiplyNode, "Out", sceneWorldPositionNode, "A");
                ConnectSlots(tool, shader, cameraNode, "Position", sceneWorldPositionNode, "B");
                ConnectSlots(tool, shader, waterPositionNode, "Out", depthSubtractNode, "A");
                ConnectSlots(tool, shader, sceneWorldPositionNode, "Out", depthSubtractNode, "B");
                ConnectSlots(tool, shader, depthSubtractNode, "Out", splitDepthNode, "In");
                ConnectSlots(tool, shader, splitDepthNode, "G", negateDepthNode, "In");
                ConnectSlots(tool, shader, negateDepthNode, "Out", fadeDivideNode, "A");
                ConnectSlots(tool, shader, depthFadeDistancePropertyNode, "Depth Fade Distance", fadeDivideNode, "B");
                ConnectSlots(tool, shader, fadeDivideNode, "Out", exponentialNode, "In");
                ConnectSlots(tool, shader, exponentialNode, "Out", saturateNode, "In");
                var finalConnectResult = ConnectSlots(
                    tool,
                    shader,
                    saturateNode,
                    "Out",
                    alphaBlock,
                    "Alpha",
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true,
                    replaceExistingInputConnection: true);

                Assert.IsNotNull(finalConnectResult.Structure);
                Assert.IsTrue(finalConnectResult.Structure!.Edges!.Any(edge =>
                    edge.OutputNodeId == screenPositionNode.ObjectId
                    && edge.OutputSlotId == FindSlot(screenPositionNode, "Out").SlotId
                    && edge.InputNodeId == sceneDepthNode.ObjectId
                    && edge.InputSlotId == FindSlot(sceneDepthNode, "UV").SlotId),
                    "Expected raw Screen Position to drive Scene Depth UV.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == cameraNode.ObjectId
                    && edge.OutputSlotId == FindSlot(cameraNode, "Position").SlotId
                    && edge.InputNodeId == sceneWorldPositionNode.ObjectId
                    && edge.InputSlotId == FindSlot(sceneWorldPositionNode, "B").SlotId),
                    "Expected Camera.Position to reconstruct world-space scene position.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == exponentialNode.ObjectId
                    && edge.InputNodeId == saturateNode.ObjectId),
                    "Expected Exponential(BaseE) to feed Saturate.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == saturateNode.ObjectId
                    && edge.InputNodeId == alphaBlock.ObjectId),
                    "Expected the saturated depth fade to drive Alpha.");

                Assert.IsNotNull(finalConnectResult.Graph);
                Assert.IsTrue(finalConnectResult.Graph!.ShaderResolved, "WorldSpaceDepthFade validation graph should resolve to a compiled Shader.");
                Assert.IsFalse(finalConnectResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "WorldSpaceDepthFade validation graph should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ConnectEdge_RejectsDynamicStepEdgeInput()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_DissolveStepEdgeReject.shadergraph", LitFullTemplateAssetPath);
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var add = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "add", PositionX = -740f, PositionY = 80f });
                var step = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "step", PositionX = -500f, PositionY = 80f });
                var structure = tool.GetStructure(new AssetObjectRef(shader));
                var addNode = structure.Nodes!.First(node => node.ObjectId == add.Node!.ObjectId);
                var stepNode = structure.Nodes!.First(node => node.ObjectId == step.Node!.ObjectId);

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    ConnectSlots(tool, shader!, addNode, "Out", stepNode, "Edge"));

                StringAssert.Contains("Step.Edge requires a literal compile-time value", exception!.Message);
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_BatchConnectEdge_RejectsDynamicStepEdgeInputAndRollsBack()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_DissolveStepEdgeBatchReject.shadergraph", LitFullTemplateAssetPath);
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var exception = Assert.Throws<InvalidOperationException>(() =>
                    tool.Batch(
                        new AssetObjectRef(shader),
                        new ShaderGraphBatchInput
                        {
                            Operations = new List<ShaderGraphBatchOperationInput>
                            {
                                new()
                                {
                                    Kind = "addNode",
                                    Alias = "add",
                                    AddNode = new ShaderGraphAddNodeInput { NodeType = "add", PositionX = -740f, PositionY = 80f }
                                },
                                new()
                                {
                                    Kind = "addNode",
                                    Alias = "step",
                                    AddNode = new ShaderGraphAddNodeInput { NodeType = "step", PositionX = -500f, PositionY = 80f }
                                },
                                new()
                                {
                                    Kind = "connectEdge",
                                    ConnectEdge = new ShaderGraphConnectEdgeInput
                                    {
                                        OutputSlot = new ShaderGraphSlotRef
                                        {
                                            Node = new ShaderGraphNodeRef { Alias = "add" },
                                            DisplayName = "Out"
                                        },
                                        InputSlot = new ShaderGraphSlotRef
                                        {
                                            Node = new ShaderGraphNodeRef { Alias = "step" },
                                            DisplayName = "Edge"
                                        }
                                    }
                                }
                            }
                        }));

                StringAssert.Contains("Step.Edge requires a literal compile-time value", exception!.Message);

                var structureAfterRollback = tool.GetStructure(new AssetObjectRef(shader));
                Assert.IsFalse(structureAfterRollback.Nodes!.Any(node =>
                    string.Equals(node.Type, "UnityEditor.ShaderGraph.StepNode", StringComparison.Ordinal)),
                    "The failed batch should roll the added Step node back out of the graph.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_BatchRollback_UsesCurrentDiskBytesAfterOverwriteCreate()
        {
            var assetPath = $"{TestFolder}/Validation_BatchRollback_DiskSnapshot.shadergraph";
            try
            {
                var tool = new Tool_Assets_ShaderGraph();
                var created = tool.Create(
                    assetPath: assetPath,
                    templateAssetPath: TemplateAssetPath,
                    overwrite: true);
                Assert.IsNotNull(created);

                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "color",
                        DisplayName = "Rollback Marker",
                        OverrideReferenceName = "_RollbackMarker",
                        ColorHex = "#FFFFFFFF"
                    });
                Assert.IsTrue(
                    File.ReadAllText(assetPath).Contains("Rollback Marker", StringComparison.Ordinal),
                    "The test setup should write a marker property before overwrite.");

                var staleShaderRef = new AssetObjectRef(shader);
                var overwritten = tool.Create(
                    assetPath: assetPath,
                    templateAssetPath: TemplateAssetPath,
                    overwrite: true);
                Assert.IsNotNull(overwritten);

                var cleanBytes = File.ReadAllBytes(assetPath);
                Assert.IsFalse(
                    File.ReadAllText(assetPath).Contains("Rollback Marker", StringComparison.Ordinal),
                    "Overwrite create should replace the marker graph with clean template disk content.");

                var exception = Assert.Throws<InvalidOperationException>(() =>
                    tool.Batch(
                        staleShaderRef,
                        new ShaderGraphBatchInput
                        {
                            Operations = new List<ShaderGraphBatchOperationInput>
                            {
                                new()
                                {
                                    Kind = "addProperty",
                                    AddProperty = new ShaderGraphAddPropertyInput
                                    {
                                        PropertyType = "color",
                                        DisplayName = "Base Color",
                                        OverrideReferenceName = "_BaseColor",
                                        ColorHex = "#FFFFFFFF"
                                    }
                                }
                            }
                        }));

                StringAssert.Contains("Asset rolled back to exact pre-batch disk content", exception!.Message);
                CollectionAssert.AreEqual(
                    cleanBytes,
                    File.ReadAllBytes(assetPath),
                    "A failed op[0] batch must restore the exact bytes that were on disk after overwrite create.");
                Assert.IsFalse(
                    File.ReadAllText(assetPath).Contains("Rollback Marker", StringComparison.Ordinal),
                    "Rollback must not restore stale graph content from an earlier MCP session.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_GetData_FlagsExistingDynamicStepEdgeInput()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_DissolveStepEdgeDiagnostics.shadergraph", LitFullTemplateAssetPath);
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var add = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "add", PositionX = -740f, PositionY = 80f });
                var step = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "step", PositionX = -500f, PositionY = 80f });
                var structure = tool.GetStructure(new AssetObjectRef(shader));
                var addNode = structure.Nodes!.First(node => node.ObjectId == add.Node!.ObjectId);
                var stepNode = structure.Nodes!.First(node => node.ObjectId == step.Node!.ObjectId);

                AddRawEdgeForTest(assetPath, addNode, "Out", stepNode, "Edge");

                var data = tool.GetData(new AssetObjectRef(shader), includeMessages: false, includeProperties: false, includeDiagnostics: true);

                Assert.IsTrue(data.HasErrors, "Serialized validation should mark the graph as erroneous even if ShaderUtil misses the import failure.");
                Assert.IsTrue(data.Diagnostics!.Any(d => d.Code == "SHADERGRAPH_LITERAL_SLOT_EDGE" && d.Severity == "Error"),
                    "Expected get-data diagnostics to report the dynamic edge into Step.Edge.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_DissolveTrialPath_CanCreateLiteralStepEdgeAndDynamicInputChain()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_DissolveTrialPath.shadergraph", LitFullTemplateAssetPath);
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var time = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "time", PositionX = -1220f, PositionY = 80f });
                var fraction = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "fraction", PositionX = -980f, PositionY = 80f });
                var add = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "add", PositionX = -740f, PositionY = 80f });
                var step = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "step", PositionX = -500f, PositionY = 80f });
                var invertColors = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "invertColors", PositionX = -260f, PositionY = 80f });

                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = add.Node!.ObjectId,
                        Add = new ShaderGraphBinaryVectorNodeSettingsUpdateInput
                        {
                            B = new ShaderGraphVector4ValueUpdateInput { X = 0.025f, Y = 0.025f, Z = 0.025f, W = 0.025f }
                        }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = invertColors.Node!.ObjectId,
                        InvertColors = new ShaderGraphInvertColorsNodeSettingsUpdateInput
                        {
                            Red = true,
                            Green = false,
                            Blue = false
                        }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = step.Node!.ObjectId,
                        Step = new ShaderGraphStepNodeSettingsUpdateInput
                        {
                            Edge = new ShaderGraphVector4ValueUpdateInput { X = 0.025f, Y = 0.025f, Z = 0.025f, W = 0.025f }
                        }
                    });

                var structureBeforeWiring = tool.GetStructure(new AssetObjectRef(shader));
                var nodesById = structureBeforeWiring.Nodes!.ToDictionary(node => node.ObjectId);
                var timeNode = nodesById[time.Node!.ObjectId];
                var fractionNode = nodesById[fraction.Node!.ObjectId];
                var addNode = nodesById[add.Node.ObjectId];
                var stepNode = nodesById[step.Node!.ObjectId];
                var invertColorsNode = nodesById[invertColors.Node.ObjectId];
                var emissionBlock = structureBeforeWiring.Nodes!
                    .First(node => node.SerializedDescriptor == "SurfaceDescription.Emission");

                ConnectSlots(tool, shader, timeNode, "Time", fractionNode, "In");
                ConnectSlots(tool, shader, fractionNode, "Out", addNode, "A");
                ConnectSlots(tool, shader, addNode, "Out", stepNode, "In");
                ConnectSlots(tool, shader, stepNode, "Out", invertColorsNode, "In");
                var finalConnectResult = ConnectSlots(
                    tool,
                    shader,
                    invertColorsNode,
                    "Out",
                    emissionBlock,
                    "Emission",
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true,
                    replaceExistingInputConnection: true);

                Assert.IsTrue(finalConnectResult.Structure!.Edges!.Any(edge =>
                    edge.OutputNodeId == timeNode.ObjectId
                    && edge.InputNodeId == fractionNode.ObjectId),
                    "Expected Time.Time to feed Fraction.In.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == fractionNode.ObjectId
                    && edge.InputNodeId == addNode.ObjectId),
                    "Expected Fraction.Out to feed Add.A.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == addNode.ObjectId
                    && edge.InputNodeId == stepNode.ObjectId),
                    "Expected Add.Out to feed Step.In while Step.Edge remains a literal threshold.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == stepNode.ObjectId
                    && edge.InputNodeId == invertColorsNode.ObjectId),
                    "Expected Step.Out to feed Invert Colors.In.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == invertColorsNode.ObjectId
                    && edge.InputNodeId == emissionBlock.ObjectId),
                    "Expected Invert Colors.Out to feed Fragment Emission.");

                Assert.IsNotNull(finalConnectResult.Graph);
                Assert.IsTrue(finalConnectResult.Graph!.ShaderResolved, "The dissolve-trial validation graph should resolve to a compiled Shader.");
                Assert.IsFalse(finalConnectResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "The dissolve-trial validation graph should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_MinionsArtWaterBehaviorNodes_CanBeWiredEndToEnd()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_MinionsArtWaterBehaviorPath.shadergraph", LitFullTemplateAssetPath);
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var blockResult = tool.SetBlocks(
                    new AssetObjectRef(shader),
                    new ShaderGraphSetBlocksInput
                    {
                        Context = "fragment",
                        Blocks = new()
                        {
                            "emission",
                            "alpha",
                            "normalWS"
                        }
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsTrue(blockResult.Graph!.ShaderResolved, "Fragment block setup should keep the Shader Graph import valid.");
                Assert.IsFalse(blockResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Fragment block setup should not introduce import errors.");

                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Blend Opacity",
                        OverrideReferenceName = "_BlendOpacity",
                        FloatValue = 0.35f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Height Value",
                        OverrideReferenceName = "_HeightValue",
                        FloatValue = 0.2f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Normal Strength",
                        OverrideReferenceName = "_NormalStrength",
                        FloatValue = 0.75f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Threshold A",
                        OverrideReferenceName = "_ThresholdA",
                        FloatValue = 0.1f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Threshold B",
                        OverrideReferenceName = "_ThresholdB",
                        FloatValue = 0.4f
                    });

                var blendOpacityProperty = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_BlendOpacity",
                        PositionX = -1220f,
                        PositionY = 400f
                    });
                var heightProperty = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_HeightValue",
                        PositionX = -1220f,
                        PositionY = 640f
                    });
                var normalStrengthProperty = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_NormalStrength",
                        PositionX = -1220f,
                        PositionY = 760f
                    });
                var thresholdAProperty = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_ThresholdA",
                        PositionX = -1220f,
                        PositionY = 1000f
                    });
                var thresholdBProperty = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_ThresholdB",
                        PositionX = -1220f,
                        PositionY = 1120f
                    });

                var screenPosition = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "screenPosition", PositionX = -1220f, PositionY = -40f });
                var sceneColor = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "sceneColor", PositionX = -940f, PositionY = -40f });
                var position = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "position", PositionX = -1220f, PositionY = 160f });
                var swizzle = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "swizzle", PositionX = -940f, PositionY = 160f });
                var remap = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "remap", PositionX = -660f, PositionY = 160f });
                var vector2 = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "vector2", PositionX = -940f, PositionY = 360f });
                var blend = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "blend", PositionX = -380f, PositionY = 40f });
                var normalFromHeight = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "normalFromHeight", PositionX = -660f, PositionY = 640f });
                var comparison = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "comparison", PositionX = -660f, PositionY = 1000f });
                var branch = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "branch", PositionX = -380f, PositionY = 980f });

                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = position.Node!.ObjectId,
                        Position = new ShaderGraphPositionNodeSettingsUpdateInput { Space = "world" }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = swizzle.Node!.ObjectId,
                        Swizzle = new ShaderGraphSwizzleNodeSettingsUpdateInput { Mask = "xz" }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = vector2.Node!.ObjectId,
                        Vector2 = new ShaderGraphVector2NodeSettingsUpdateInput
                        {
                            X = 0.05f,
                            Y = 0.85f
                        }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = blend.Node!.ObjectId,
                        Blend = new ShaderGraphBlendNodeSettingsUpdateInput { BlendMode = "screen" }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = normalFromHeight.Node!.ObjectId,
                        NormalFromHeight = new ShaderGraphNormalFromHeightNodeSettingsUpdateInput { OutputSpace = "world" }
                    });
                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = comparison.Node!.ObjectId,
                        Comparison = new ShaderGraphComparisonNodeSettingsUpdateInput { ComparisonType = "less" }
                    });

                var structureBeforeWiring = tool.GetStructure(new AssetObjectRef(shader));
                var nodesById = structureBeforeWiring.Nodes!.ToDictionary(node => node.ObjectId);
                var blendOpacityPropertyNode = nodesById[blendOpacityProperty.Node!.ObjectId];
                var heightPropertyNode = nodesById[heightProperty.Node!.ObjectId];
                var normalStrengthPropertyNode = nodesById[normalStrengthProperty.Node!.ObjectId];
                var thresholdAPropertyNode = nodesById[thresholdAProperty.Node!.ObjectId];
                var thresholdBPropertyNode = nodesById[thresholdBProperty.Node!.ObjectId];
                var screenPositionNode = nodesById[screenPosition.Node!.ObjectId];
                var sceneColorNode = nodesById[sceneColor.Node!.ObjectId];
                var positionNode = nodesById[position.Node!.ObjectId];
                var swizzleNode = nodesById[swizzle.Node!.ObjectId];
                var remapNode = nodesById[remap.Node!.ObjectId];
                var vector2Node = nodesById[vector2.Node!.ObjectId];
                var blendNode = nodesById[blend.Node!.ObjectId];
                var normalFromHeightNode = nodesById[normalFromHeight.Node!.ObjectId];
                var comparisonNode = nodesById[comparison.Node!.ObjectId];
                var branchNode = nodesById[branch.Node!.ObjectId];
                var emissionBlock = structureBeforeWiring.Nodes!
                    .First(node => node.SerializedDescriptor == "SurfaceDescription.Emission");
                var alphaBlock = structureBeforeWiring.Nodes!
                    .First(node => node.SerializedDescriptor == "SurfaceDescription.Alpha");
                var normalWsBlock = structureBeforeWiring.Nodes!
                    .First(node => node.SerializedDescriptor == "SurfaceDescription.NormalWS");

                ConnectSlots(tool, shader, screenPositionNode, "Out", sceneColorNode, "UV");
                ConnectSlots(tool, shader, positionNode, "Out", swizzleNode, "In");
                ConnectSlots(tool, shader, swizzleNode, "Out", remapNode, "In");
                ConnectSlots(tool, shader, vector2Node, "Out", remapNode, "Out Min Max");
                ConnectSlots(tool, shader, sceneColorNode, "Out", blendNode, "Base");
                ConnectSlots(tool, shader, remapNode, "Out", blendNode, "Blend");
                ConnectSlots(tool, shader, blendOpacityPropertyNode, "Blend Opacity", blendNode, "Opacity");
                ConnectSlots(tool, shader, heightPropertyNode, "Height Value", normalFromHeightNode, "In");
                ConnectSlots(tool, shader, normalStrengthPropertyNode, "Normal Strength", normalFromHeightNode, "Strength");
                ConnectSlots(tool, shader, thresholdAPropertyNode, "Threshold A", comparisonNode, "A");
                ConnectSlots(tool, shader, thresholdBPropertyNode, "Threshold B", comparisonNode, "B");
                ConnectSlots(tool, shader, comparisonNode, "Out", branchNode, "Predicate");
                ConnectSlots(tool, shader, thresholdAPropertyNode, "Threshold A", branchNode, "True");
                ConnectSlots(tool, shader, thresholdBPropertyNode, "Threshold B", branchNode, "False");
                ConnectSlots(tool, shader, blendNode, "Out", emissionBlock, "Emission", replaceExistingInputConnection: true);
                ConnectSlots(tool, shader, normalFromHeightNode, "Out", normalWsBlock, "Normal (World Space)", replaceExistingInputConnection: true);
                var finalConnectResult = ConnectSlots(
                    tool,
                    shader,
                    branchNode,
                    "Out",
                    alphaBlock,
                    "Alpha",
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true,
                    replaceExistingInputConnection: true);

                Assert.IsTrue(finalConnectResult.Structure!.Edges!.Any(edge =>
                    edge.OutputNodeId == screenPositionNode.ObjectId
                    && edge.InputNodeId == sceneColorNode.ObjectId),
                    "Expected Screen Position to feed Scene Color UV.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == swizzleNode.ObjectId
                    && edge.InputNodeId == remapNode.ObjectId),
                    "Expected the Swizzle output to drive Remap.In.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == blendNode.ObjectId
                    && edge.InputNodeId == emissionBlock.ObjectId),
                    "Expected Blend.Out to drive Emission.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == normalFromHeightNode.ObjectId
                    && edge.InputNodeId == normalWsBlock.ObjectId),
                    "Expected Normal From Height.Out to drive the world-space normal block.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == comparisonNode.ObjectId
                    && edge.InputNodeId == branchNode.ObjectId),
                    "Expected Comparison.Out to drive Branch.Predicate.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == branchNode.ObjectId
                    && edge.InputNodeId == alphaBlock.ObjectId),
                    "Expected Branch.Out to drive Alpha.");

                Assert.IsNotNull(finalConnectResult.Graph);
                Assert.IsTrue(finalConnectResult.Graph!.ShaderResolved, "The MinionsArt behavior-node validation graph should resolve to a compiled Shader.");
                Assert.IsFalse(finalConnectResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "The MinionsArt behavior-node validation graph should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_MinionsArtWaterScreenPositionInputs_AcceptDynamicVectorOutputs()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_MinionsArtWaterDynamicScreenPositionEdges.shadergraph", LitFullTemplateAssetPath);
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var branch = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "branch", PositionX = -1220f, PositionY = -40f });
                var subtract = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "subtract", PositionX = -1220f, PositionY = 260f });
                var sceneColor = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "sceneColor", PositionX = -880f, PositionY = -140f });
                var branchSceneDepth = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "sceneDepth", PositionX = -880f, PositionY = 80f });
                var subtractSceneDepth = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "sceneDepth", PositionX = -880f, PositionY = 300f });

                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = branch.Node!.ObjectId,
                        Branch = new ShaderGraphBranchNodeSettingsUpdateInput
                        {
                            Predicate = true,
                            TrueValue = new ShaderGraphVector4ValueUpdateInput { X = 0.15f, Y = 0.25f, Z = 0.35f, W = 1f },
                            FalseValue = new ShaderGraphVector4ValueUpdateInput { X = 0.45f, Y = 0.55f, Z = 0.65f, W = 1f }
                        }
                    });

                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = subtract.Node!.ObjectId,
                        Subtract = new ShaderGraphBinaryVectorNodeSettingsUpdateInput
                        {
                            A = new ShaderGraphVector4ValueUpdateInput { X = 0.9f, Y = 0.8f, Z = 0.7f, W = 1f },
                            B = new ShaderGraphVector4ValueUpdateInput { X = 0.1f, Y = 0.2f, Z = 0.3f, W = 0f }
                        }
                    });

                var structureBeforeWiring = tool.GetStructure(new AssetObjectRef(shader));
                var nodesById = structureBeforeWiring.Nodes!.ToDictionary(node => node.ObjectId);
                var branchNode = nodesById[branch.Node.ObjectId];
                var subtractNode = nodesById[subtract.Node!.ObjectId];
                var sceneColorNode = nodesById[sceneColor.Node!.ObjectId];
                var branchSceneDepthNode = nodesById[branchSceneDepth.Node!.ObjectId];
                var subtractSceneDepthNode = nodesById[subtractSceneDepth.Node!.ObjectId];

                Assert.AreEqual("UnityEditor.ShaderGraph.DynamicVectorMaterialSlot", FindSlot(branchNode, "Out").Type);
                Assert.AreEqual("UnityEditor.ShaderGraph.DynamicVectorMaterialSlot", FindSlot(subtractNode, "Out").Type);
                Assert.AreEqual("UnityEditor.ShaderGraph.ScreenPositionMaterialSlot", FindSlot(sceneColorNode, "UV").Type);
                Assert.AreEqual("UnityEditor.ShaderGraph.ScreenPositionMaterialSlot", FindSlot(branchSceneDepthNode, "UV").Type);
                Assert.AreEqual("UnityEditor.ShaderGraph.ScreenPositionMaterialSlot", FindSlot(subtractSceneDepthNode, "UV").Type);

                ConnectSlots(tool, shader, branchNode, "Out", sceneColorNode, "UV");
                ConnectSlots(tool, shader, branchNode, "Out", branchSceneDepthNode, "UV");
                var finalConnectResult = ConnectSlots(
                    tool,
                    shader,
                    subtractNode,
                    "Out",
                    subtractSceneDepthNode,
                    "UV",
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsTrue(finalConnectResult.Structure!.Edges!.Any(edge =>
                    edge.OutputNodeId == branchNode.ObjectId
                    && edge.InputNodeId == sceneColorNode.ObjectId),
                    "Expected Branch.Out to feed Scene Color.UV.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == branchNode.ObjectId
                    && edge.InputNodeId == branchSceneDepthNode.ObjectId),
                    "Expected Branch.Out to feed Scene Depth.UV.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == subtractNode.ObjectId
                    && edge.InputNodeId == subtractSceneDepthNode.ObjectId),
                    "Expected Subtract.Out to feed Scene Depth.UV.");

                Assert.IsNotNull(finalConnectResult.Graph);
                Assert.IsTrue(finalConnectResult.Graph!.ShaderResolved, "Dynamic screen-position input validation graph should resolve to a compiled Shader.");
                Assert.IsFalse(finalConnectResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Dynamic screen-position input validation graph should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_MinionsArtWaterTilingInputs_AcceptScalarPropertyOutputs()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_MinionsArtWaterScalarToTilingEdges.shadergraph", LitFullTemplateAssetPath);
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
                        DisplayName = "Distort Scale",
                        OverrideReferenceName = "_DistortScale",
                        FloatValue = 1.75f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "float",
                        DisplayName = "Noise Scale",
                        OverrideReferenceName = "_NoiseScale",
                        FloatValue = 12f
                    });

                var distortScaleProperty = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_DistortScale",
                        PositionX = -1220f,
                        PositionY = -40f
                    });
                var noiseScaleProperty = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_NoiseScale",
                        PositionX = -1220f,
                        PositionY = 200f
                    });
                var distortTiling = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "tilingAndOffset", PositionX = -880f, PositionY = -40f });
                var noiseTiling = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "tilingAndOffset", PositionX = -880f, PositionY = 200f });

                var structureBeforeWiring = tool.GetStructure(new AssetObjectRef(shader));
                var nodesById = structureBeforeWiring.Nodes!.ToDictionary(node => node.ObjectId);
                var distortScalePropertyNode = nodesById[distortScaleProperty.Node!.ObjectId];
                var noiseScalePropertyNode = nodesById[noiseScaleProperty.Node!.ObjectId];
                var distortTilingNode = nodesById[distortTiling.Node!.ObjectId];
                var noiseTilingNode = nodesById[noiseTiling.Node!.ObjectId];

                Assert.AreEqual("UnityEditor.ShaderGraph.Vector1MaterialSlot", FindSlot(distortScalePropertyNode, "Distort Scale").Type);
                Assert.AreEqual("UnityEditor.ShaderGraph.Vector1MaterialSlot", FindSlot(noiseScalePropertyNode, "Noise Scale").Type);
                Assert.AreEqual("UnityEditor.ShaderGraph.Vector2MaterialSlot", FindSlot(distortTilingNode, "Tiling").Type);
                Assert.AreEqual("UnityEditor.ShaderGraph.Vector2MaterialSlot", FindSlot(noiseTilingNode, "Tiling").Type);

                ConnectSlots(tool, shader, distortScalePropertyNode, "Distort Scale", distortTilingNode, "Tiling");
                var finalConnectResult = ConnectSlots(
                    tool,
                    shader,
                    noiseScalePropertyNode,
                    "Noise Scale",
                    noiseTilingNode,
                    "Tiling",
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsTrue(finalConnectResult.Structure!.Edges!.Any(edge =>
                    edge.OutputNodeId == distortScalePropertyNode.ObjectId
                    && edge.InputNodeId == distortTilingNode.ObjectId),
                    "Expected Distort Scale property to feed Tiling And Offset.Tiling.");
                Assert.IsTrue(finalConnectResult.Structure.Edges.Any(edge =>
                    edge.OutputNodeId == noiseScalePropertyNode.ObjectId
                    && edge.InputNodeId == noiseTilingNode.ObjectId),
                    "Expected Noise Scale property to feed Tiling And Offset.Tiling.");

                Assert.IsNotNull(finalConnectResult.Graph);
                Assert.IsTrue(finalConnectResult.Graph!.ShaderResolved, "Scalar-to-tiling validation graph should resolve to a compiled Shader.");
                Assert.IsFalse(finalConnectResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Scalar-to-tiling validation graph should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_MinionsArtWaterLiteralDefaults_CanSetMultiplyAndRemap()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_MinionsArtWaterLiteralDefaults.shadergraph", LitFullTemplateAssetPath);
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var multiply = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "multiply", PositionX = -880f, PositionY = -40f });
                var remap = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "remap", PositionX = -880f, PositionY = 200f });

                var multiplyResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = multiply.Node!.ObjectId,
                        Multiply = new ShaderGraphMultiplyNodeSettingsUpdateInput
                        {
                            A = new ShaderGraphVector4ValueUpdateInput { X = 0.5f },
                            B = new ShaderGraphVector4ValueUpdateInput { X = 0.1f }
                        }
                    });
                var remapResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = remap.Node!.ObjectId,
                        Remap = new ShaderGraphRemapNodeSettingsUpdateInput
                        {
                            Input = new ShaderGraphVector4ValueUpdateInput { X = 0.25f, Y = 0.5f, Z = 0.75f, W = 1f },
                            InMinMax = new ShaderGraphVector2ValueUpdateInput { X = 0f, Y = 1f },
                            OutMinMax = new ShaderGraphVector2ValueUpdateInput { X = -1f, Y = 1f }
                        }
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsTrue(multiplyResult.ChangedFields!.Contains("node.multiply.a.x"));
                Assert.IsTrue(multiplyResult.ChangedFields.Contains("node.multiply.b.x"));
                Assert.IsNotNull(multiplyResult.Node!.Multiply);
                Assert.AreEqual(0.5f, multiplyResult.Node.Multiply!.A!.X ?? 0f, 0.0001f);
                Assert.AreEqual(0.1f, multiplyResult.Node.Multiply.B!.X ?? 0f, 0.0001f);
                AssertMatrixFirstRowComponent(multiplyResult.Node, "B", "e00", 0.1f);

                Assert.IsTrue(remapResult.ChangedFields!.Contains("node.remap.input.x"));
                Assert.IsTrue(remapResult.ChangedFields.Contains("node.remap.inMinMax.x"));
                Assert.IsTrue(remapResult.ChangedFields.Contains("node.remap.inMinMax.y"));
                Assert.IsNotNull(remapResult.Node!.Remap);
                Assert.AreEqual(0f, remapResult.Node.Remap!.InMinMax!.X ?? -1f, 0.0001f);
                Assert.AreEqual(1f, remapResult.Node.Remap.InMinMax.Y ?? -1f, 0.0001f);
                AssertSlotVector4(remapResult.Node, "In", 0.25f, 0.5f, 0.75f, 1f);
                AssertSlotVector2(remapResult.Node, "In Min Max", 0f, 1f);
                AssertSlotVector2(remapResult.Node, "Out Min Max", -1f, 1f);

                Assert.IsNotNull(remapResult.Graph);
                Assert.IsTrue(remapResult.Graph!.ShaderResolved, "Updating MinionsArt literal defaults should keep the Shader Graph import valid.");
                Assert.IsFalse(remapResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Updating MinionsArt literal defaults should not introduce import errors.");
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(movedColorNode);
                Assert.AreEqual("updatePosition", movedColorNode.Operation);
                Assert.IsNotNull(movedColorNode.Node);
                Assert.AreEqual(existingColorNode.ObjectId, movedColorNode.NodeObjectId);
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
        public void ShaderGraph_ReconnectEdge_RetargetsUvInputToDynamicVectorOutput()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_ReconnectEdge_DynamicVectorUv.shadergraph");
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
                        DisplayName = "UV Base",
                        OverrideReferenceName = "_UvBase",
                        VectorX = 0.15f,
                        VectorY = 0.35f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "vector2",
                        DisplayName = "UV Offset",
                        OverrideReferenceName = "_UvOffset",
                        VectorX = 0.4f,
                        VectorY = -0.2f
                    });

                var uvBaseNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_UvBase",
                        PositionX = -980f,
                        PositionY = -80f
                    });
                var uvOffsetNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_UvOffset",
                        PositionX = -980f,
                        PositionY = 120f
                    });
                var addNodeResult = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "add",
                        PositionX = -700f,
                        PositionY = 20f
                    });
                var tilingNodeResult = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "tilingAndOffset",
                        PositionX = -420f,
                        PositionY = 20f
                    });

                var structureBeforeReconnect = tool.GetStructure(new AssetObjectRef(shader));
                var addNode = structureBeforeReconnect.Nodes!
                    .First(n => n.ObjectId == addNodeResult.Node!.ObjectId);
                var addInputA = addNode.Slots!.First(s => s.DisplayName == "A");
                var addInputB = addNode.Slots.First(s => s.DisplayName == "B");
                var addOutput = addNode.Slots.First(s => s.DisplayName == "Out");
                var tilingNode = structureBeforeReconnect.Nodes
                    .First(n => n.ObjectId == tilingNodeResult.Node!.ObjectId);
                var tilingUvInput = tilingNode.Slots!
                    .First(s => s.DisplayName == "UV");
                var uvBaseOutput = uvBaseNodeResult.Node!.Slots!.Single();
                var uvOffsetOutput = uvOffsetNodeResult.Node!.Slots!.Single();

                tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = uvBaseNodeResult.Node.ObjectId,
                        OutputSlotObjectId = uvBaseOutput.ObjectId,
                        InputNodeObjectId = addNode.ObjectId,
                        InputSlotObjectId = addInputA.ObjectId
                    });
                tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = uvOffsetNodeResult.Node.ObjectId,
                        OutputSlotObjectId = uvOffsetOutput.ObjectId,
                        InputNodeObjectId = addNode.ObjectId,
                        InputSlotObjectId = addInputB.ObjectId
                    });
                tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = uvBaseNodeResult.Node.ObjectId,
                        OutputSlotObjectId = uvBaseOutput.ObjectId,
                        InputNodeObjectId = tilingNode.ObjectId,
                        InputSlotObjectId = tilingUvInput.ObjectId
                    });

                var reconnectResult = tool.ReconnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphReconnectEdgeInput
                    {
                        ExistingOutputNodeObjectId = uvBaseNodeResult.Node.ObjectId,
                        ExistingOutputSlotObjectId = uvBaseOutput.ObjectId,
                        ExistingInputNodeObjectId = tilingNode.ObjectId,
                        ExistingInputSlotObjectId = tilingUvInput.ObjectId,
                        NewOutputNodeObjectId = addNode.ObjectId,
                        NewOutputSlotObjectId = addOutput.ObjectId
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(reconnectResult);
                Assert.IsTrue(reconnectResult.ChangedFields!.Contains("edge.disconnected"));
                Assert.IsTrue(reconnectResult.ChangedFields.Contains("edge.reconnected"));
                Assert.IsTrue(reconnectResult.ChangedFields.Contains("edge.connected"));

                Assert.IsNotNull(reconnectResult.RemovedEdge);
                Assert.AreEqual(uvBaseNodeResult.Node.ObjectId, reconnectResult.RemovedEdge!.OutputNodeId);
                Assert.AreEqual(uvBaseOutput.SlotId, reconnectResult.RemovedEdge.OutputSlotId);
                Assert.AreEqual(tilingNode.ObjectId, reconnectResult.RemovedEdge.InputNodeId);
                Assert.AreEqual(tilingUvInput.SlotId, reconnectResult.RemovedEdge.InputSlotId);

                Assert.IsNotNull(reconnectResult.Edge);
                Assert.AreEqual(addNode.ObjectId, reconnectResult.Edge!.OutputNodeId);
                Assert.AreEqual(addOutput.SlotId, reconnectResult.Edge.OutputSlotId);
                Assert.AreEqual(tilingNode.ObjectId, reconnectResult.Edge.InputNodeId);
                Assert.AreEqual(tilingUvInput.SlotId, reconnectResult.Edge.InputSlotId);

                Assert.IsNotNull(reconnectResult.Structure);
                Assert.IsTrue(reconnectResult.Structure!.Edges!.Any(e =>
                    e.OutputNodeId == addNode.ObjectId
                    && e.OutputSlotId == addOutput.SlotId
                    && e.InputNodeId == tilingNode.ObjectId
                    && e.InputSlotId == tilingUvInput.SlotId));
                Assert.IsFalse(reconnectResult.Structure.Edges.Any(e =>
                    e.OutputNodeId == uvBaseNodeResult.Node.ObjectId
                    && e.OutputSlotId == uvBaseOutput.SlotId
                    && e.InputNodeId == tilingNode.ObjectId
                    && e.InputSlotId == tilingUvInput.SlotId),
                    "The previous direct vector2 feed into Tiling And Offset.UV should be removed during reconnect.");

                Assert.IsNotNull(reconnectResult.Graph);
                Assert.IsTrue(reconnectResult.Graph!.ShaderResolved, "Reconnecting a UV input to a dynamic vector output should keep the Shader Graph import valid.");
                Assert.IsFalse(reconnectResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Reconnecting a UV input to a dynamic vector output should not introduce import errors.");
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
                    includeStructure: true,
                    includeGraph: true,
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
        public void ShaderGraph_RerouteOutputSlot_MovesUvConsumersToDynamicVectorOutput()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_RerouteOutputSlot_DynamicVectorUv.shadergraph");
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
                        DisplayName = "Legacy UV",
                        OverrideReferenceName = "_LegacyUv",
                        VectorX = 0.5f,
                        VectorY = 0.125f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "vector2",
                        DisplayName = "UV Bias",
                        OverrideReferenceName = "_UvBias",
                        VectorX = 0.1f,
                        VectorY = 0.2f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "vector2",
                        DisplayName = "UV Delta",
                        OverrideReferenceName = "_UvDelta",
                        VectorX = -0.15f,
                        VectorY = 0.3f
                    });

                var legacyUvNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_LegacyUv",
                        PositionX = -1060f,
                        PositionY = -120f
                    });
                var uvBiasNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_UvBias",
                        PositionX = -1060f,
                        PositionY = 80f
                    });
                var uvDeltaNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_UvDelta",
                        PositionX = -1060f,
                        PositionY = 280f
                    });
                var addNodeResult = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "add",
                        PositionX = -760f,
                        PositionY = 180f
                    });
                var tilingNodeAResult = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "tilingAndOffset",
                        PositionX = -420f,
                        PositionY = -140f
                    });
                var tilingNodeBResult = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "tilingAndOffset",
                        PositionX = -420f,
                        PositionY = 120f
                    });

                var structureBeforeReroute = tool.GetStructure(new AssetObjectRef(shader));
                var addNode = structureBeforeReroute.Nodes!
                    .First(n => n.ObjectId == addNodeResult.Node!.ObjectId);
                var addInputA = addNode.Slots!.First(s => s.DisplayName == "A");
                var addInputB = addNode.Slots.First(s => s.DisplayName == "B");
                var addOutput = addNode.Slots.First(s => s.DisplayName == "Out");
                var tilingNodeA = structureBeforeReroute.Nodes
                    .First(n => n.ObjectId == tilingNodeAResult.Node!.ObjectId);
                var tilingNodeAUvInput = tilingNodeA.Slots!
                    .First(s => s.DisplayName == "UV");
                var tilingNodeB = structureBeforeReroute.Nodes
                    .First(n => n.ObjectId == tilingNodeBResult.Node!.ObjectId);
                var tilingNodeBUvInput = tilingNodeB.Slots!
                    .First(s => s.DisplayName == "UV");
                var sampleTextureNode = structureBeforeReroute.Nodes
                    .First(n => n.Name == "Sample Texture 2D");
                var sampleUvInput = sampleTextureNode.Slots!
                    .First(s => s.DisplayName == "UV");
                var legacyUvOutput = legacyUvNodeResult.Node!.Slots!.Single();
                var uvBiasOutput = uvBiasNodeResult.Node!.Slots!.Single();
                var uvDeltaOutput = uvDeltaNodeResult.Node!.Slots!.Single();

                tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = uvBiasNodeResult.Node.ObjectId,
                        OutputSlotObjectId = uvBiasOutput.ObjectId,
                        InputNodeObjectId = addNode.ObjectId,
                        InputSlotObjectId = addInputA.ObjectId
                    });
                tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = uvDeltaNodeResult.Node.ObjectId,
                        OutputSlotObjectId = uvDeltaOutput.ObjectId,
                        InputNodeObjectId = addNode.ObjectId,
                        InputSlotObjectId = addInputB.ObjectId
                    });
                tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = legacyUvNodeResult.Node.ObjectId,
                        OutputSlotObjectId = legacyUvOutput.ObjectId,
                        InputNodeObjectId = tilingNodeA.ObjectId,
                        InputSlotObjectId = tilingNodeAUvInput.ObjectId
                    });
                tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = legacyUvNodeResult.Node.ObjectId,
                        OutputSlotObjectId = legacyUvOutput.ObjectId,
                        InputNodeObjectId = tilingNodeB.ObjectId,
                        InputSlotObjectId = tilingNodeBUvInput.ObjectId
                    });
                tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = legacyUvNodeResult.Node.ObjectId,
                        OutputSlotObjectId = legacyUvOutput.ObjectId,
                        InputNodeObjectId = sampleTextureNode.ObjectId,
                        InputSlotObjectId = sampleUvInput.ObjectId
                    });

                var structureWithLegacyConsumers = tool.GetStructure(new AssetObjectRef(shader));
                var existingLegacyConsumers = structureWithLegacyConsumers.Edges!
                    .Where(e => e.OutputNodeId == legacyUvNodeResult.Node.ObjectId
                        && e.OutputSlotId == legacyUvOutput.SlotId)
                    .ToList();

                Assert.AreEqual(3, existingLegacyConsumers.Count,
                    "Expected the direct UV property to feed the three UV consumers before reroute.");

                var rerouteResult = tool.RerouteOutputSlot(
                    new AssetObjectRef(shader),
                    new ShaderGraphRerouteOutputSlotInput
                    {
                        ExistingOutputNodeObjectId = legacyUvNodeResult.Node.ObjectId,
                        ExistingOutputSlotObjectId = legacyUvOutput.ObjectId,
                        NewOutputNodeObjectId = addNode.ObjectId,
                        NewOutputSlotObjectId = addOutput.ObjectId
                    },
                    includeStructure: true,
                    includeGraph: true,
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
                        e.OutputNodeId == legacyUvNodeResult.Node.ObjectId
                        && e.OutputSlotId == legacyUvOutput.SlotId),
                    "The direct UV property output should not feed any consumers after reroute.");

                Assert.IsTrue(rerouteResult.Structure.Edges.Any(e =>
                    e.OutputNodeId == addNode.ObjectId
                    && e.OutputSlotId == addOutput.SlotId
                    && e.InputNodeId == tilingNodeA.ObjectId
                    && e.InputSlotId == tilingNodeAUvInput.SlotId));
                Assert.IsTrue(rerouteResult.Structure.Edges.Any(e =>
                    e.OutputNodeId == addNode.ObjectId
                    && e.OutputSlotId == addOutput.SlotId
                    && e.InputNodeId == tilingNodeB.ObjectId
                    && e.InputSlotId == tilingNodeBUvInput.SlotId));
                Assert.IsTrue(rerouteResult.Structure.Edges.Any(e =>
                    e.OutputNodeId == addNode.ObjectId
                    && e.OutputSlotId == addOutput.SlotId
                    && e.InputNodeId == sampleTextureNode.ObjectId
                    && e.InputSlotId == sampleUvInput.SlotId));
                Assert.AreEqual(structureWithLegacyConsumers.Edges.Count, rerouteResult.Structure.Edges.Count,
                    "Rerouting the UV consumers should preserve total edge count.");

                Assert.IsNotNull(rerouteResult.Graph);
                Assert.IsTrue(rerouteResult.Graph!.ShaderResolved, "Rerouting UV consumers to a dynamic vector output should keep the Shader Graph import valid.");
                Assert.IsFalse(rerouteResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Rerouting UV consumers to a dynamic vector output should not introduce import errors.");
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
                    includeStructure: true,
                    includeGraph: true,
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
        public void ShaderGraph_ConnectEdge_AllowsDynamicVectorOutputsToFeedUvSlots()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateEdge_DynamicVectorUv.shadergraph");
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
                        DisplayName = "UV A",
                        OverrideReferenceName = "_UvA",
                        VectorX = 0.125f,
                        VectorY = 0.875f
                    });
                tool.AddProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyInput
                    {
                        PropertyType = "vector2",
                        DisplayName = "UV B",
                        OverrideReferenceName = "_UvB",
                        VectorX = 0.2f,
                        VectorY = -0.35f
                    });

                var uvANodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_UvA",
                        PositionX = -980f,
                        PositionY = -40f
                    });
                var uvBNodeResult = tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_UvB",
                        PositionX = -980f,
                        PositionY = 160f
                    });
                var addNodeResult = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "add",
                        PositionX = -700f,
                        PositionY = 60f
                    });
                var tilingNodeResult = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "tilingAndOffset",
                        PositionX = -420f,
                        PositionY = 60f
                    });

                var structureBeforeConnect = tool.GetStructure(new AssetObjectRef(shader));
                var addNode = structureBeforeConnect.Nodes!
                    .First(n => n.ObjectId == addNodeResult.Node!.ObjectId);
                var addInputA = addNode.Slots!.First(s => s.DisplayName == "A");
                var addInputB = addNode.Slots.First(s => s.DisplayName == "B");
                var addOutput = addNode.Slots.First(s => s.DisplayName == "Out");
                var tilingNode = structureBeforeConnect.Nodes
                    .First(n => n.ObjectId == tilingNodeResult.Node!.ObjectId);
                var tilingUvInput = tilingNode.Slots!
                    .First(s => s.DisplayName == "UV");
                var uvAOutput = uvANodeResult.Node!.Slots!.Single();
                var uvBOutput = uvBNodeResult.Node!.Slots!.Single();

                tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = uvANodeResult.Node.ObjectId,
                        OutputSlotObjectId = uvAOutput.ObjectId,
                        InputNodeObjectId = addNode.ObjectId,
                        InputSlotObjectId = addInputA.ObjectId
                    });
                tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = uvBNodeResult.Node.ObjectId,
                        OutputSlotObjectId = uvBOutput.ObjectId,
                        InputNodeObjectId = addNode.ObjectId,
                        InputSlotObjectId = addInputB.ObjectId
                    });

                var connectDynamicUv = tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = addNode.ObjectId,
                        OutputSlotObjectId = addOutput.ObjectId,
                        InputNodeObjectId = tilingNode.ObjectId,
                        InputSlotObjectId = tilingUvInput.ObjectId
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(connectDynamicUv);
                Assert.IsTrue(connectDynamicUv.ChangedFields!.Contains("edge.connected"));

                Assert.IsNotNull(connectDynamicUv.Structure);
                Assert.IsTrue(connectDynamicUv.Structure!.Edges!.Any(e =>
                    e.OutputNodeId == addNode.ObjectId
                    && e.OutputSlotId == addOutput.SlotId
                    && e.InputNodeId == tilingNode.ObjectId
                    && e.InputSlotId == tilingUvInput.SlotId));

                Assert.IsNotNull(connectDynamicUv.Graph);
                Assert.IsTrue(connectDynamicUv.Graph!.ShaderResolved, "Dynamic vector outputs should be able to feed UV inputs when Unity resolves the math node as vector2-compatible.");
                Assert.IsFalse(connectDynamicUv.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Connecting a dynamic vector output into a UV input should not introduce import errors when the graph resolves to a valid vector2 flow.");
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
        public void ShaderGraph_ValidateTextureWorkflow_ReportsGraphAndMaterialTextureAssignments()
        {
            var graphAssetPath = CreateShaderGraphAssetCopy("Validation_TextureWorkflow.shadergraph");
            var materialAssetPath = $"{TestFolder}/Validation_TextureWorkflow.mat";
            var blackboardTexturePath = CreateTextureAsset("Validation_TextureWorkflow_Blackboard.png", new Color(0.85f, 0.2f, 0.1f, 1f));
            var nodeTexturePath = CreateTextureAsset("Validation_TextureWorkflow_Node.png", new Color(0.1f, 0.45f, 0.9f, 1f));
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(graphAssetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{graphAssetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                tool.UpdateProperty(
                    new AssetObjectRef(shader),
                    new ShaderGraphPropertyUpdateInput
                    {
                        PropertyReferenceName = "_BaseMap",
                        TextureAssetPath = blackboardTexturePath
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                var sampleNodeResult = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "sampleTexture2D",
                        PositionX = -720f,
                        PositionY = -80f
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = sampleNodeResult.NodeObjectId,
                        SampleTexture2D = new ShaderGraphSampleTexture2DNodeSettingsUpdateInput
                        {
                            TextureSlotAssetPath = nodeTexturePath,
                            TextureSlotDefaultType = "red"
                        }
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                var result = tool.ValidateTextureWorkflow(
                    assetRef: new AssetObjectRef(shader),
                    materialAssetPath: materialAssetPath,
                    overwrite: true,
                    applyGraphTextureDefaultsToMaterial: true,
                    expectedGraphTextureAssetPaths: new[] { blackboardTexturePath, nodeTexturePath },
                    expectedMaterialTextures: new[]
                    {
                        new ShaderGraphExpectedMaterialTextureInput
                        {
                            PropertyName = "_BaseMap",
                            TextureAssetPath = blackboardTexturePath
                        }
                    },
                    includeStructure: false);

                Assert.IsNotNull(result);
                Assert.AreEqual(graphAssetPath, result.GraphAssetPath);
                Assert.AreEqual(materialAssetPath, result.MaterialAssetPath);
                Assert.IsTrue(result.ShaderMatchesGraph, "Validation material should use the Shader Graph's compiled shader.");
                Assert.IsTrue(result.AllExpectationsMatched, "All requested graph and material texture expectations should match.");
                Assert.IsTrue(result.AppliedMaterialTexturePropertyNames!.Contains("_BaseMap"),
                    "Blackboard texture defaults should be copied into matching material texture properties.");

                Assert.IsTrue(result.GraphTextureReferences!.Any(reference =>
                        reference.SourceKind == "blackboardProperty"
                        && reference.PropertyReferenceName == "_BaseMap"
                        && reference.TextureAssetPath == blackboardTexturePath),
                    "Expected graph texture references to include the assigned blackboard texture.");
                Assert.IsTrue(result.GraphTextureReferences!.Any(reference =>
                        reference.SourceKind == "nodeSlot"
                        && reference.OwnerObjectId == sampleNodeResult.NodeObjectId
                        && reference.TextureAssetPath == nodeTexturePath
                        && reference.TextureDefaultType == "red"),
                    "Expected graph texture references to include the direct Sample Texture 2D slot texture.");

                var baseMapTextureProperty = result.MaterialTextureProperties!
                    .Single(property => property.PropertyName == "_BaseMap");
                Assert.IsTrue(baseMapTextureProperty.HasTexture, "Validation material should have _BaseMap assigned.");
                Assert.AreEqual(blackboardTexturePath, baseMapTextureProperty.TextureAssetPath);
                Assert.IsTrue(baseMapTextureProperty.WasAppliedFromGraph,
                    "The _BaseMap material texture should report that it came from the graph blackboard default.");

                var material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
                var blackboardTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(blackboardTexturePath);
                Assert.IsNotNull(material, $"Expected Material asset to resolve at '{materialAssetPath}'.");
                Assert.AreEqual(blackboardTexture, material!.GetTexture("_BaseMap"),
                    "Created material should reference the graph-assigned blackboard texture asset.");

                Assert.IsNotNull(result.Graph);
                Assert.IsTrue(result.Graph!.ShaderResolved, "Texture workflow validation should keep the Shader Graph import valid.");
                Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Texture workflow validation should not introduce graph import errors.");
            }
            finally
            {
                CleanupTestAsset(materialAssetPath);
                CleanupTestAsset(graphAssetPath);
                CleanupTestAsset(blackboardTexturePath);
                CleanupTestAsset(nodeTexturePath);
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

        [Test]
        public void ShaderGraph_AddNode_DefaultsToSlimResponseShape()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddNode_SlimDefault.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();

                var slim = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "add",
                        PositionX = -200f,
                        PositionY = 60f
                    });

                Assert.AreEqual("add", slim.Operation);
                Assert.IsNotNull(slim.Node, "Slim default should still surface the added node diff.");
                Assert.IsNotNull(slim.ChangedFields, "Slim default should still surface ChangedFields.");
                Assert.IsTrue(slim.ChangedFields!.Contains("node.added"));

                Assert.IsNotNull(slim.GraphSummary, "Slim default must populate GraphSummary.");
                Assert.IsTrue(slim.GraphSummary!.ShaderResolved, "Adding an allowlisted node should keep the Shader Graph valid.");
                Assert.IsFalse(slim.GraphSummary.HasErrors, "Adding an allowlisted node should not introduce import errors.");
                Assert.Greater(slim.GraphSummary.NodeCount, 0, "GraphSummary should report a positive node count after add.");
                if (slim.GraphSummary.Diagnostics != null)
                {
                    Assert.IsFalse(slim.GraphSummary.Diagnostics.Any(d => string.Equals(d.Severity, "Info", StringComparison.Ordinal)),
                        "GraphSummary diagnostics should filter out Info-severity entries.");
                }

                Assert.IsNull(slim.Structure, "Slim default must NOT include the full Structure block.");
                Assert.IsNull(slim.Graph, "Slim default must NOT include the full Graph block.");

                var withStructure = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "add",
                        PositionX = -200f,
                        PositionY = 180f
                    },
                    includeStructure: true);

                Assert.IsNotNull(withStructure.Structure, "Passing includeStructure: true must restore the full Structure block.");
                Assert.IsNotNull(withStructure.GraphSummary, "GraphSummary should always be populated.");
                Assert.IsNull(withStructure.Graph, "includeStructure alone must NOT populate the full Graph block.");
                Assert.IsTrue(withStructure.Structure!.Nodes!.Any(n => n.ObjectId == withStructure.Node!.ObjectId),
                    "Structure block should contain the freshly added node.");

                var withGraph = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput
                    {
                        NodeType = "add",
                        PositionX = -200f,
                        PositionY = 300f
                    },
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.IsNotNull(withGraph.Graph, "Passing includeGraph: true must restore the full Graph block.");
                Assert.IsTrue(withGraph.Graph!.ShaderResolved);
                Assert.IsNull(withGraph.Structure, "includeGraph alone must NOT populate the full Structure block.");
                Assert.IsNotNull(withGraph.GraphSummary, "GraphSummary should always be populated.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddNode_AddsFlameTrialNodes()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddNode_FlameTrial.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var nodesToCreate = new[]
                {
                    new
                    {
                        ApiName = "uv",
                        TypeName = "UnityEditor.ShaderGraph.UVNode",
                        DisplayName = "UV",
                        SlotNames = new[] { "Out" }
                    },
                    new
                    {
                        ApiName = "simpleNoise",
                        TypeName = "UnityEditor.ShaderGraph.NoiseNode",
                        DisplayName = "Simple Noise",
                        SlotNames = new[] { "UV", "Scale", "Out" }
                    }
                };

                ShaderGraphNodeMutationResultData? simpleNoiseResult = null;
                for (var i = 0; i < nodesToCreate.Length; i++)
                {
                    var nodeToCreate = nodesToCreate[i];
                    var result = tool.AddNode(
                        new AssetObjectRef(shader),
                        new ShaderGraphAddNodeInput
                        {
                            NodeType = nodeToCreate.ApiName,
                            PositionX = -1200f + i * 200f,
                            PositionY = -80f
                        },
                        includeGraph: i == nodesToCreate.Length - 1,
                        includeMessages: i == nodesToCreate.Length - 1,
                        includeProperties: i == nodesToCreate.Length - 1);

                    Assert.AreEqual("add", result.Operation);
                    Assert.IsTrue(result.ChangedFields!.Contains("node.added"));
                    Assert.IsNotNull(result.Node);
                    Assert.AreEqual(nodeToCreate.TypeName, result.Node!.Type);
                    Assert.AreEqual(nodeToCreate.DisplayName, result.Node.Name);
                    foreach (var slotName in nodeToCreate.SlotNames)
                    {
                        Assert.IsTrue(result.Node.Slots!.Any(slot => slot.DisplayName == slotName),
                            $"Expected '{nodeToCreate.DisplayName}' to expose slot '{slotName}'.");
                    }

                    if (nodeToCreate.ApiName == "simpleNoise")
                        simpleNoiseResult = result;

                    if (i == nodesToCreate.Length - 1)
                    {
                        Assert.IsNotNull(result.Graph);
                        Assert.IsTrue(result.Graph!.ShaderResolved, "Adding flame-trial nodes should keep the Shader Graph import valid.");
                        Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                            "Adding flame-trial nodes should not introduce import errors.");
                    }
                }

                Assert.IsNotNull(simpleNoiseResult, "Expected the Simple Noise node to be created for duplicate/move/delete validation.");
                var duplicateResult = tool.DuplicateNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphDuplicateNodeInput
                    {
                        NodeObjectId = simpleNoiseResult!.Node!.ObjectId,
                        PositionOffsetX = 64f,
                        PositionOffsetY = 32f
                    });

                Assert.IsNotNull(duplicateResult.Node);
                Assert.AreEqual("UnityEditor.ShaderGraph.NoiseNode", duplicateResult.Node!.Type);
                Assert.AreNotEqual(simpleNoiseResult.Node.ObjectId, duplicateResult.Node.ObjectId);
                Assert.AreEqual(simpleNoiseResult.Node.PositionX + 64f, duplicateResult.Node.PositionX, 0.001f);

                var moveResult = tool.UpdateNodePosition(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodePositionInput
                    {
                        NodeObjectId = duplicateResult.Node.ObjectId,
                        PositionX = 200f,
                        PositionY = 320f
                    });

                Assert.AreEqual(200f, moveResult.Node!.PositionX);
                Assert.AreEqual(320f, moveResult.Node.PositionY);

                var deleteResult = tool.DeleteNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphDeleteNodeInput
                    {
                        NodeObjectId = duplicateResult.Node.ObjectId
                    });

                Assert.AreEqual("delete", deleteResult.Operation);
                Assert.IsFalse(deleteResult.Structure!.Nodes!.Any(n => n.ObjectId == duplicateResult.Node.ObjectId));
                Assert.IsTrue(deleteResult.Graph!.ShaderResolved, "Deleting a duplicated Simple Noise node should keep the Shader Graph import valid.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateNodeSettings_UpdatesFlameTrialSettings()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodeSettings_FlameTrial.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var uv = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "uv", PositionX = -1100f, PositionY = -40f });
                var simpleNoise = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "simpleNoise", PositionX = -800f, PositionY = -40f });

                Assert.AreEqual("UV0", uv.Node!.Uv!.Channel);

                foreach (var channel in new[] { "UV1", "UV2", "UV3", "UV0" })
                {
                    var uvResult = tool.UpdateNodeSettings(
                        new AssetObjectRef(shader),
                        new ShaderGraphUpdateNodeSettingsInput
                        {
                            NodeObjectId = uv.Node.ObjectId,
                            Uv = new ShaderGraphUvNodeSettingsUpdateInput { Channel = channel }
                        });

                    Assert.AreEqual(channel, uvResult.Node!.Uv!.Channel,
                        $"Expected UV channel readback to match '{channel}' after update.");
                }

                var simpleNoiseResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = simpleNoise.Node!.ObjectId,
                        SimpleNoise = new ShaderGraphSimpleNoiseNodeSettingsUpdateInput { Scale = 42.5f }
                    },
                    includeStructure: true,
                    includeGraph: true,
                    includeMessages: true,
                    includeProperties: true);

                Assert.AreEqual(42.5f, simpleNoiseResult.Node!.SimpleNoise!.Scale ?? 0f, 0.0001f);
                AssertSlotFloat(simpleNoiseResult.Node, "Scale", 42.5f);

                var invalidChannel = Assert.Throws<ArgumentException>(() => tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = uv.Node.ObjectId,
                        Uv = new ShaderGraphUvNodeSettingsUpdateInput { Channel = "UV4" }
                    }));
                StringAssert.Contains("Supported values: UV0, UV1, UV2, UV3", invalidChannel!.Message);

                Assert.IsNotNull(simpleNoiseResult.Graph);
                Assert.IsTrue(simpleNoiseResult.Graph!.ShaderResolved, "Updating flame-trial node settings should keep the Shader Graph import valid.");
                Assert.IsFalse(simpleNoiseResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Updating flame-trial node settings should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_FlameTrialNodes_CanBeWiredEndToEnd()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_FlameTrial_E2E.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var uv = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "uv", PositionX = -1100f, PositionY = 0f });
                var add = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "add", PositionX = -820f, PositionY = 0f });
                var simpleNoise = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "simpleNoise", PositionX = -540f, PositionY = 0f });
                var sampleTexture = tool.AddNode(new AssetObjectRef(shader), new ShaderGraphAddNodeInput { NodeType = "sampleTexture2D", PositionX = -260f, PositionY = 0f });

                tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = simpleNoise.Node!.ObjectId,
                        SimpleNoise = new ShaderGraphSimpleNoiseNodeSettingsUpdateInput { Scale = 100f }
                    });

                var structure = tool.GetStructure(new AssetObjectRef(shader));
                var nodesById = structure.Nodes!.ToDictionary(n => n.ObjectId!, n => n);
                var uvNode = nodesById[uv.Node!.ObjectId!];
                var addNode = nodesById[add.Node!.ObjectId!];
                var simpleNoiseNode = nodesById[simpleNoise.Node.ObjectId!];
                var sampleTextureNode = nodesById[sampleTexture.Node!.ObjectId!];

                ConnectSlots(tool, shader, uvNode, "Out", addNode, "A");
                ConnectSlots(tool, shader, addNode, "Out", simpleNoiseNode, "UV");
                var finalEdge = ConnectSlots(tool, shader, addNode, "Out", sampleTextureNode, "UV",
                    includeMessages: true, includeProperties: true);

                Assert.IsNotNull(finalEdge.Graph);
                Assert.IsTrue(finalEdge.Graph!.ShaderResolved, "Wiring the flame-trial chain should keep the Shader Graph import valid.");
                Assert.IsFalse(finalEdge.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Wiring the flame-trial chain should not introduce import errors.");

                var finalStructure = tool.GetStructure(new AssetObjectRef(shader));
                Assert.IsTrue(finalStructure.Edges!.Any(edge =>
                    edge.OutputNodeId == uvNode.ObjectId
                    && edge.OutputSlotId == FindSlot(uvNode, "Out").SlotId
                    && edge.InputNodeId == addNode.ObjectId
                    && edge.InputSlotId == FindSlot(addNode, "A").SlotId),
                    "Expected UV.Out -> Add.A edge to be present after wiring.");
                Assert.IsTrue(finalStructure.Edges.Any(edge =>
                    edge.OutputNodeId == addNode.ObjectId
                    && edge.OutputSlotId == FindSlot(addNode, "Out").SlotId
                    && edge.InputNodeId == simpleNoiseNode.ObjectId
                    && edge.InputSlotId == FindSlot(simpleNoiseNode, "UV").SlotId),
                    "Expected Add.Out -> Simple Noise.UV edge to be present after wiring.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_UpdateNodeSettings_UpdatesSmoothstepEdgeAndInputSlots()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_UpdateNodeSettings_Smoothstep.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var smoothstep = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "smoothstep", PositionX = -600f, PositionY = 0f });

                var result = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = smoothstep.Node!.ObjectId,
                        Smoothstep = new ShaderGraphSmoothstepNodeSettingsUpdateInput
                        {
                            Edge1 = new ShaderGraphVector4ValueUpdateInput { X = 0.25f, Y = 0.25f, Z = 0.25f, W = 0.25f },
                            Edge2 = new ShaderGraphVector4ValueUpdateInput { X = 0.75f, Y = 0.75f, Z = 0.75f, W = 0.75f },
                            Input = new ShaderGraphVector4ValueUpdateInput { X = 0.5f, Y = 0.5f, Z = 0.5f, W = 0.5f }
                        }
                    },
                    includeGraph: true);

                Assert.AreEqual("updateSettings", result.Operation);
                Assert.IsTrue(result.ChangedFields!.Contains("node.smoothstep.edge1.x"));
                Assert.IsTrue(result.ChangedFields.Contains("node.smoothstep.edge2.x"));
                Assert.IsTrue(result.ChangedFields.Contains("node.smoothstep.input.x"));

                Assert.IsNotNull(result.Node!.Smoothstep);
                AssertSlotVector4(result.Node, "Edge1", 0.25f, 0.25f, 0.25f, 0.25f);
                AssertSlotVector4(result.Node, "Edge2", 0.75f, 0.75f, 0.75f, 0.75f);
                AssertSlotVector4(result.Node, "In", 0.5f, 0.5f, 0.5f, 0.5f);
                Assert.AreEqual(0.25f, result.Node.Smoothstep!.Edge1!.X ?? 0f, 0.0001f);
                Assert.AreEqual(0.75f, result.Node.Smoothstep.Edge2!.X ?? 0f, 0.0001f);
                Assert.AreEqual(0.5f, result.Node.Smoothstep.Input!.X ?? 0f, 0.0001f);

                Assert.IsTrue(result.Graph!.ShaderResolved, "Updating Smoothstep settings should keep the Shader Graph import valid.");
                Assert.IsFalse(result.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Updating Smoothstep settings should not introduce import errors.");

                var emptyPayload = Assert.Throws<ArgumentException>(() => tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = smoothstep.Node.ObjectId,
                        Smoothstep = new ShaderGraphSmoothstepNodeSettingsUpdateInput()
                    }));
                StringAssert.Contains("At least one supported node settings field must be provided",
                    emptyPayload!.Message);
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddNode_AddsPowerNodeWithBinaryVectorSettings()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddNode_Power.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var power = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "power", PositionX = -400f, PositionY = 0f },
                    includeStructure: true);

                Assert.AreEqual("add", power.Operation);
                Assert.AreEqual("UnityEditor.ShaderGraph.PowerNode", power.Node!.Type);
                Assert.AreEqual("Power", power.Node.Name);
                Assert.IsTrue(power.Node.Slots!.Any(s => s.DisplayName == "A"));
                Assert.IsTrue(power.Node.Slots.Any(s => s.DisplayName == "B"));
                Assert.IsTrue(power.Node.Slots.Any(s => s.DisplayName == "Out"));

                var settingsResult = tool.UpdateNodeSettings(
                    new AssetObjectRef(shader),
                    new ShaderGraphUpdateNodeSettingsInput
                    {
                        NodeObjectId = power.Node.ObjectId,
                        Power = new ShaderGraphBinaryVectorNodeSettingsUpdateInput
                        {
                            A = new ShaderGraphVector4ValueUpdateInput { X = 0.25f, Y = 0.25f, Z = 0.25f, W = 0.25f },
                            B = new ShaderGraphVector4ValueUpdateInput { X = 3f, Y = 3f, Z = 3f, W = 3f }
                        }
                    },
                    includeGraph: true);

                Assert.AreEqual("updateSettings", settingsResult.Operation);
                Assert.IsTrue(settingsResult.ChangedFields!.Contains("node.power.a.x"));
                Assert.IsTrue(settingsResult.ChangedFields.Contains("node.power.b.x"));
                AssertSlotVector4(settingsResult.Node!, "A", 0.25f, 0.25f, 0.25f, 0.25f);
                AssertSlotVector4(settingsResult.Node!, "B", 3f, 3f, 3f, 3f);
                Assert.IsTrue(settingsResult.Graph!.ShaderResolved, "Power node settings should keep the Shader Graph valid.");
                Assert.IsFalse(settingsResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Power node settings should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ConnectEdge_AllowsVector1ScalarBroadcastIntoUvInput()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_ConnectEdge_Vector1ToUv.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var time = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "time", PositionX = -900f, PositionY = -60f },
                    includeStructure: true);
                var simpleNoise = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "simpleNoise", PositionX = -500f, PositionY = -60f },
                    includeStructure: true);

                var timeOut = time.Node!.Slots!.First(s => s.DisplayName == "Time");
                var noiseUv = simpleNoise.Node!.Slots!.First(s => s.DisplayName == "UV");
                Assert.AreEqual("UnityEditor.ShaderGraph.Vector1MaterialSlot", timeOut.Type,
                    "Time.Time should be a Vector1MaterialSlot scalar output.");
                Assert.AreEqual("UnityEditor.ShaderGraph.UVMaterialSlot", noiseUv.Type,
                    "Simple Noise UV should be a UVMaterialSlot input.");

                var connect = tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = time.Node.ObjectId,
                        OutputSlotObjectId = timeOut.ObjectId,
                        InputNodeObjectId = simpleNoise.Node.ObjectId,
                        InputSlotObjectId = noiseUv.ObjectId
                    },
                    includeGraph: true);

                Assert.IsNotNull(connect.Edge, "Edge connection should return a populated Edge diff.");
                Assert.AreEqual(time.Node.ObjectId, connect.Edge!.OutputNodeId);
                Assert.AreEqual(simpleNoise.Node.ObjectId, connect.Edge.InputNodeId);
                Assert.IsTrue(connect.GraphSummary!.ShaderResolved,
                    "Scalar -> UV broadcast should keep the Shader Graph valid.");
                Assert.IsFalse(connect.GraphSummary.HasErrors,
                    "Scalar -> UV broadcast should not introduce import errors.");
                Assert.IsFalse(connect.Graph!.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Scalar -> UV broadcast should not raise compile-time errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_ConnectEdge_AllowsVector4OutputIntoUvInput()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_ConnectEdge_Vector4ToUv.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                var sourceSample = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "sampleTexture2D", PositionX = -800f, PositionY = -100f },
                    includeStructure: true);
                var downstreamSample = tool.AddNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddNodeInput { NodeType = "sampleTexture2D", PositionX = -400f, PositionY = -100f },
                    includeStructure: true);

                var sourceRgba = sourceSample.Node!.Slots!.First(s => s.DisplayName == "RGBA");
                var downstreamUv = downstreamSample.Node!.Slots!.First(s => s.DisplayName == "UV");
                Assert.AreEqual("UnityEditor.ShaderGraph.Vector4MaterialSlot", sourceRgba.Type,
                    "Sample Texture 2D RGBA should be a Vector4MaterialSlot.");
                Assert.AreEqual("UnityEditor.ShaderGraph.UVMaterialSlot", downstreamUv.Type,
                    "Sample Texture 2D UV should be a UVMaterialSlot.");

                var connect = tool.ConnectEdge(
                    new AssetObjectRef(shader),
                    new ShaderGraphConnectEdgeInput
                    {
                        OutputNodeObjectId = sourceSample.Node.ObjectId,
                        OutputSlotObjectId = sourceRgba.ObjectId,
                        InputNodeObjectId = downstreamSample.Node.ObjectId,
                        InputSlotObjectId = downstreamUv.ObjectId
                    },
                    includeGraph: true);

                Assert.IsNotNull(connect.Edge, "Edge connection should return a populated Edge diff.");
                Assert.AreEqual(sourceSample.Node.ObjectId, connect.Edge!.OutputNodeId);
                Assert.AreEqual(downstreamSample.Node.ObjectId, connect.Edge.InputNodeId);
                Assert.IsTrue(connect.GraphSummary!.ShaderResolved, "Direct Vector4 -> UV should keep the Shader Graph valid.");
                Assert.IsFalse(connect.GraphSummary.HasErrors, "Direct Vector4 -> UV should not introduce import errors.");
                Assert.IsNotNull(connect.Graph);
                Assert.IsFalse(connect.Graph!.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Direct Vector4 -> UV should not raise compile-time errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        static string CreateShaderGraphAssetCopy(string fileName)
            => CreateShaderGraphAssetCopy(fileName, TemplateAssetPath);

        static string CreateProjectShaderGraphAssetCopy(string fileName, string sourceAssetPath)
        {
            var destinationPath = $"{TestFolder}/{fileName}";
            EnsureFolder(TestFolder);

            var sourceFullPath = Path.GetFullPath(sourceAssetPath);
            Assert.IsTrue(File.Exists(sourceFullPath), $"Expected project Shader Graph source to exist at '{sourceFullPath}'.");

            File.Copy(sourceFullPath, Path.GetFullPath(destinationPath), overwrite: true);
            AssetDatabase.ImportAsset(destinationPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            return destinationPath;
        }

        static void MoveCategoryReferenceToFront(string assetPath, string categoryObjectId)
        {
            var objects = ReadShaderGraphJsonObjects(assetPath);
            var root = objects[0];
            Assert.IsTrue(root["m_CategoryData"] is JsonArray, "Expected Shader Graph root to contain m_CategoryData.");

            var categoryArray = (JsonArray)root["m_CategoryData"]!;
            var categoryIndex = -1;
            for (var i = 0; i < categoryArray.Count; i++)
            {
                if (string.Equals(categoryArray[i]?["m_Id"]?.GetValue<string>(), categoryObjectId, StringComparison.Ordinal))
                {
                    categoryIndex = i;
                    break;
                }
            }

            Assert.GreaterOrEqual(categoryIndex, 0, $"Expected category '{categoryObjectId}' to be referenced by m_CategoryData.");

            categoryArray.RemoveAt(categoryIndex);
            categoryArray.Insert(0, new JsonObject
            {
                ["m_Id"] = categoryObjectId
            });

            var sourceText = string.Join(
                Environment.NewLine + Environment.NewLine,
                objects.Select(obj => obj.ToJsonString(ShaderGraphTestJsonWriteOptions))) + Environment.NewLine;
            File.WriteAllText(assetPath, sourceText);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static List<JsonObject> ReadShaderGraphJsonObjects(string assetPath)
        {
            var objects = new List<JsonObject>();
            foreach (var jsonObject in EnumerateShaderGraphJsonObjects(File.ReadAllText(assetPath)))
            {
                var parsedObject = JsonNode.Parse(jsonObject)?.AsObject();
                Assert.IsNotNull(parsedObject, $"Expected top-level Shader Graph JSON object in '{assetPath}' to parse.");
                objects.Add(parsedObject!);
            }

            Assert.IsNotEmpty(objects, $"Expected Shader Graph source '{assetPath}' to contain JSON objects.");
            return objects;
        }

        static void AddRawEdgeForTest(
            string assetPath,
            ShaderGraphNodeDefinitionData outputNode,
            string outputSlotName,
            ShaderGraphNodeDefinitionData inputNode,
            string inputSlotName)
        {
            var objects = ReadShaderGraphJsonObjects(assetPath);
            var root = objects[0];
            if (root["m_Edges"] is not JsonArray edgesArray)
            {
                edgesArray = new JsonArray();
                root["m_Edges"] = edgesArray;
            }

            var outputSlot = FindSlot(outputNode, outputSlotName);
            var inputSlot = FindSlot(inputNode, inputSlotName);
            edgesArray.Add(new JsonObject
            {
                ["m_OutputSlot"] = new JsonObject
                {
                    ["m_Node"] = new JsonObject
                    {
                        ["m_Id"] = outputNode.ObjectId
                    },
                    ["m_SlotId"] = outputSlot.SlotId
                },
                ["m_InputSlot"] = new JsonObject
                {
                    ["m_Node"] = new JsonObject
                    {
                        ["m_Id"] = inputNode.ObjectId
                    },
                    ["m_SlotId"] = inputSlot.SlotId
                }
            });

            var sourceText = string.Join(
                Environment.NewLine + Environment.NewLine,
                objects.Select(obj => obj.ToJsonString(ShaderGraphTestJsonWriteOptions))) + Environment.NewLine;
            File.WriteAllText(assetPath, sourceText);
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        }

        static IEnumerable<string> EnumerateShaderGraphJsonObjects(string sourceText)
        {
            var depth = 0;
            var startIndex = -1;
            var insideString = false;
            var isEscaped = false;

            for (var i = 0; i < sourceText.Length; i++)
            {
                var ch = sourceText[i];

                if (insideString)
                {
                    if (isEscaped)
                    {
                        isEscaped = false;
                        continue;
                    }

                    if (ch == '\\')
                    {
                        isEscaped = true;
                        continue;
                    }

                    if (ch == '"')
                        insideString = false;

                    continue;
                }

                if (ch == '"')
                {
                    insideString = true;
                    continue;
                }

                if (ch == '{')
                {
                    if (depth == 0)
                        startIndex = i;

                    depth++;
                    continue;
                }

                if (ch != '}')
                    continue;

                depth--;
                if (depth != 0 || startIndex < 0)
                    continue;

                yield return sourceText.Substring(startIndex, i - startIndex + 1);
                startIndex = -1;
            }
        }

        static string CreateShaderGraphAssetCopy(string fileName, string templateAssetPath)
        {
            var destinationPath = $"{TestFolder}/{fileName}";
            EnsureFolder(TestFolder);

            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(templateAssetPath);
            Assert.IsNotNull(packageInfo, $"Expected package info for '{templateAssetPath}'.");

            var packageRoot = $"Packages/{packageInfo!.name}";
            var relativeTemplatePath = templateAssetPath.Substring(packageRoot.Length).TrimStart('/');
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

        static ShaderGraphEdgeMutationResultData ConnectSlots(
            Tool_Assets_ShaderGraph tool,
            Shader shader,
            ShaderGraphNodeDefinitionData outputNode,
            string outputSlotName,
            ShaderGraphNodeDefinitionData inputNode,
            string inputSlotName,
            bool includeStructure = false,
            bool includeGraph = false,
            bool includeMessages = false,
            bool includeProperties = false,
            bool replaceExistingInputConnection = false)
        {
            var outputSlot = FindSlot(outputNode, outputSlotName);
            var inputSlot = FindSlot(inputNode, inputSlotName);

            return tool.ConnectEdge(
                new AssetObjectRef(shader),
                new ShaderGraphConnectEdgeInput
                {
                    OutputNodeObjectId = outputNode.ObjectId,
                    OutputSlotObjectId = outputSlot.ObjectId,
                    InputNodeObjectId = inputNode.ObjectId,
                    InputSlotObjectId = inputSlot.ObjectId,
                    ReplaceExistingInputConnection = replaceExistingInputConnection
                },
                includeStructure: includeStructure,
                includeGraph: includeGraph,
                includeMessages: includeMessages,
                includeProperties: includeProperties);
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

        static void AssertMatrixFirstRowComponent(ShaderGraphNodeDefinitionData node, string displayName, string componentName, float expectedValue)
        {
            var slot = FindSlot(node, displayName);
            AssertVectorComponent(slot.ValueJson, componentName, expectedValue, displayName, "ValueJson");
            AssertVectorComponent(slot.DefaultValueJson, componentName, expectedValue, displayName, "DefaultValueJson");
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
