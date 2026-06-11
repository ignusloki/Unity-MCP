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
                Assert.IsFalse(result.UniversalTarget.AlphaClip ?? true);
                Assert.IsTrue(result.UniversalTarget.CastShadows ?? false);
                Assert.IsTrue(result.UniversalTarget.ReceiveShadows ?? false);

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
                            AlphaClip = true,
                            CastShadows = false,
                            ReceiveShadows = false
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
                Assert.IsTrue(result.ChangedFields.Contains("universalTarget.alphaClip"));

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
                Assert.IsTrue(result.Settings.UniversalTarget.AlphaClip ?? false);
                Assert.IsFalse(result.Settings.UniversalTarget.CastShadows ?? true);
                Assert.IsFalse(result.Settings.UniversalTarget.ReceiveShadows ?? true);

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
                        OverrideReferenceName = "_DiffuseTex"
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
        public void ShaderGraph_AddPropertyNode_AddsColorAndFloatPropertyNodes()
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

                Assert.IsNotNull(colorNodeResult);
                Assert.IsNotNull(colorNodeResult.Node);
                Assert.AreEqual("UnityEditor.ShaderGraph.PropertyNode", colorNodeResult.Node!.Type);
                Assert.AreEqual("_BaseColor", colorNodeResult.Node.PropertyReferenceName);
                Assert.AreEqual(-720f, colorNodeResult.Node.PositionX);
                Assert.AreEqual(160f, colorNodeResult.Node.PositionY);
                Assert.IsNotEmpty(colorNodeResult.Node.Slots);
                Assert.AreEqual("Color", colorNodeResult.Node.Slots![0].DisplayName);

                Assert.IsNotNull(floatNodeResult);
                Assert.IsNotNull(floatNodeResult.Node);
                Assert.AreEqual("UnityEditor.ShaderGraph.PropertyNode", floatNodeResult.Node!.Type);
                Assert.AreEqual("_GlowStrength", floatNodeResult.Node.PropertyReferenceName);
                Assert.AreEqual(260f, floatNodeResult.Node.PositionY);
                Assert.IsNotEmpty(floatNodeResult.Node.Slots);
                Assert.AreEqual("Glow Strength", floatNodeResult.Node.Slots![0].DisplayName);
                Assert.AreEqual("UnityEditor.ShaderGraph.Vector1MaterialSlot", floatNodeResult.Node.Slots[0].Type);

                Assert.IsNotNull(floatNodeResult.Structure);
                Assert.IsTrue(floatNodeResult.Structure!.Nodes!.Count >= 4,
                    "The graph should contain the original nodes plus the added Property nodes.");
                Assert.IsTrue(floatNodeResult.Structure.Nodes.Any(n => n.PropertyReferenceName == "_BaseColor"));
                Assert.IsTrue(floatNodeResult.Structure.Nodes.Any(n => n.PropertyReferenceName == "_GlowStrength"));

                Assert.IsNotNull(floatNodeResult.Graph);
                Assert.IsTrue(floatNodeResult.Graph!.ShaderResolved, "Updated Shader Graph should still resolve a compiled shader.");
                Assert.IsFalse(floatNodeResult.Graph.Diagnostics!.Any(d => d.Severity == "Error"),
                    "Adding safe Property nodes should not introduce import errors.");
            }
            finally
            {
                CleanupTestAsset(assetPath);
            }
        }

        [Test]
        public void ShaderGraph_AddPropertyNode_UnsupportedPropertyType_Throws()
        {
            var assetPath = CreateShaderGraphAssetCopy("Validation_AddPropertyNode_Unsupported.shadergraph");
            try
            {
                var shader = AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
                Assert.IsNotNull(shader, $"Expected Shader asset to resolve at '{assetPath}'.");

                var tool = new Tool_Assets_ShaderGraph();
                Assert.Throws<InvalidOperationException>(() => tool.AddPropertyNode(
                    new AssetObjectRef(shader),
                    new ShaderGraphAddPropertyNodeInput
                    {
                        PropertyReferenceName = "_BaseMap",
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
                Assert.IsTrue(sampleTextureNodeResult.ChangedFields!.Contains("node.added"));
                Assert.IsNotNull(sampleTextureNodeResult.Node);
                Assert.AreEqual("UnityEditor.ShaderGraph.SampleTexture2DNode", sampleTextureNodeResult.Node!.Type);
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
                Assert.IsTrue(deleteResult.ChangedFields!.Contains("node.deleted"));
                Assert.IsTrue(deleteResult.ChangedFields.Contains("edge.autoRemoved"),
                    "Deleting a connected node should report that edges were cleaned up automatically.");
                Assert.IsNotNull(deleteResult.Node);
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
                Assert.IsNotNull(movedColorNode.Node);
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
