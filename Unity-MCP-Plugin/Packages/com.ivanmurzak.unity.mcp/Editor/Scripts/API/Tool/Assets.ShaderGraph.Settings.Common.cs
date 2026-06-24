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
using System.Text.Json;
using System.Text.Json.Nodes;
using AIGD;
using com.IvanMurzak.Unity.MCP.Editor.Utils;
using com.IvanMurzak.Unity.MCP.Runtime.Extensions;
using UnityEditor;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        const string UniversalTargetTypeName =
            "UnityEditor.Rendering.Universal.ShaderGraph.UniversalTarget";

        static readonly JsonSerializerOptions ShaderGraphJsonWriteOptions = new()
        {
            WriteIndented = true
        };

        sealed class ShaderGraphMutableDocument
        {
            public string AssetPath { get; set; } = string.Empty;
            public string FullPath { get; set; } = string.Empty;
            public List<JsonObject> Objects { get; } = new();
            public Dictionary<string, JsonObject> ObjectsById { get; } = new(StringComparer.Ordinal);
            public JsonObject Root => Objects[0];
        }

        internal static ShaderGraphSettingsData BuildShaderGraphSettingsData(AssetObjectRef assetRef)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var asset = assetRef.FindAssetObject();
            var resolvedAsset = asset ?? AssetDatabase.LoadMainAssetAtPath(assetPath);
            var data = ReadSettingsData(assetPath);
            data.Reference = resolvedAsset == null ? null : new AssetObjectRef(resolvedAsset);
            return data;
        }

        internal static ShaderGraphSettingsMutationResultData UpdateShaderGraphSettings(
            AssetObjectRef assetRef,
            ShaderGraphSettingsUpdateInput settings,
            bool includeGraph,
            bool includeMessages,
            bool includeProperties,
            bool deferImport = false)
        {
            if (settings == null)
                throw new ArgumentNullException(nameof(settings));

            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            if (!HasAnyUpdates(settings))
                throw new ArgumentException("At least one Shader Graph setting must be specified.", nameof(settings));

            var document = LoadMutableDocument(assetPath);
            var changedFields = new List<string>();

            if (settings.Graph != null)
                ApplyRootSettings(document.Root, settings.Graph, changedFields);

            if (settings.UniversalTarget != null)
            {
                var universalTarget = FindActiveTarget(document, UniversalTargetTypeName);
                if (universalTarget == null)
                {
                    throw new InvalidOperationException(
                        $"Shader Graph '{assetPath}' does not contain an active Universal target.");
                }

                ApplyUniversalTargetSettings(universalTarget, settings.UniversalTarget, changedFields);
            }

            if (changedFields.Count > 0)
            {
                WriteMutableDocument(document);
                if (!deferImport)
                {
                    AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                    EditorUtils.RepaintAllEditorWindows();
                }
            }

            if (deferImport)
            {
                return new ShaderGraphSettingsMutationResultData
                {
                    ChangedFields = changedFields
                };
            }

            var graphRef = new AssetObjectRef(assetPath);
            return new ShaderGraphSettingsMutationResultData
            {
                ChangedFields = changedFields,
                Settings = BuildShaderGraphSettingsData(graphRef),
                GraphSummary = BuildShaderGraphSummary(graphRef),
                Graph = includeGraph
                    ? BuildShaderGraphData(
                        graphRef,
                        includeMessages: includeMessages,
                        includeProperties: includeProperties,
                        includeDiagnostics: true)
                    : null
            };
        }

        static ShaderGraphSettingsData ReadSettingsData(string assetPath)
        {
            var data = new ShaderGraphSettingsData
            {
                AssetPath = assetPath
            };

            try
            {
                var document = LoadMutableDocument(assetPath);
                var root = document.Root;
                var warnings = new List<string>();
                var activeTargetTypes = new List<string>();

                data.SourceParsed = true;
                data.GraphType = GetString(root, "m_Type");
                data.GraphVersion = GetInt(root, "m_SGVersion");
                data.Graph = new ShaderGraphRootSettingsData
                {
                    ShaderMenuPath = GetString(root, "m_Path"),
                    GraphPrecisionValue = GetInt(root, "m_GraphPrecision"),
                    GraphPrecision = FormatGraphPrecision(GetInt(root, "m_GraphPrecision")),
                    PreviewModeValue = GetInt(root, "m_PreviewMode"),
                    PreviewMode = FormatPreviewMode(GetInt(root, "m_PreviewMode"))
                };

                foreach (var activeTargetId in GetIdArray(root, "m_ActiveTargets"))
                {
                    if (!document.ObjectsById.TryGetValue(activeTargetId, out var targetObject))
                        continue;

                    var targetType = GetString(targetObject, "m_Type");
                    if (!string.IsNullOrEmpty(targetType))
                        activeTargetTypes.Add(targetType);

                    if (string.Equals(targetType, UniversalTargetTypeName, StringComparison.Ordinal))
                    {
                        data.UniversalTarget = ParseUniversalTargetSettings(targetObject);
                        continue;
                    }

                    warnings.Add($"Active target '{targetType ?? activeTargetId}' is not yet exposed by the settings tool.");
                }

                if (activeTargetTypes.Count > 0)
                    data.ActiveTargetTypes = activeTargetTypes;

                if (warnings.Count > 0)
                    data.Warnings = warnings;
            }
            catch (Exception ex)
            {
                data.ParseError = ex.Message;
            }

            return data;
        }

        static ShaderGraphUniversalTargetSettingsData ParseUniversalTargetSettings(JsonObject targetObject)
        {
            var surfaceTypeValue = GetInt(targetObject, "m_SurfaceType");
            var alphaModeValue = GetInt(targetObject, "m_AlphaMode");
            var renderFaceValue = GetInt(targetObject, "m_RenderFace");
            var depthWriteValue = GetInt(targetObject, "m_ZWriteControl");
            var depthTestValue = GetInt(targetObject, "m_ZTestMode");
            var additionalMotionVectorsValue = GetInt(targetObject, "m_AdditionalMotionVectorMode");

            return new ShaderGraphUniversalTargetSettingsData
            {
                ObjectId = GetString(targetObject, "m_ObjectId"),
                Type = GetString(targetObject, "m_Type"),
                AllowMaterialOverride = GetBool(targetObject, "m_AllowMaterialOverride"),
                SurfaceTypeValue = surfaceTypeValue,
                SurfaceType = FormatSurfaceType(surfaceTypeValue),
                AlphaModeValue = alphaModeValue,
                AlphaMode = FormatAlphaMode(alphaModeValue),
                RenderFaceValue = renderFaceValue,
                RenderFace = FormatRenderFace(renderFaceValue),
                DepthWriteValue = depthWriteValue,
                DepthWrite = FormatDepthWrite(depthWriteValue),
                DepthTestValue = depthTestValue,
                DepthTest = FormatDepthTest(depthTestValue),
                AlphaClip = GetBool(targetObject, "m_AlphaClip"),
                CastShadows = GetBool(targetObject, "m_CastShadows"),
                ReceiveShadows = GetBool(targetObject, "m_ReceiveShadows"),
                DisableTint = GetBool(targetObject, "m_DisableTint"),
                AdditionalMotionVectorsValue = additionalMotionVectorsValue,
                AdditionalMotionVectors = FormatAdditionalMotionVectors(additionalMotionVectorsValue),
                AlembicMotionVectors = GetBool(targetObject, "m_AlembicMotionVectors"),
                SupportsLodCrossFade = GetBool(targetObject, "m_SupportsLODCrossFade"),
                CustomEditorGui = GetString(targetObject, "m_CustomEditorGUI"),
                SupportVfx = GetBool(targetObject, "m_SupportVFX")
            };
        }

        static bool HasAnyUpdates(ShaderGraphSettingsUpdateInput settings)
            => HasGraphUpdates(settings.Graph) || HasUniversalTargetUpdates(settings.UniversalTarget);

        static bool HasGraphUpdates(ShaderGraphRootSettingsUpdateInput? graph)
            => graph != null
               && (!string.IsNullOrWhiteSpace(graph.ShaderMenuPath)
                   || !string.IsNullOrWhiteSpace(graph.GraphPrecision)
                   || !string.IsNullOrWhiteSpace(graph.PreviewMode));

        static bool HasUniversalTargetUpdates(ShaderGraphUniversalTargetSettingsUpdateInput? universalTarget)
            => universalTarget != null
               && (universalTarget.AllowMaterialOverride.HasValue
                   || !string.IsNullOrWhiteSpace(universalTarget.SurfaceType)
                   || !string.IsNullOrWhiteSpace(universalTarget.AlphaMode)
                   || !string.IsNullOrWhiteSpace(universalTarget.RenderFace)
                   || !string.IsNullOrWhiteSpace(universalTarget.DepthWrite)
                   || !string.IsNullOrWhiteSpace(universalTarget.DepthTest)
                   || universalTarget.AlphaClip.HasValue
                   || universalTarget.CastShadows.HasValue
                   || universalTarget.ReceiveShadows.HasValue
                   || universalTarget.DisableTint.HasValue
                   || !string.IsNullOrWhiteSpace(universalTarget.AdditionalMotionVectors)
                   || universalTarget.AlembicMotionVectors.HasValue
                   || universalTarget.SupportsLodCrossFade.HasValue
                   || universalTarget.CustomEditorGui != null
                   || universalTarget.SupportVfx.HasValue);

        static void ApplyRootSettings(
            JsonObject root,
            ShaderGraphRootSettingsUpdateInput graph,
            List<string> changedFields)
        {
            if (!string.IsNullOrWhiteSpace(graph.ShaderMenuPath))
            {
                var shaderMenuPath = graph.ShaderMenuPath!.Trim();
                if (string.IsNullOrEmpty(shaderMenuPath))
                    throw new ArgumentException("graph.shaderMenuPath must not be empty.");

                SetString(root, "m_Path", shaderMenuPath, "graph.shaderMenuPath", changedFields);
            }

            if (!string.IsNullOrWhiteSpace(graph.GraphPrecision))
            {
                var graphPrecision = ParseGraphPrecision(graph.GraphPrecision!);
                SetInt(root, "m_GraphPrecision", graphPrecision, "graph.graphPrecision", changedFields);
            }

            if (!string.IsNullOrWhiteSpace(graph.PreviewMode))
            {
                var previewMode = ParsePreviewMode(graph.PreviewMode!);
                SetInt(root, "m_PreviewMode", previewMode, "graph.previewMode", changedFields);
            }
        }

        static void ApplyUniversalTargetSettings(
            JsonObject targetObject,
            ShaderGraphUniversalTargetSettingsUpdateInput universalTarget,
            List<string> changedFields)
        {
            if (universalTarget.AllowMaterialOverride.HasValue)
            {
                SetBool(
                    targetObject,
                    "m_AllowMaterialOverride",
                    universalTarget.AllowMaterialOverride.Value,
                    "universalTarget.allowMaterialOverride",
                    changedFields);
            }

            if (!string.IsNullOrWhiteSpace(universalTarget.SurfaceType))
            {
                var surfaceType = ParseSurfaceType(universalTarget.SurfaceType!);
                SetInt(targetObject, "m_SurfaceType", surfaceType, "universalTarget.surfaceType", changedFields);
            }

            if (!string.IsNullOrWhiteSpace(universalTarget.AlphaMode))
            {
                var alphaMode = ParseAlphaMode(universalTarget.AlphaMode!);
                SetInt(targetObject, "m_AlphaMode", alphaMode, "universalTarget.alphaMode", changedFields);
            }

            if (!string.IsNullOrWhiteSpace(universalTarget.RenderFace))
            {
                var renderFace = ParseRenderFace(universalTarget.RenderFace!);
                SetInt(targetObject, "m_RenderFace", renderFace, "universalTarget.renderFace", changedFields);
            }

            if (!string.IsNullOrWhiteSpace(universalTarget.DepthWrite))
            {
                var depthWrite = ParseDepthWrite(universalTarget.DepthWrite!);
                SetInt(targetObject, "m_ZWriteControl", depthWrite, "universalTarget.depthWrite", changedFields);
            }

            if (!string.IsNullOrWhiteSpace(universalTarget.DepthTest))
            {
                var depthTest = ParseDepthTest(universalTarget.DepthTest!);
                SetInt(targetObject, "m_ZTestMode", depthTest, "universalTarget.depthTest", changedFields);
            }

            if (universalTarget.AlphaClip.HasValue)
            {
                SetBool(
                    targetObject,
                    "m_AlphaClip",
                    universalTarget.AlphaClip.Value,
                    "universalTarget.alphaClip",
                    changedFields);
            }

            if (universalTarget.CastShadows.HasValue)
            {
                SetBool(
                    targetObject,
                    "m_CastShadows",
                    universalTarget.CastShadows.Value,
                    "universalTarget.castShadows",
                    changedFields);
            }

            if (universalTarget.ReceiveShadows.HasValue)
            {
                SetBool(
                    targetObject,
                    "m_ReceiveShadows",
                    universalTarget.ReceiveShadows.Value,
                    "universalTarget.receiveShadows",
                    changedFields);
            }

            if (universalTarget.DisableTint.HasValue)
            {
                SetBool(
                    targetObject,
                    "m_DisableTint",
                    universalTarget.DisableTint.Value,
                    "universalTarget.disableTint",
                    changedFields);
            }

            if (!string.IsNullOrWhiteSpace(universalTarget.AdditionalMotionVectors))
            {
                var additionalMotionVectors = ParseAdditionalMotionVectors(universalTarget.AdditionalMotionVectors!);
                SetInt(
                    targetObject,
                    "m_AdditionalMotionVectorMode",
                    additionalMotionVectors,
                    "universalTarget.additionalMotionVectors",
                    changedFields);
            }

            if (universalTarget.AlembicMotionVectors.HasValue)
            {
                SetBool(
                    targetObject,
                    "m_AlembicMotionVectors",
                    universalTarget.AlembicMotionVectors.Value,
                    "universalTarget.alembicMotionVectors",
                    changedFields);
            }

            if (universalTarget.SupportsLodCrossFade.HasValue)
            {
                SetBool(
                    targetObject,
                    "m_SupportsLODCrossFade",
                    universalTarget.SupportsLodCrossFade.Value,
                    "universalTarget.supportsLodCrossFade",
                    changedFields);
            }

            if (universalTarget.CustomEditorGui != null)
            {
                SetString(
                    targetObject,
                    "m_CustomEditorGUI",
                    universalTarget.CustomEditorGui.Trim(),
                    "universalTarget.customEditorGui",
                    changedFields);
            }

            if (universalTarget.SupportVfx.HasValue)
            {
                SetBool(
                    targetObject,
                    "m_SupportVFX",
                    universalTarget.SupportVfx.Value,
                    "universalTarget.supportVfx",
                    changedFields);
            }
        }

        static ShaderGraphMutableDocument LoadMutableDocument(string assetPath)
        {
            var fullPath = ResolvePhysicalAssetPath(assetPath);
            if (!File.Exists(fullPath))
                throw new FileNotFoundException($"Physical file does not exist at '{fullPath}'.", fullPath);

            var document = new ShaderGraphMutableDocument
            {
                AssetPath = assetPath,
                FullPath = fullPath
            };

            foreach (var jsonObject in EnumerateTopLevelJsonObjects(File.ReadAllText(fullPath)))
            {
                if (JsonNode.Parse(jsonObject)?.AsObject() is not JsonObject parsedObject)
                    throw new InvalidOperationException($"Failed to parse a top-level JSON object from '{assetPath}'.");

                document.Objects.Add(parsedObject);

                var objectId = GetString(parsedObject, "m_ObjectId");
                if (!string.IsNullOrEmpty(objectId))
                    document.ObjectsById[objectId] = parsedObject;
            }

            if (document.Objects.Count == 0)
                throw new InvalidOperationException($"No JSON objects were found in Shader Graph source '{assetPath}'.");

            return document;
        }

        static JsonObject? FindActiveTarget(ShaderGraphMutableDocument document, string targetType)
        {
            foreach (var activeTargetId in GetIdArray(document.Root, "m_ActiveTargets"))
            {
                if (!document.ObjectsById.TryGetValue(activeTargetId, out var targetObject))
                    continue;

                if (string.Equals(GetString(targetObject, "m_Type"), targetType, StringComparison.Ordinal))
                    return targetObject;
            }

            return null;
        }

        static void WriteMutableDocument(ShaderGraphMutableDocument document)
        {
            var serializedObjects = document.Objects
                .Select(obj => obj.ToJsonString(ShaderGraphJsonWriteOptions))
                .ToArray();

            var sourceText = string.Join(Environment.NewLine + Environment.NewLine, serializedObjects) + Environment.NewLine;
            File.WriteAllText(document.FullPath, sourceText);
        }

        static void SetString(
            JsonObject root,
            string propertyName,
            string value,
            string changedFieldName,
            List<string> changedFields)
        {
            var currentValue = GetString(root, propertyName);
            if (string.Equals(currentValue, value, StringComparison.Ordinal))
                return;

            root[propertyName] = value;
            changedFields.Add(changedFieldName);
        }

        static void SetInt(
            JsonObject root,
            string propertyName,
            int value,
            string changedFieldName,
            List<string> changedFields)
        {
            var currentValue = GetInt(root, propertyName);
            if (currentValue == value)
                return;

            root[propertyName] = value;
            changedFields.Add(changedFieldName);
        }

        static void SetBool(
            JsonObject root,
            string propertyName,
            bool value,
            string changedFieldName,
            List<string> changedFields)
        {
            var currentValue = GetBool(root, propertyName);
            if (currentValue == value)
                return;

            root[propertyName] = value;
            changedFields.Add(changedFieldName);
        }

        static string? GetString(JsonObject root, string propertyName)
        {
            if (root[propertyName] is not JsonValue propertyValue)
                return null;

            return propertyValue.TryGetValue<string>(out var value) ? value : null;
        }

        static int? GetInt(JsonObject root, string propertyName)
        {
            if (root[propertyName] is not JsonValue propertyValue)
                return null;

            if (propertyValue.TryGetValue<int>(out var intValue))
                return intValue;

            if (propertyValue.TryGetValue<double>(out var doubleValue))
                return (int)doubleValue;

            return null;
        }

        static bool? GetBool(JsonObject root, string propertyName)
        {
            if (root[propertyName] is not JsonValue propertyValue)
                return null;

            return propertyValue.TryGetValue<bool>(out var value) ? value : null;
        }

        static List<string> GetIdArray(JsonObject root, string propertyName)
        {
            if (root[propertyName] is not JsonArray array)
                return new List<string>();

            return array
                .Select(item => item?["m_Id"]?.GetValue<string>())
                .Where(id => !string.IsNullOrEmpty(id))
                .Cast<string>()
                .ToList();
        }

        static int ParseGraphPrecision(string value)
        {
            return NormalizeEnumValue(value) switch
            {
                "single" => 0,
                "graph" => 1,
                "half" => 2,
                _ => throw new ArgumentException(
                    $"Unsupported graph precision '{value}'. Supported values: single, graph, half.")
            };
        }

        static int ParsePreviewMode(string value)
        {
            return NormalizeEnumValue(value) switch
            {
                "inherit" => 0,
                "preview2d" or "2d" => 1,
                "preview3d" or "3d" => 2,
                _ => throw new ArgumentException(
                    $"Unsupported preview mode '{value}'. Supported values: inherit, preview2d, preview3d.")
            };
        }

        static int ParseSurfaceType(string value)
        {
            return NormalizeEnumValue(value) switch
            {
                "opaque" => 0,
                "transparent" => 1,
                _ => throw new ArgumentException(
                    $"Unsupported surface type '{value}'. Supported values: opaque, transparent.")
            };
        }

        static int ParseAlphaMode(string value)
        {
            return NormalizeEnumValue(value) switch
            {
                "alpha" => 0,
                "premultiply" or "premultiplied" => 1,
                "additive" => 2,
                "multiply" => 3,
                _ => throw new ArgumentException(
                    $"Unsupported alpha mode '{value}'. Supported values: alpha, premultiply, additive, multiply.")
            };
        }

        static int ParseRenderFace(string value)
        {
            return NormalizeEnumValue(value) switch
            {
                "both" => 0,
                "back" => 1,
                "front" => 2,
                _ => throw new ArgumentException(
                    $"Unsupported render face '{value}'. Supported values: front, back, both.")
            };
        }

        static int ParseDepthWrite(string value)
        {
            return NormalizeEnumValue(value) switch
            {
                "auto" => 0,
                "forceenabled" or "enabled" or "enable" or "on" => 1,
                "forcedisabled" or "disabled" or "disable" or "off" => 2,
                _ => throw new ArgumentException(
                    $"Unsupported depth write '{value}'. Supported values: auto, forceEnabled, forceDisabled.")
            };
        }

        static int ParseDepthTest(string value)
        {
            return NormalizeEnumValue(value) switch
            {
                "never" => 1,
                "less" => 2,
                "equal" => 3,
                "lessequal" or "lequal" => 4,
                "greater" => 5,
                "notequal" => 6,
                "greaterequal" or "gequal" => 7,
                "always" => 8,
                _ => throw new ArgumentException(
                    $"Unsupported depth test '{value}'. Supported values: never, less, equal, lessEqual, greater, notEqual, greaterEqual, always.")
            };
        }

        static int ParseAdditionalMotionVectors(string value)
        {
            return NormalizeEnumValue(value) switch
            {
                "none" => 0,
                "timebased" or "time" => 1,
                "custom" => 2,
                _ => throw new ArgumentException(
                    $"Unsupported additional motion vectors '{value}'. Supported values: none, timeBased, custom.")
            };
        }

        static string? FormatGraphPrecision(int? value)
        {
            return value switch
            {
                0 => "single",
                1 => "graph",
                2 => "half",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatPreviewMode(int? value)
        {
            return value switch
            {
                0 => "inherit",
                1 => "preview2d",
                2 => "preview3d",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatSurfaceType(int? value)
        {
            return value switch
            {
                0 => "opaque",
                1 => "transparent",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatAlphaMode(int? value)
        {
            return value switch
            {
                0 => "alpha",
                1 => "premultiply",
                2 => "additive",
                3 => "multiply",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatRenderFace(int? value)
        {
            return value switch
            {
                0 => "both",
                1 => "back",
                2 => "front",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatDepthWrite(int? value)
        {
            return value switch
            {
                0 => "auto",
                1 => "forceEnabled",
                2 => "forceDisabled",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatDepthTest(int? value)
        {
            return value switch
            {
                1 => "never",
                2 => "less",
                3 => "equal",
                4 => "lessEqual",
                5 => "greater",
                6 => "notEqual",
                7 => "greaterEqual",
                8 => "always",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatAdditionalMotionVectors(int? value)
        {
            return value switch
            {
                0 => "none",
                1 => "timeBased",
                2 => "custom",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string NormalizeEnumValue(string value)
            => new string(value
                .Trim()
                .Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant)
                .ToArray());
    }
}
