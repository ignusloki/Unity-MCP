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
using AIGD;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using com.IvanMurzak.Unity.MCP.Editor.Utils;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderGraphValidateTextureWorkflowToolId = "assets-shadergraph-validate-texture-workflow";

        [AiTool
        (
            AssetsShaderGraphValidateTextureWorkflowToolId,
            Title = "Assets / Shader Graph / Validate Texture Workflow"
        )]
        [AiSkillDescription("Create or overwrite a Material from a Shader Graph, then validate graph texture references and material texture-property readback.")]
        [AiSkillBody("Validate texture behavior for a Shader Graph asset after texture assignment. " +
            "The tool creates or overwrites a Material from the graph's compiled Shader, optionally copies blackboard Texture2D defaults into matching material texture properties, and returns graph texture references, material texture properties, shader diagnostics, and optional expectation results.\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `materialAssetPath` — destination material path under `Assets/` ending in `.mat`.\n" +
            "- `overwrite` — when true, replace an existing material asset.\n" +
            "- `applyGraphTextureDefaultsToMaterial` — when true, copy graph blackboard Texture2D asset references into matching material texture properties such as `_BaseMap`.\n" +
            "- `expectedGraphTextureAssetPaths` — optional project texture paths expected in graph source references.\n" +
            "- `expectedMaterialTextures` — optional `{ propertyName, textureAssetPath }` material texture expectations.\n\n" +
            "Direct unconnected Sample Texture 2D slot textures are graph-embedded references, not material properties; validate those through `graphTextureReferences`.")]
        [Description("Create or overwrite a Material from a Shader Graph, then validate graph texture references and material texture-property readback.")]
        public ShaderGraphTextureWorkflowValidationResultData ValidateTextureWorkflow
        (
            [Description("Reference to a '.shadergraph' asset.")]
            AssetObjectRef assetRef,
            [Description("Destination material asset path. Must start with 'Assets/' and end with '.mat'.")]
            string materialAssetPath,
            [Description("When true, replace an existing destination material. Default: false.")]
            bool? overwrite = false,
            [Description("When true, copy Shader Graph blackboard Texture2D defaults into matching material texture properties. Default: true.")]
            bool? applyGraphTextureDefaultsToMaterial = true,
            [Description("Optional project texture asset paths expected to appear in Shader Graph source texture references.")]
            string[]? expectedGraphTextureAssetPaths = null,
            [Description("Optional material texture property expectations to validate after material creation.")]
            ShaderGraphExpectedMaterialTextureInput[]? expectedMaterialTextures = null,
            [Description("Include full Shader Graph structure in the result. Default: true.")]
            bool? includeStructure = true
        )
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            return MainThread.Instance.Run(() =>
            {
                var graphAssetPath = ResolveAssetPath(assetRef);
                if (!IsShaderGraphAssetPath(graphAssetPath))
                    throw new ArgumentException(Error.AssetIsNotShaderGraph(graphAssetPath), nameof(assetRef));

                var shader = AssetDatabase.LoadAssetAtPath<Shader>(graphAssetPath);
                if (shader == null)
                    throw new Exception(Error.FailedToLoadShaderGraphShader(graphAssetPath));

                var materialReference = Tool_Assets.CreateMaterialAsset(
                    assetPath: materialAssetPath,
                    shader: shader,
                    overwrite: overwrite ?? false);

                var material = AssetDatabase.LoadAssetAtPath<Material>(materialAssetPath);
                if (material == null)
                    throw new Exception($"Material asset was not created at path '{materialAssetPath}'.");

                var graphReference = new AssetObjectRef(shader);
                var structure = BuildShaderGraphStructureData(graphReference);
                var graphTextureReferences = CollectGraphTextureReferences(structure);
                var appliedMaterialTexturePropertyNames = new List<string>();

                if (applyGraphTextureDefaultsToMaterial ?? true)
                {
                    ApplyGraphTextureDefaultsToMaterial(material, graphTextureReferences, appliedMaterialTexturePropertyNames);
                    if (appliedMaterialTexturePropertyNames.Count > 0)
                    {
                        EditorUtility.SetDirty(material);
                        AssetDatabase.SaveAssets();
                        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    }
                }

                var materialTextureProperties = CollectMaterialTextureProperties(
                    material,
                    appliedMaterialTexturePropertyNames);
                var expectations = BuildTextureExpectationResults(
                    expectedGraphTextureAssetPaths,
                    expectedMaterialTextures,
                    graphTextureReferences,
                    materialTextureProperties);
                var graph = BuildShaderGraphData(
                    graphReference,
                    includeMessages: true,
                    includeProperties: true,
                    includeDiagnostics: true);

                EditorUtils.RepaintAllEditorWindows();

                return new ShaderGraphTextureWorkflowValidationResultData
                {
                    GraphReference = graphReference,
                    GraphAssetPath = graphAssetPath,
                    MaterialReference = materialReference,
                    MaterialAssetPath = materialAssetPath,
                    GraphShaderName = shader.name,
                    MaterialShaderName = material.shader != null ? material.shader.name : null,
                    ShaderMatchesGraph = material.shader == shader,
                    AppliedGraphTextureDefaultsToMaterial = applyGraphTextureDefaultsToMaterial ?? true,
                    AppliedMaterialTexturePropertyNames = appliedMaterialTexturePropertyNames,
                    GraphTextureReferences = graphTextureReferences,
                    MaterialTextureProperties = materialTextureProperties,
                    Expectations = expectations,
                    AllExpectationsMatched = expectations.All(expectation => expectation.Matched),
                    Structure = (includeStructure ?? true) ? structure : null,
                    Graph = graph
                };
            });
        }

        static List<ShaderGraphTextureReferenceData> CollectGraphTextureReferences(ShaderGraphStructureData structure)
        {
            var references = new List<ShaderGraphTextureReferenceData>();

            foreach (var property in structure.Properties ?? Enumerable.Empty<ShaderGraphPropertyDefinitionData>())
            {
                if (!string.Equals(property.PropertyKind, "texture2D", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (string.IsNullOrWhiteSpace(property.TextureAssetPath))
                    continue;

                references.Add(new ShaderGraphTextureReferenceData
                {
                    SourceKind = "blackboardProperty",
                    OwnerObjectId = property.ObjectId,
                    OwnerName = property.Name,
                    PropertyReferenceName = property.EffectiveReferenceName,
                    TextureAssetGuid = property.TextureAssetGuid,
                    TextureAssetPath = property.TextureAssetPath,
                    TextureDefaultType = property.TextureDefaultType
                });
            }

            foreach (var node in structure.Nodes ?? Enumerable.Empty<ShaderGraphNodeDefinitionData>())
            {
                foreach (var slot in node.Slots ?? Enumerable.Empty<ShaderGraphSlotDefinitionData>())
                {
                    if (!string.Equals(slot.Type, "UnityEditor.ShaderGraph.Texture2DInputMaterialSlot", StringComparison.Ordinal))
                        continue;

                    if (string.IsNullOrWhiteSpace(slot.TextureAssetPath))
                        continue;

                    references.Add(new ShaderGraphTextureReferenceData
                    {
                        SourceKind = "nodeSlot",
                        OwnerObjectId = node.ObjectId,
                        OwnerName = node.Name,
                        SlotId = slot.SlotId,
                        SlotName = slot.DisplayName,
                        TextureAssetGuid = slot.TextureAssetGuid,
                        TextureAssetPath = slot.TextureAssetPath,
                        TextureDefaultType = slot.TextureDefaultType
                    });
                }
            }

            return references;
        }

        static void ApplyGraphTextureDefaultsToMaterial(
            Material material,
            List<ShaderGraphTextureReferenceData> graphTextureReferences,
            List<string> appliedMaterialTexturePropertyNames)
        {
            foreach (var reference in graphTextureReferences)
            {
                if (!string.Equals(reference.SourceKind, "blackboardProperty", StringComparison.Ordinal))
                    continue;

                if (string.IsNullOrWhiteSpace(reference.PropertyReferenceName)
                    || string.IsNullOrWhiteSpace(reference.TextureAssetPath))
                    continue;

                if (!material.HasTexture(reference.PropertyReferenceName))
                    continue;

                var texture = AssetDatabase.LoadAssetAtPath<Texture>(reference.TextureAssetPath);
                if (texture == null)
                    continue;

                material.SetTexture(reference.PropertyReferenceName, texture);
                appliedMaterialTexturePropertyNames.Add(reference.PropertyReferenceName);
            }
        }

        static List<ShaderGraphMaterialTexturePropertyData> CollectMaterialTextureProperties(
            Material material,
            List<string> appliedMaterialTexturePropertyNames)
        {
            var properties = new List<ShaderGraphMaterialTexturePropertyData>();
            var shader = material.shader;
            if (shader == null)
                return properties;

            var propertyCount = shader.GetPropertyCount();
            for (var i = 0; i < propertyCount; i++)
            {
                if (shader.GetPropertyType(i) != ShaderPropertyType.Texture)
                    continue;

                var propertyName = shader.GetPropertyName(i);
                var texture = material.GetTexture(propertyName);
                var textureAssetPath = texture != null ? AssetDatabase.GetAssetPath(texture) : null;
                var textureAssetGuid = string.IsNullOrWhiteSpace(textureAssetPath)
                    ? null
                    : AssetDatabase.AssetPathToGUID(textureAssetPath);

                properties.Add(new ShaderGraphMaterialTexturePropertyData
                {
                    PropertyName = propertyName,
                    Description = shader.GetPropertyDescription(i),
                    Flags = shader.GetPropertyFlags(i).ToString(),
                    DefaultTextureName = shader.GetPropertyTextureDefaultName(i),
                    HasTexture = texture != null,
                    TextureAssetGuid = string.IsNullOrWhiteSpace(textureAssetGuid) ? null : textureAssetGuid,
                    TextureAssetPath = string.IsNullOrWhiteSpace(textureAssetPath) ? null : textureAssetPath,
                    WasAppliedFromGraph = appliedMaterialTexturePropertyNames.Contains(propertyName)
                });
            }

            return properties;
        }

        static List<ShaderGraphTextureExpectationResultData> BuildTextureExpectationResults(
            string[]? expectedGraphTextureAssetPaths,
            ShaderGraphExpectedMaterialTextureInput[]? expectedMaterialTextures,
            List<ShaderGraphTextureReferenceData> graphTextureReferences,
            List<ShaderGraphMaterialTexturePropertyData> materialTextureProperties)
        {
            var expectations = new List<ShaderGraphTextureExpectationResultData>();

            foreach (var expectedPath in expectedGraphTextureAssetPaths ?? Array.Empty<string>())
            {
                var normalizedExpectedPath = NormalizeAssetPath(expectedPath);
                var matchingReference = graphTextureReferences.FirstOrDefault(reference =>
                    string.Equals(NormalizeAssetPath(reference.TextureAssetPath), normalizedExpectedPath, StringComparison.OrdinalIgnoreCase));

                expectations.Add(new ShaderGraphTextureExpectationResultData
                {
                    Scope = "graph",
                    ExpectedTextureAssetPath = expectedPath,
                    ExpectedTextureAssetGuid = AssetGuidOrNull(expectedPath),
                    ActualTextureAssetPath = matchingReference?.TextureAssetPath,
                    ActualTextureAssetGuid = matchingReference?.TextureAssetGuid,
                    Matched = matchingReference != null
                });
            }

            foreach (var expected in expectedMaterialTextures ?? Array.Empty<ShaderGraphExpectedMaterialTextureInput>())
            {
                var matchingProperty = materialTextureProperties.FirstOrDefault(property =>
                    string.Equals(property.PropertyName, expected.PropertyName, StringComparison.Ordinal));
                var normalizedExpectedPath = NormalizeAssetPath(expected.TextureAssetPath);
                var normalizedActualPath = NormalizeAssetPath(matchingProperty?.TextureAssetPath);

                expectations.Add(new ShaderGraphTextureExpectationResultData
                {
                    Scope = "material",
                    PropertyName = expected.PropertyName,
                    ExpectedTextureAssetPath = expected.TextureAssetPath,
                    ExpectedTextureAssetGuid = AssetGuidOrNull(expected.TextureAssetPath),
                    ActualTextureAssetPath = matchingProperty?.TextureAssetPath,
                    ActualTextureAssetGuid = matchingProperty?.TextureAssetGuid,
                    Matched = !string.IsNullOrWhiteSpace(normalizedExpectedPath)
                              && string.Equals(normalizedActualPath, normalizedExpectedPath, StringComparison.OrdinalIgnoreCase)
                });
            }

            return expectations;
        }

        static string? AssetGuidOrNull(string? assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return null;

            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            return string.IsNullOrWhiteSpace(guid) ? null : guid;
        }

        static string? NormalizeAssetPath(string? assetPath)
            => string.IsNullOrWhiteSpace(assetPath)
                ? null
                : assetPath.Trim().Replace('\\', '/');
    }
}
