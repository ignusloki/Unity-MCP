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
