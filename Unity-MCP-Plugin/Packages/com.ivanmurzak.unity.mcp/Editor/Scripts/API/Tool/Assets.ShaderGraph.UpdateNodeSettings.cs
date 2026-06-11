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
using System.Reflection;
using AIGD;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEditor;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderGraphUpdateNodeSettingsToolId = "assets-shadergraph-update-node-settings";

        [AiTool
        (
            AssetsShaderGraphUpdateNodeSettingsToolId,
            Title = "Assets / Shader Graph / Update Node Settings"
        )]
        [AiSkillDescription("Update supported serialized settings on an existing Shader Graph node, then re-import the graph and return the updated node and diagnostics.")]
        [AiSkillBody("Update supported settings on an existing node inside a '.shadergraph' asset.\n\n" +
            "Current Epic 8 support is intentionally narrow and typed:\n" +
            "- existing nodes only\n" +
            "- selection by `nodeObjectId`\n" +
            "- current supported node family: `Sample Texture 2D`\n" +
            "- supported Sample Texture 2D fields: `textureType`, `normalMapSpace`, `useGlobalMipBias`, `mipSamplingMode`\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `node` — node selector plus typed settings for the supported node family.\n" +
            "- `includeMessages` — include shader compiler messages in returned graph data.\n" +
            "- `includeProperties` — include compiled shader properties in returned graph data.\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect node object ids and current supported node settings.")]
        [Description("Update supported serialized settings on an existing Shader Graph node and re-import the graph.")]
        public ShaderGraphNodeMutationResultData UpdateNodeSettings(
            AssetObjectRef assetRef,
            ShaderGraphUpdateNodeSettingsInput node,
            [Description("Include shader compiler messages in the returned graph data. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Default: false")]
            bool? includeProperties = false)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            if (node == null)
                throw new ArgumentNullException(nameof(node));

            return MainThread.Instance.Run(() => UpdateShaderGraphNodeSettings(
                assetRef,
                node,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphNodeMutationResultData UpdateShaderGraphNodeSettings(
            AssetObjectRef assetRef,
            ShaderGraphUpdateNodeSettingsInput node,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var nodeObjectId = node.NodeObjectId?.Trim();
            if (string.IsNullOrEmpty(nodeObjectId))
                throw new ArgumentException("node.nodeObjectId must be provided.", nameof(node));

            if (!HasAnyNodeSettingsUpdates(node))
                throw new ArgumentException("At least one supported node settings field must be provided.", nameof(node));

            var document = LoadShaderGraphReflectionDocument(assetPath);
            var nodeObject = ResolveShaderGraphNodeObject(document, nodeObjectId);
            var changedFields = new List<string>();

            if (node.SampleTexture2D != null)
                ApplySampleTexture2DNodeSettings(nodeObject, node.SampleTexture2D, changedFields);

            if (changedFields.Count == 0)
                throw new InvalidOperationException($"Shader Graph node '{nodeObjectId}' did not change.");

            InvokeShaderGraphMethod(document.Bindings.ValidateGraphMethod, document.GraphData);
            SaveShaderGraphReflectionDocument(document);
            FinalizeShaderGraphMutation(assetPath);

            var graphRef = new AssetObjectRef(assetPath);
            var structure = BuildShaderGraphStructureData(graphRef);
            var updatedNode = structure.Nodes?
                .FirstOrDefault(n => string.Equals(n.ObjectId, nodeObjectId, StringComparison.Ordinal));

            return new ShaderGraphNodeMutationResultData
            {
                ChangedFields = changedFields,
                Node = updatedNode,
                Structure = structure,
                Graph = BuildShaderGraphData(
                    graphRef,
                    includeMessages: includeMessages,
                    includeProperties: includeProperties,
                    includeDiagnostics: true)
            };
        }

        static bool HasAnyNodeSettingsUpdates(ShaderGraphUpdateNodeSettingsInput node)
            => HasSampleTexture2DUpdates(node.SampleTexture2D);

        static bool HasSampleTexture2DUpdates(ShaderGraphSampleTexture2DNodeSettingsUpdateInput? sampleTexture2D)
            => sampleTexture2D != null
               && (!string.IsNullOrWhiteSpace(sampleTexture2D.TextureType)
                   || !string.IsNullOrWhiteSpace(sampleTexture2D.NormalMapSpace)
                   || sampleTexture2D.UseGlobalMipBias.HasValue
                   || !string.IsNullOrWhiteSpace(sampleTexture2D.MipSamplingMode));

        static void ApplySampleTexture2DNodeSettings(
            object nodeObject,
            ShaderGraphSampleTexture2DNodeSettingsUpdateInput sampleTexture2D,
            List<string> changedFields)
        {
            var nodeType = nodeObject.GetType();
            if (!string.Equals(nodeType.FullName, "UnityEditor.ShaderGraph.SampleTexture2DNode", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Node '{nodeType.FullName}' does not support sampleTexture2D settings updates. Expected UnityEditor.ShaderGraph.SampleTexture2DNode.");
            }

            if (!string.IsNullOrWhiteSpace(sampleTexture2D.TextureType))
            {
                SetEnumProperty(
                    nodeObject,
                    nodeType,
                    "textureType",
                    sampleTexture2D.TextureType!,
                    "node.sampleTexture2D.textureType",
                    changedFields);
            }

            if (!string.IsNullOrWhiteSpace(sampleTexture2D.NormalMapSpace))
            {
                SetEnumProperty(
                    nodeObject,
                    nodeType,
                    "normalMapSpace",
                    sampleTexture2D.NormalMapSpace!,
                    "node.sampleTexture2D.normalMapSpace",
                    changedFields);
            }

            if (sampleTexture2D.UseGlobalMipBias.HasValue)
            {
                SetBoolProperty(
                    nodeObject,
                    nodeType,
                    "enableGlobalMipBias",
                    sampleTexture2D.UseGlobalMipBias.Value,
                    "node.sampleTexture2D.useGlobalMipBias",
                    changedFields);
            }

            if (!string.IsNullOrWhiteSpace(sampleTexture2D.MipSamplingMode))
            {
                SetEnumProperty(
                    nodeObject,
                    nodeType,
                    "mipSamplingMode",
                    sampleTexture2D.MipSamplingMode!,
                    "node.sampleTexture2D.mipSamplingMode",
                    changedFields);
            }
        }

        static void SetEnumProperty(
            object target,
            Type targetType,
            string propertyName,
            string value,
            string changedFieldName,
            List<string> changedFields)
        {
            var property = ResolveNodeInstanceProperty(targetType, propertyName);
            var enumType = property.PropertyType;
            if (!enumType.IsEnum)
                throw new InvalidOperationException($"Property '{targetType.FullName}.{propertyName}' is not an enum.");

            var parsedValue = ParseNodeEnumValue(enumType, value, changedFieldName);
            var currentValue = property.GetValue(target);
            if (Equals(currentValue, parsedValue))
                return;

            property.SetValue(target, parsedValue);
            changedFields.Add(changedFieldName);
        }

        static void SetBoolProperty(
            object target,
            Type targetType,
            string propertyName,
            bool value,
            string changedFieldName,
            List<string> changedFields)
        {
            var property = ResolveNodeInstanceProperty(targetType, propertyName);
            if (property.PropertyType != typeof(bool))
                throw new InvalidOperationException($"Property '{targetType.FullName}.{propertyName}' is not a bool.");

            var currentValue = property.GetValue(target) is bool boolValue
                ? boolValue
                : (bool?)null;
            if (currentValue == value)
                return;

            property.SetValue(target, value);
            changedFields.Add(changedFieldName);
        }

        static PropertyInfo ResolveNodeInstanceProperty(Type targetType, string propertyName)
        {
            var property = targetType.GetProperty(
                propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            return property
                ?? throw new InvalidOperationException(
                    $"Property '{targetType.FullName}.{propertyName}' could not be resolved.");
        }

        static object ParseNodeEnumValue(Type enumType, string value, string fieldPath)
        {
            var normalizedValue = NormalizeEnumValue(value);
            foreach (var enumName in Enum.GetNames(enumType))
            {
                if (string.Equals(NormalizeEnumValue(enumName), normalizedValue, StringComparison.Ordinal))
                    return Enum.Parse(enumType, enumName, ignoreCase: false);
            }

            var supportedValues = string.Join(", ", Enum.GetNames(enumType)
                .Select(enumName => NormalizeEnumValue(enumName))
                .OrderBy(enumName => enumName, StringComparer.Ordinal));

            throw new ArgumentException(
                $"Unsupported value '{value}' for {fieldPath}. Supported values: {supportedValues}.");
        }
    }
}
