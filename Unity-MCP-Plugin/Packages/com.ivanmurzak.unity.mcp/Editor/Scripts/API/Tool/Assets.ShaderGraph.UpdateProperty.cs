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
using System.Text.Json.Nodes;
using AIGD;
using com.IvanMurzak.McpPlugin;
using com.IvanMurzak.ReflectorNet.Utils;
using UnityEngine;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderGraphUpdatePropertyToolId = "assets-shadergraph-update-property";

        [AiTool
        (
            AssetsShaderGraphUpdatePropertyToolId,
            Title = "Assets / Shader Graph / Update Property"
        )]
        [AiSkillDescription("Update an existing Shader Graph blackboard property by object id or reference name, then re-import the graph and return the updated property and diagnostics.")]
        [AiSkillBody("Update an existing Shader Graph blackboard property on a '.shadergraph' asset.\n\n" +
            "Current support is intentionally narrow and safe:\n" +
            "- generic property fields: display name, override reference name, hidden, generate property block\n" +
            "- color property default value via `colorHex`\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `property` — selector plus requested updates.\n" +
            "- `includeMessages` — include shader compiler messages in returned graph data.\n" +
            "- `includeProperties` — include compiled shader properties in returned graph data.\n\n" +
            "Use `assets-shadergraph-get-structure` first to inspect property object ids, types, and effective reference names.")]
        [Description("Update an existing Shader Graph blackboard property and re-import the graph.")]
        public ShaderGraphPropertyMutationResultData UpdateProperty(
            AssetObjectRef assetRef,
            ShaderGraphPropertyUpdateInput property,
            [Description("Include shader compiler messages in the returned graph data. Default: false")]
            bool? includeMessages = false,
            [Description("Include compiled shader properties in the returned graph data. Default: false")]
            bool? includeProperties = false)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            if (property == null)
                throw new ArgumentNullException(nameof(property));

            return MainThread.Instance.Run(() => UpdateShaderGraphProperty(
                assetRef,
                property,
                includeMessages: includeMessages ?? false,
                includeProperties: includeProperties ?? false));
        }

        static ShaderGraphPropertyMutationResultData UpdateShaderGraphProperty(
            AssetObjectRef assetRef,
            ShaderGraphPropertyUpdateInput property,
            bool includeMessages,
            bool includeProperties)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            if (!HasAnyPropertyUpdates(property))
                throw new ArgumentException("At least one property field must be specified.", nameof(property));

            var document = LoadMutableDocument(assetPath);
            var propertyIds = GetIdArray(document.Root, "m_Properties");
            var propertyObjects = propertyIds
                .Where(document.ObjectsById.ContainsKey)
                .Select(id => document.ObjectsById[id])
                .ToList();

            var propertyObject = ResolvePropertyObject(property, propertyObjects);
            var changedFields = new List<string>();

            ApplyGenericPropertyUpdates(propertyObject, property, propertyObjects, changedFields);
            ApplyTypedPropertyUpdates(propertyObject, property, changedFields);

            if (changedFields.Count > 0)
            {
                WriteMutableDocument(document);
                UnityEditor.AssetDatabase.ImportAsset(assetPath, UnityEditor.ImportAssetOptions.ForceSynchronousImport);
                UnityEditor.AssetDatabase.SaveAssets();
                UnityEditor.AssetDatabase.Refresh(UnityEditor.ImportAssetOptions.ForceSynchronousImport);
                com.IvanMurzak.Unity.MCP.Editor.Utils.EditorUtils.RepaintAllEditorWindows();
            }

            var graphRef = new AssetObjectRef(assetPath);
            var structure = BuildShaderGraphStructureData(graphRef);
            var updatedPropertyId = GetString(propertyObject, "m_ObjectId");
            var updatedProperty = structure.Properties?
                .FirstOrDefault(p => string.Equals(p.ObjectId, updatedPropertyId, StringComparison.Ordinal));

            return new ShaderGraphPropertyMutationResultData
            {
                ChangedFields = changedFields,
                Property = updatedProperty,
                Structure = structure,
                Graph = BuildShaderGraphData(
                    graphRef,
                    includeMessages: includeMessages,
                    includeProperties: includeProperties,
                    includeDiagnostics: true)
            };
        }

        static JsonObject ResolvePropertyObject(
            ShaderGraphPropertyUpdateInput property,
            List<JsonObject> propertyObjects)
        {
            var objectId = property.PropertyObjectId?.Trim();
            var referenceName = property.PropertyReferenceName?.Trim();

            if (string.IsNullOrEmpty(objectId) && string.IsNullOrEmpty(referenceName))
            {
                throw new ArgumentException(
                    "Either propertyObjectId or propertyReferenceName must be provided.");
            }

            JsonObject? resolved = null;

            if (!string.IsNullOrEmpty(objectId))
            {
                resolved = propertyObjects.FirstOrDefault(obj =>
                    string.Equals(GetString(obj, "m_ObjectId"), objectId, StringComparison.Ordinal));
            }
            else
            {
                resolved = propertyObjects.FirstOrDefault(obj =>
                    string.Equals(GetEffectivePropertyReferenceName(obj), referenceName, StringComparison.Ordinal));
            }

            if (resolved == null)
            {
                throw new InvalidOperationException(
                    $"Shader Graph property was not found. objectId='{objectId ?? string.Empty}', referenceName='{referenceName ?? string.Empty}'.");
            }

            return resolved;
        }

        static bool HasAnyPropertyUpdates(ShaderGraphPropertyUpdateInput property)
            => !string.IsNullOrWhiteSpace(property.DisplayName)
               || !string.IsNullOrWhiteSpace(property.OverrideReferenceName)
               || property.Hidden.HasValue
               || property.GeneratePropertyBlock.HasValue
               || !string.IsNullOrWhiteSpace(property.ColorHex);

        static void ApplyGenericPropertyUpdates(
            JsonObject propertyObject,
            ShaderGraphPropertyUpdateInput property,
            List<JsonObject> allPropertyObjects,
            List<string> changedFields)
        {
            if (!string.IsNullOrWhiteSpace(property.DisplayName))
            {
                var displayName = property.DisplayName!.Trim();
                ValidateUniqueDisplayName(propertyObject, allPropertyObjects, displayName);
                SetString(propertyObject, "m_Name", displayName, "property.displayName", changedFields);
                SetString(propertyObject, "m_RefNameGeneratedByDisplayName", displayName, "property.refNameGeneratedByDisplayName", changedFields);

                var overrideReferenceName = GetString(propertyObject, "m_OverrideReferenceName");
                if (string.IsNullOrEmpty(overrideReferenceName))
                {
                    var generatedDefaultReferenceName = GenerateDefaultReferenceName(displayName);
                    ValidateUniqueReferenceName(propertyObject, allPropertyObjects, generatedDefaultReferenceName);
                    SetString(
                        propertyObject,
                        "m_DefaultReferenceName",
                        generatedDefaultReferenceName,
                        "property.defaultReferenceName",
                        changedFields);
                }
            }

            if (!string.IsNullOrWhiteSpace(property.OverrideReferenceName))
            {
                var overrideReferenceName = property.OverrideReferenceName!.Trim();
                ValidateReferenceNameSyntax(overrideReferenceName);
                ValidateUniqueReferenceName(propertyObject, allPropertyObjects, overrideReferenceName);
                SetString(
                    propertyObject,
                    "m_OverrideReferenceName",
                    overrideReferenceName,
                    "property.overrideReferenceName",
                    changedFields);
            }

            if (property.Hidden.HasValue)
            {
                SetBool(
                    propertyObject,
                    "m_Hidden",
                    property.Hidden.Value,
                    "property.hidden",
                    changedFields);
            }

            if (property.GeneratePropertyBlock.HasValue)
            {
                SetBool(
                    propertyObject,
                    "m_GeneratePropertyBlock",
                    property.GeneratePropertyBlock.Value,
                    "property.generatePropertyBlock",
                    changedFields);
            }
        }

        static void ApplyTypedPropertyUpdates(
            JsonObject propertyObject,
            ShaderGraphPropertyUpdateInput property,
            List<string> changedFields)
        {
            if (string.IsNullOrWhiteSpace(property.ColorHex))
                return;

            var propertyType = GetString(propertyObject, "m_Type");
            if (!string.Equals(propertyType, "UnityEditor.ShaderGraph.Internal.ColorShaderProperty", StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"colorHex is only supported for ColorShaderProperty. Property type: '{propertyType ?? "null"}'.");
            }

            if (!ColorUtility.TryParseHtmlString(property.ColorHex!.Trim(), out var color))
                throw new ArgumentException($"Invalid colorHex '{property.ColorHex}'. Expected formats like '#RRGGBB' or '#RRGGBBAA'.");

            if (propertyObject["m_Value"] is not JsonObject colorValue)
                throw new InvalidOperationException("Color property is missing its serialized m_Value object.");

            SetFloat(colorValue, "r", color.r, "property.color.r", changedFields);
            SetFloat(colorValue, "g", color.g, "property.color.g", changedFields);
            SetFloat(colorValue, "b", color.b, "property.color.b", changedFields);
            SetFloat(colorValue, "a", color.a, "property.color.a", changedFields);
        }

        static void ValidateUniqueDisplayName(
            JsonObject propertyObject,
            List<JsonObject> allPropertyObjects,
            string displayName)
        {
            var duplicate = allPropertyObjects.Any(other =>
                !ReferenceEquals(other, propertyObject)
                && string.Equals(GetString(other, "m_Name"), displayName, StringComparison.OrdinalIgnoreCase));

            if (duplicate)
                throw new InvalidOperationException($"Another Shader Graph property already uses display name '{displayName}'.");
        }

        static void ValidateUniqueReferenceName(
            JsonObject propertyObject,
            List<JsonObject> allPropertyObjects,
            string referenceName)
        {
            var duplicate = allPropertyObjects.Any(other =>
                !ReferenceEquals(other, propertyObject)
                && string.Equals(GetEffectivePropertyReferenceName(other), referenceName, StringComparison.OrdinalIgnoreCase));

            if (duplicate)
                throw new InvalidOperationException($"Another Shader Graph property already uses reference name '{referenceName}'.");
        }

        static void ValidateReferenceNameSyntax(string referenceName)
        {
            if (string.IsNullOrWhiteSpace(referenceName))
                throw new ArgumentException("overrideReferenceName must not be empty.");

            if (!(char.IsLetter(referenceName[0]) || referenceName[0] == '_'))
            {
                throw new ArgumentException(
                    $"overrideReferenceName '{referenceName}' must start with a letter or underscore.");
            }

            for (var i = 1; i < referenceName.Length; i++)
            {
                var ch = referenceName[i];
                if (!(char.IsLetterOrDigit(ch) || ch == '_'))
                {
                    throw new ArgumentException(
                        $"overrideReferenceName '{referenceName}' must contain only letters, digits, or underscores.");
                }
            }
        }

        static string GetEffectivePropertyReferenceName(JsonObject propertyObject)
        {
            return GetString(propertyObject, "m_OverrideReferenceName")
                ?? GetString(propertyObject, "m_DefaultReferenceName")
                ?? string.Empty;
        }

        static string GenerateDefaultReferenceName(string displayName)
        {
            var sanitizedChars = displayName
                .Trim()
                .Select(ch => char.IsLetterOrDigit(ch) ? ch : '_')
                .ToArray();

            var sanitized = new string(sanitizedChars).Trim('_');
            if (string.IsNullOrEmpty(sanitized))
                sanitized = "Property";

            if (char.IsDigit(sanitized[0]))
                sanitized = "_" + sanitized;

            return sanitized.StartsWith("_", StringComparison.Ordinal)
                ? sanitized
                : "_" + sanitized;
        }

        static void SetFloat(
            JsonObject root,
            string propertyName,
            float value,
            string changedFieldName,
            List<string> changedFields)
        {
            var currentValue = GetFloat(root, propertyName);
            if (currentValue.HasValue && Mathf.Abs(currentValue.Value - value) <= 0.0001f)
                return;

            root[propertyName] = value;
            changedFields.Add(changedFieldName);
        }

        static float? GetFloat(JsonObject root, string propertyName)
        {
            if (root[propertyName] is not JsonValue propertyValue)
                return null;

            if (propertyValue.TryGetValue<float>(out var floatValue))
                return floatValue;

            if (propertyValue.TryGetValue<double>(out var doubleValue))
                return (float)doubleValue;

            return null;
        }
    }
}
