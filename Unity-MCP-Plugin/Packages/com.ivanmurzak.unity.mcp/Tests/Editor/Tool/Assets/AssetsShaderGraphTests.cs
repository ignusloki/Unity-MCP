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
    }
}
