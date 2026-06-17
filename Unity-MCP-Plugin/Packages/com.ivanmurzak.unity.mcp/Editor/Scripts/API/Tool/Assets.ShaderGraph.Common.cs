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
using AIGD;
using com.IvanMurzak.Unity.MCP.Runtime.Extensions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        sealed class ShaderGraphSourceInfo
        {
            public string AssetPath { get; set; } = string.Empty;
            public bool FileExists { get; set; }
            public bool SourceParsed { get; set; }
            public string? ParseError { get; set; }
            public int? GraphVersion { get; set; }
            public string? GraphType { get; set; }
            public string? ShaderMenuPath { get; set; }
            public int GraphPropertyCount { get; set; }
            public int KeywordCount { get; set; }
            public int DropdownCount { get; set; }
            public int NodeCount { get; set; }
            public int EdgeCount { get; set; }
            public int GroupCount { get; set; }
            public int StickyNoteCount { get; set; }
            public int SubDataCount { get; set; }
            public int ActiveTargetCount { get; set; }
            public List<string> ActiveTargetTypes { get; } = new();
        }

        internal static bool IsShaderGraphAssetPath(string assetPath)
            => assetPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase);

        internal static string ResolveAssetPath(AssetObjectRef assetRef)
        {
            var asset = assetRef.FindAssetObject();
            if (asset == null)
                throw new Exception(Tool_Assets.Error.NotFoundAsset(assetRef.AssetPath ?? "N/A", assetRef.AssetGuid ?? "N/A"));

            var assetPath = string.IsNullOrEmpty(assetRef.AssetPath)
                ? AssetDatabase.GetAssetPath(asset)
                : assetRef.AssetPath!;

            if (string.IsNullOrEmpty(assetPath))
                throw new Exception(Tool_Assets.Error.NotFoundAsset(assetRef.AssetPath ?? "N/A", assetRef.AssetGuid ?? "N/A"));

            return assetPath;
        }

        internal static ShaderGraphData BuildShaderGraphData(
            AssetObjectRef assetRef,
            bool includeMessages,
            bool includeProperties,
            bool includeDiagnostics)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var asset = assetRef.FindAssetObject();
            var shader = asset as Shader ?? AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath);
            var sourceInfo = ReadSourceInfo(assetPath);
            var resolvedAsset = asset ?? (UnityEngine.Object?)shader ?? AssetDatabase.LoadMainAssetAtPath(assetPath);

            var data = new ShaderGraphData
            {
                Reference = resolvedAsset == null ? null : new AssetObjectRef(resolvedAsset),
                AssetPath = assetPath,
                SourceFileExtension = Path.GetExtension(assetPath),
                SourceParsed = sourceInfo.SourceParsed,
                ImporterType = importer?.GetType().FullName,
                ShaderName = shader?.name,
                ShaderResolved = shader != null,
                IsSupported = shader != null && shader.isSupported,
                HasErrors = shader != null && ShaderUtil.ShaderHasError(shader),
                GraphVersion = sourceInfo.GraphVersion,
                GraphType = sourceInfo.GraphType,
                ShaderMenuPath = sourceInfo.ShaderMenuPath,
                GraphPropertyCount = sourceInfo.GraphPropertyCount,
                KeywordCount = sourceInfo.KeywordCount,
                DropdownCount = sourceInfo.DropdownCount,
                NodeCount = sourceInfo.NodeCount,
                EdgeCount = sourceInfo.EdgeCount,
                GroupCount = sourceInfo.GroupCount,
                StickyNoteCount = sourceInfo.StickyNoteCount,
                SubDataCount = sourceInfo.SubDataCount,
                ActiveTargetCount = sourceInfo.ActiveTargetCount,
                ActiveTargetTypes = sourceInfo.ActiveTargetTypes.Count == 0
                    ? null
                    : sourceInfo.ActiveTargetTypes.OrderBy(typeName => typeName).ToList()
            };

            if (shader != null)
                FillCompiledShaderData(data, shader, includeMessages, includeProperties);

            if (includeDiagnostics)
                data.Diagnostics = BuildDiagnostics(assetPath, sourceInfo, importer, shader);

            return data;
        }

        internal static ShaderGraphStructureData BuildShaderGraphStructureData(AssetObjectRef assetRef)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var asset = assetRef.FindAssetObject();
            var resolvedAsset = asset ?? AssetDatabase.LoadMainAssetAtPath(assetPath);
            var data = ReadStructureData(assetPath);
            data.Reference = resolvedAsset == null ? null : new AssetObjectRef(resolvedAsset);
            return data;
        }

        internal static ShaderGraphSummaryData BuildShaderGraphSummary(AssetObjectRef assetRef)
        {
            var assetPath = ResolveAssetPath(assetRef);
            if (!IsShaderGraphAssetPath(assetPath))
                throw new ArgumentException(Error.AssetIsNotShaderGraph(assetPath), nameof(assetRef));

            var asset = assetRef.FindAssetObject();
            var shader = asset as Shader ?? AssetDatabase.LoadAssetAtPath<Shader>(assetPath);
            var importer = AssetImporter.GetAtPath(assetPath);
            var sourceInfo = ReadSourceInfo(assetPath);

            var fullDiagnostics = BuildDiagnostics(assetPath, sourceInfo, importer, shader);
            var filtered = fullDiagnostics
                .Where(d => !string.Equals(d.Severity, "Info", StringComparison.Ordinal))
                .ToList();

            return new ShaderGraphSummaryData
            {
                ShaderResolved = shader != null,
                HasErrors = shader != null && ShaderUtil.ShaderHasError(shader),
                NodeCount = sourceInfo.NodeCount,
                EdgeCount = sourceInfo.EdgeCount,
                Diagnostics = filtered.Count == 0 ? null : filtered
            };
        }

        static void FillCompiledShaderData(
            ShaderGraphData data,
            Shader shader,
            bool includeMessages,
            bool includeProperties)
        {
            data.RenderQueue = shader.renderQueue;
            data.PassCount = shader.passCount;
            data.ShaderPropertyCount = shader.GetPropertyCount();

            if (shader.passCount > 0)
            {
                var renderType = shader.FindPassTagValue(0, new ShaderTagId("RenderType")).name;
                data.RenderType = string.IsNullOrEmpty(renderType) ? null : renderType;
            }

            if (includeMessages)
            {
                var messages = ShaderUtil.GetShaderMessages(shader);
                if (messages != null && messages.Length > 0)
                {
                    data.Messages = messages.Select(msg => new ShaderMessageData
                    {
                        Message = msg.message,
                        Line = msg.line,
                        Severity = msg.severity.ToString(),
                        Platform = msg.platform.ToString()
                    }).ToList();
                }
            }

            if (includeProperties)
            {
                var propertyCount = shader.GetPropertyCount();
                if (propertyCount > 0)
                {
                    data.Properties = new List<ShaderPropertyData>(propertyCount);
                    for (var i = 0; i < propertyCount; i++)
                    {
                        var propType = shader.GetPropertyType(i);
                        var prop = new ShaderPropertyData
                        {
                            Name = shader.GetPropertyName(i),
                            Description = shader.GetPropertyDescription(i),
                            Type = propType.ToString(),
                            Flags = shader.GetPropertyFlags(i).ToString(),
                            NameId = shader.GetPropertyNameId(i)
                        };

                        if (propType == ShaderPropertyType.Range)
                        {
                            var rangeLimits = shader.GetPropertyRangeLimits(i);
                            prop.RangeMin = rangeLimits.x;
                            prop.RangeMax = rangeLimits.y;
                        }

                        if (propType == ShaderPropertyType.Texture)
                        {
                            var defaultTextureName = shader.GetPropertyTextureDefaultName(i);
                            if (!string.IsNullOrEmpty(defaultTextureName))
                                prop.DefaultTextureName = defaultTextureName;
                        }

                        var attributes = shader.GetPropertyAttributes(i);
                        if (attributes != null && attributes.Length > 0)
                            prop.Attributes = attributes.ToList();

                        data.Properties.Add(prop);
                    }
                }
            }
        }

        static List<ShaderGraphDiagnosticData> BuildDiagnostics(
            string assetPath,
            ShaderGraphSourceInfo sourceInfo,
            AssetImporter? importer,
            Shader? shader)
        {
            var diagnostics = new List<ShaderGraphDiagnosticData>();

            if (importer == null)
            {
                diagnostics.Add(new ShaderGraphDiagnosticData
                {
                    Code = "IMPORTER_MISSING",
                    Severity = "Error",
                    Message = $"No AssetImporter was found for '{assetPath}'.",
                    Hint = "Refresh the AssetDatabase or re-import the asset."
                });
            }

            if (!sourceInfo.FileExists)
            {
                diagnostics.Add(new ShaderGraphDiagnosticData
                {
                    Code = "SOURCE_FILE_MISSING",
                    Severity = "Error",
                    Message = $"Shader Graph source file is missing at '{assetPath}'.",
                    Hint = "Restore the file or recreate the graph asset."
                });
                return diagnostics;
            }

            if (!sourceInfo.SourceParsed)
            {
                diagnostics.Add(new ShaderGraphDiagnosticData
                {
                    Code = "SOURCE_PARSE_FAILED",
                    Severity = "Error",
                    Message = $"Shader Graph source parsing failed for '{assetPath}': {sourceInfo.ParseError}",
                    Hint = "Open the graph in the Shader Graph editor and re-save it, or recreate it from a known-good template."
                });
            }
            else
            {
                if (!string.Equals(sourceInfo.GraphType, "UnityEditor.ShaderGraph.GraphData", StringComparison.Ordinal))
                {
                    diagnostics.Add(new ShaderGraphDiagnosticData
                    {
                        Code = "UNEXPECTED_ROOT_TYPE",
                        Severity = "Warning",
                        Message = $"Unexpected root graph type '{sourceInfo.GraphType ?? "null"}' in '{assetPath}'.",
                        Hint = "Verify the asset is a Shader Graph and not another serialized importer asset."
                    });
                }

                if (sourceInfo.NodeCount == 0)
                {
                    diagnostics.Add(new ShaderGraphDiagnosticData
                    {
                        Code = "EMPTY_GRAPH",
                        Severity = "Warning",
                        Message = $"Shader Graph '{assetPath}' does not declare any nodes.",
                        Hint = "Open the graph and ensure it contains a valid node network."
                    });
                }

                if (sourceInfo.ActiveTargetCount == 0)
                {
                    diagnostics.Add(new ShaderGraphDiagnosticData
                    {
                        Code = "NO_ACTIVE_TARGETS",
                        Severity = "Warning",
                        Message = $"Shader Graph '{assetPath}' does not declare any active targets.",
                        Hint = "Assign at least one target in Shader Graph Graph Settings."
                    });
                }
            }

            if (shader == null)
            {
                diagnostics.Add(new ShaderGraphDiagnosticData
                {
                    Code = "SHADER_UNRESOLVED",
                    Severity = "Error",
                    Message = $"Unity did not resolve a compiled Shader from '{assetPath}'.",
                    Hint = "Check the importer status, refresh the asset, and inspect the Console for import errors."
                });
                return diagnostics;
            }

            if (ShaderUtil.ShaderHasError(shader))
            {
                diagnostics.Add(new ShaderGraphDiagnosticData
                {
                    Code = "SHADER_COMPILE_ERRORS",
                    Severity = "Error",
                    Message = $"Compiled shader '{shader.name}' reports errors.",
                    Hint = "Inspect 'Messages' for compiler output and fix the graph before material creation."
                });
            }

            if (diagnostics.Count == 0)
            {
                diagnostics.Add(new ShaderGraphDiagnosticData
                {
                    Code = "OK",
                    Severity = "Info",
                    Message = $"Shader Graph '{assetPath}' imported successfully.",
                    Hint = "The graph is ready for validation or material creation."
                });
            }

            return diagnostics;
        }

        static ShaderGraphSourceInfo ReadSourceInfo(string assetPath)
        {
            var info = new ShaderGraphSourceInfo
            {
                AssetPath = assetPath
            };

            var fullPath = ResolvePhysicalAssetPath(assetPath);
            info.FileExists = File.Exists(fullPath);
            if (!info.FileExists)
            {
                info.ParseError = $"Physical file does not exist at '{fullPath}'.";
                return info;
            }

            var activeTargetIds = new List<string>();
            var typesByObjectId = new Dictionary<string, string>(StringComparer.Ordinal);

            try
            {
                var objectIndex = 0;
                foreach (var jsonObject in EnumerateTopLevelJsonObjects(File.ReadAllText(fullPath)))
                {
                    using var document = JsonDocument.Parse(jsonObject);
                    var root = document.RootElement;
                    objectIndex++;

                    if (objectIndex == 1)
                        PopulateSourceSummary(info, root, activeTargetIds);

                    var objectId = GetString(root, "m_ObjectId");
                    var objectType = GetString(root, "m_Type");
                    if (!string.IsNullOrEmpty(objectId) && !string.IsNullOrEmpty(objectType))
                        typesByObjectId[objectId!] = objectType!;
                }

                if (objectIndex == 0)
                {
                    info.ParseError = "No JSON objects were found in the Shader Graph source file.";
                    return info;
                }

                foreach (var activeTargetId in activeTargetIds)
                {
                    if (typesByObjectId.TryGetValue(activeTargetId, out var typeName))
                        info.ActiveTargetTypes.Add(typeName);
                }

                info.SourceParsed = true;
            }
            catch (Exception ex)
            {
                info.ParseError = ex.Message;
            }

            return info;
        }

        static void PopulateSourceSummary(
            ShaderGraphSourceInfo info,
            JsonElement root,
            List<string> activeTargetIds)
        {
            info.GraphVersion = GetInt(root, "m_SGVersion");
            info.GraphType = GetString(root, "m_Type");
            info.ShaderMenuPath = GetString(root, "m_Path");
            info.GraphPropertyCount = CountArray(root, "m_Properties");
            info.KeywordCount = CountArray(root, "m_Keywords");
            info.DropdownCount = CountArray(root, "m_Dropdowns");
            info.NodeCount = CountArray(root, "m_Nodes");
            info.EdgeCount = CountArray(root, "m_Edges");
            info.GroupCount = CountArray(root, "m_GroupDatas");
            info.StickyNoteCount = CountArray(root, "m_StickyNoteDatas");
            info.SubDataCount = CountArray(root, "m_SubDatas");
            info.ActiveTargetCount = CountArray(root, "m_ActiveTargets");

            if (root.TryGetProperty("m_ActiveTargets", out var activeTargets) &&
                activeTargets.ValueKind == JsonValueKind.Array)
            {
                foreach (var target in activeTargets.EnumerateArray())
                {
                    var targetId = GetString(target, "m_Id");
                    if (!string.IsNullOrEmpty(targetId))
                        activeTargetIds.Add(targetId!);
                }
            }
        }

        static ShaderGraphStructureData ReadStructureData(string assetPath)
        {
            var data = new ShaderGraphStructureData
            {
                AssetPath = assetPath
            };

            var fullPath = ResolvePhysicalAssetPath(assetPath);
            if (!File.Exists(fullPath))
            {
                data.ParseError = $"Physical file does not exist at '{fullPath}'.";
                return data;
            }

            try
            {
                var jsonObjects = EnumerateTopLevelJsonObjects(File.ReadAllText(fullPath)).ToList();
                if (jsonObjects.Count == 0)
                {
                    data.ParseError = "No JSON objects were found in the Shader Graph source file.";
                    return data;
                }

                var objectTypesById = new Dictionary<string, string>(StringComparer.Ordinal);
                var propertyIds = new List<string>();
                var categoryIds = new List<string>();
                var nodeIds = new List<string>();
                var activeTargetIds = new List<string>();
                var propertyIdSet = new HashSet<string>(StringComparer.Ordinal);
                var categoryIdSet = new HashSet<string>(StringComparer.Ordinal);
                var nodeIdSet = new HashSet<string>(StringComparer.Ordinal);
                var activeTargetIdSet = new HashSet<string>(StringComparer.Ordinal);
                var propertiesById = new Dictionary<string, ShaderGraphPropertyDefinitionData>(StringComparer.Ordinal);
                var categoriesById = new Dictionary<string, ShaderGraphCategoryDefinitionData>(StringComparer.Ordinal);
                var nodesById = new Dictionary<string, ShaderGraphNodeDefinitionData>(StringComparer.Ordinal);
                var slotsByObjectId = new Dictionary<string, ShaderGraphSlotDefinitionData>(StringComparer.Ordinal);
                var targetsById = new Dictionary<string, ShaderGraphTargetDefinitionData>(StringComparer.Ordinal);

                for (var i = 0; i < jsonObjects.Count; i++)
                {
                    using var document = JsonDocument.Parse(jsonObjects[i]);
                    var root = document.RootElement;
                    var objectId = GetString(root, "m_ObjectId");
                    var objectType = GetString(root, "m_Type");

                    if (!string.IsNullOrEmpty(objectId) && !string.IsNullOrEmpty(objectType))
                        objectTypesById[objectId!] = objectType!;

                    if (i == 0)
                    {
                        PopulateStructureRoot(
                            data,
                            root,
                            propertyIds,
                            categoryIds,
                            nodeIds,
                            activeTargetIds,
                            propertyIdSet,
                            categoryIdSet,
                            nodeIdSet,
                            activeTargetIdSet);
                        continue;
                    }

                    if (string.IsNullOrEmpty(objectId))
                        continue;

                    var resolvedObjectId = objectId!;
                    if (propertyIdSet.Contains(resolvedObjectId))
                    {
                        propertiesById[resolvedObjectId] = ParsePropertyDefinition(root, resolvedObjectId);
                        continue;
                    }

                    if (categoryIdSet.Contains(resolvedObjectId))
                    {
                        categoriesById[resolvedObjectId] = ParseCategoryDefinition(root, resolvedObjectId);
                        continue;
                    }

                    if (nodeIdSet.Contains(resolvedObjectId))
                    {
                        nodesById[resolvedObjectId] = ParseNodeDefinition(root, resolvedObjectId);
                        continue;
                    }

                    if (activeTargetIdSet.Contains(resolvedObjectId))
                    {
                        targetsById[resolvedObjectId] = ParseTargetDefinition(root, resolvedObjectId);
                        continue;
                    }

                    if (IsSlotDefinition(root))
                        slotsByObjectId[resolvedObjectId] = ParseSlotDefinition(root, resolvedObjectId);
                }

                data.Properties = propertyIds
                    .Where(propertiesById.ContainsKey)
                    .Select(propertyId => propertiesById[propertyId])
                    .ToList();

                data.Categories = categoryIds
                    .Where(categoriesById.ContainsKey)
                    .Select(categoryId => categoriesById[categoryId])
                    .ToList();

                if (data.Categories != null && data.Properties != null)
                {
                    foreach (var category in data.Categories)
                    {
                        if (category.PropertyObjectIds == null)
                            continue;

                        for (var propertyIndex = 0; propertyIndex < category.PropertyObjectIds.Count; propertyIndex++)
                        {
                            var propertyId = category.PropertyObjectIds[propertyIndex];
                            if (!propertiesById.TryGetValue(propertyId, out var property))
                                continue;

                            property.CategoryObjectId = category.ObjectId;
                            property.CategoryName = category.Name;
                            property.CategoryIndex = propertyIndex;
                        }
                    }
                }

                data.Nodes = nodeIds
                    .Where(nodesById.ContainsKey)
                    .Select(nodeId =>
                    {
                        var node = nodesById[nodeId];
                        var nodePropertyObjectId = node.PropertyObjectId;
                        if (!string.IsNullOrEmpty(nodePropertyObjectId)
                            && propertiesById.TryGetValue(nodePropertyObjectId!, out var property))
                        {
                            node.PropertyReferenceName = !string.IsNullOrEmpty(property.OverrideReferenceName)
                                ? property.OverrideReferenceName
                                : property.DefaultReferenceName;
                        }

                        if (node.SlotObjectIds != null && node.SlotObjectIds.Count > 0)
                        {
                            node.Slots = node.SlotObjectIds
                                .Where(slotsByObjectId.ContainsKey)
                                .Select(slotObjectId => slotsByObjectId[slotObjectId])
                                .ToList();
                            PopulateSlotDerivedNodeSettings(node);
                        }

                        return node;
                    })
                    .ToList();

                data.Targets = activeTargetIds
                    .Where(targetsById.ContainsKey)
                    .Select(targetId =>
                    {
                        var target = targetsById[targetId];
                        if (target.DataObjectIds != null && target.DataObjectIds.Count > 0)
                        {
                            target.DataObjectTypes = target.DataObjectIds
                                .Where(objectTypesById.ContainsKey)
                                .Select(dataObjectId => objectTypesById[dataObjectId])
                                .ToList();
                        }

                        return target;
                    })
                    .ToList();

                data.SourceParsed = true;
            }
            catch (Exception ex)
            {
                data.ParseError = ex.Message;
            }

            return data;
        }

        static void PopulateStructureRoot(
            ShaderGraphStructureData data,
            JsonElement root,
            List<string> propertyIds,
            List<string> categoryIds,
            List<string> nodeIds,
            List<string> activeTargetIds,
            HashSet<string> propertyIdSet,
            HashSet<string> categoryIdSet,
            HashSet<string> nodeIdSet,
            HashSet<string> activeTargetIdSet)
        {
            data.GraphVersion = GetInt(root, "m_SGVersion");
            data.GraphType = GetString(root, "m_Type");
            data.ShaderMenuPath = GetString(root, "m_Path");
            data.GraphPrecision = GetInt(root, "m_GraphPrecision");
            data.PreviewMode = GetInt(root, "m_PreviewMode");
            data.OutputNodeId = GetStringAt(root, "m_OutputNode", "m_Id");
            data.Edges = ParseEdgeDefinitions(root);
            data.VertexContext = ParseContextDefinition(root, "m_VertexContext");
            data.FragmentContext = ParseContextDefinition(root, "m_FragmentContext");

            foreach (var propertyId in GetIdArray(root, "m_Properties"))
            {
                propertyIds.Add(propertyId);
                propertyIdSet.Add(propertyId);
            }

            foreach (var categoryId in GetIdArray(root, "m_CategoryData"))
            {
                categoryIds.Add(categoryId);
                categoryIdSet.Add(categoryId);
            }

            foreach (var nodeId in GetIdArray(root, "m_Nodes"))
            {
                nodeIds.Add(nodeId);
                nodeIdSet.Add(nodeId);
            }

            foreach (var activeTargetId in GetIdArray(root, "m_ActiveTargets"))
            {
                activeTargetIds.Add(activeTargetId);
                activeTargetIdSet.Add(activeTargetId);
            }
        }

        static ShaderGraphPropertyDefinitionData ParsePropertyDefinition(JsonElement root, string objectId)
        {
            var propertyType = GetString(root, "m_Type");
            var defaultReferenceName = GetString(root, "m_DefaultReferenceName");
            var overrideReferenceName = GetString(root, "m_OverrideReferenceName");
            var textureDefaultTypeValue = GetInt(root, "m_DefaultType");
            var textureAssetGuid = ParseTexture2DAssetGuid(root, propertyType);

            return new ShaderGraphPropertyDefinitionData
            {
                ObjectId = objectId,
                Type = propertyType,
                Name = GetString(root, "m_Name"),
                DefaultReferenceName = defaultReferenceName,
                OverrideReferenceName = overrideReferenceName,
                EffectiveReferenceName = string.IsNullOrEmpty(overrideReferenceName)
                    ? defaultReferenceName
                    : overrideReferenceName,
                Guid = GetStringAt(root, "m_Guid", "m_GuidSerialized"),
                Hidden = GetBool(root, "m_Hidden") ?? false,
                GeneratePropertyBlock = GetBool(root, "m_GeneratePropertyBlock") ?? false,
                ValueJson = GetRawText(root, "m_Value"),
                PropertyKind = FormatShaderGraphPropertyKind(propertyType),
                ColorHex = ParsePropertyColorHex(root, propertyType),
                FloatValue = string.Equals(propertyType, "UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty", StringComparison.Ordinal)
                    ? GetFloat(root, "m_Value")
                    : null,
                VectorX = ParseVectorPropertyComponent(root, propertyType, "x"),
                VectorY = ParseVectorPropertyComponent(root, propertyType, "y"),
                VectorZ = ParseVectorPropertyComponent(root, propertyType, "z"),
                VectorW = ParseVectorPropertyComponent(root, propertyType, "w"),
                BooleanValue = string.Equals(propertyType, "UnityEditor.ShaderGraph.Internal.BooleanShaderProperty", StringComparison.Ordinal)
                    ? GetBool(root, "m_Value")
                    : null,
                TextureDefaultTypeValue = string.Equals(propertyType, "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty", StringComparison.Ordinal)
                    ? textureDefaultTypeValue
                    : null,
                TextureDefaultType = string.Equals(propertyType, "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty", StringComparison.Ordinal)
                    ? FormatTexture2DDefaultType(textureDefaultTypeValue)
                    : null,
                TextureAssetGuid = textureAssetGuid,
                TextureAssetPath = string.IsNullOrWhiteSpace(textureAssetGuid)
                    ? null
                    : AssetDatabase.GUIDToAssetPath(textureAssetGuid),
                TextureUseTilingAndOffset = string.Equals(propertyType, "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty", StringComparison.Ordinal)
                    ? GetBool(root, "useTilingAndOffset")
                    : null,
                TextureUseTexelSize = string.Equals(propertyType, "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty", StringComparison.Ordinal)
                    ? GetBool(root, "useTexelSize")
                    : null,
                TextureIsMainTexture = string.Equals(propertyType, "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty", StringComparison.Ordinal)
                    ? GetBool(root, "isMainTexture")
                    : null,
                TextureIsHdr = string.Equals(propertyType, "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty", StringComparison.Ordinal)
                    ? GetBool(root, "isHDR")
                    : null,
                TextureModifiable = string.Equals(propertyType, "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty", StringComparison.Ordinal)
                    ? GetBool(root, "m_Modifiable")
                    : null
            };
        }

        static string? ParseTexture2DAssetGuid(JsonElement root, string? propertyType)
        {
            if (!string.Equals(propertyType, "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty", StringComparison.Ordinal))
                return null;

            return ParseSerializableTextureAssetGuid(root, "m_Value");
        }

        static string? ParseTexture2DInputSlotAssetGuid(JsonElement root, string? slotType)
        {
            if (!string.Equals(slotType, "UnityEditor.ShaderGraph.Texture2DInputMaterialSlot", StringComparison.Ordinal))
                return null;

            return ParseSerializableTextureAssetGuid(root, "m_Texture");
        }

        static string? ParseSerializableTextureAssetGuid(JsonElement root, params string[] texturePropertyPath)
        {
            if (!TryGetPropertyByPath(root, out var textureValue, texturePropertyPath) || textureValue.ValueKind != JsonValueKind.Object)
                return null;

            var directGuid = GetStringAt(textureValue, "m_Guid");
            if (!string.IsNullOrWhiteSpace(directGuid))
                return directGuid;

            var serializedTexture = GetStringAt(textureValue, "m_SerializedTexture");
            if (string.IsNullOrWhiteSpace(serializedTexture))
                return null;

            try
            {
                using var textureDocument = JsonDocument.Parse(serializedTexture!);
                var serializedGuid = GetStringAt(textureDocument.RootElement, "texture", "guid");
                return string.IsNullOrWhiteSpace(serializedGuid) ? null : serializedGuid;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        static ShaderGraphCategoryDefinitionData ParseCategoryDefinition(JsonElement root, string objectId)
        {
            return new ShaderGraphCategoryDefinitionData
            {
                ObjectId = objectId,
                Type = GetString(root, "m_Type"),
                Name = GetString(root, "m_Name"),
                PropertyObjectIds = GetIdArray(root, "m_ChildObjectList")
            };
        }

        static ShaderGraphNodeDefinitionData ParseNodeDefinition(JsonElement root, string objectId)
        {
            var node = new ShaderGraphNodeDefinitionData
            {
                ObjectId = objectId,
                Type = GetString(root, "m_Type"),
                Name = GetString(root, "m_Name"),
                GroupId = GetStringAt(root, "m_Group", "m_Id"),
                PositionX = GetFloatAt(root, "m_DrawState", "m_Position", "x") ?? 0f,
                PositionY = GetFloatAt(root, "m_DrawState", "m_Position", "y") ?? 0f,
                Width = GetFloatAt(root, "m_DrawState", "m_Position", "width") ?? 0f,
                Height = GetFloatAt(root, "m_DrawState", "m_Position", "height") ?? 0f,
                Precision = GetInt(root, "m_Precision"),
                SerializedDescriptor = GetString(root, "m_SerializedDescriptor"),
                PropertyObjectId = GetStringAt(root, "m_Property", "m_Id"),
                SlotObjectIds = GetIdArray(root, "m_Slots")
            };

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.SampleTexture2DNode", StringComparison.Ordinal))
                node.SampleTexture2D = ParseSampleTexture2DNodeSettings(root);

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.MultiplyNode", StringComparison.Ordinal))
                node.Multiply = ParseMultiplyNodeSettings(root);

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.RemapNode", StringComparison.Ordinal))
                node.Remap = new ShaderGraphRemapNodeSettingsData();

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.ViewDirectionNode", StringComparison.Ordinal)
                || string.Equals(node.Type, "UnityEditor.ShaderGraph.ViewVectorNode", StringComparison.Ordinal)
                || string.Equals(node.Type, "UnityEditor.ShaderGraph.NormalVectorNode", StringComparison.Ordinal))
            {
                node.SourceVector = ParseSpaceNodeSettings(root);
            }

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.PositionNode", StringComparison.Ordinal))
                node.Position = ParsePositionNodeSettings(root);

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.TransformNode", StringComparison.Ordinal))
                node.Transform = ParseTransformNodeSettings(root);

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.GradientNoiseNode", StringComparison.Ordinal))
                node.GradientNoise = ParseGradientNoiseNodeSettings(root);

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.NoiseNode", StringComparison.Ordinal))
                node.SimpleNoise = new ShaderGraphSimpleNoiseNodeSettingsData();

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.UVNode", StringComparison.Ordinal))
                node.Uv = ParseUvNodeSettings(root);

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.ScreenPositionNode", StringComparison.Ordinal))
                node.ScreenPosition = ParseScreenPositionNodeSettings(root);

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.SceneDepthNode", StringComparison.Ordinal))
                node.SceneDepth = ParseSceneDepthNodeSettings(root);

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.ComparisonNode", StringComparison.Ordinal))
                node.Comparison = ParseComparisonNodeSettings(root);

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.NormalFromHeightNode", StringComparison.Ordinal))
                node.NormalFromHeight = ParseNormalFromHeightNodeSettings(root);

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.BlendNode", StringComparison.Ordinal))
                node.Blend = ParseBlendNodeSettings(root);

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.SwizzleNode", StringComparison.Ordinal))
                node.Swizzle = ParseSwizzleNodeSettings(root);

            if (string.Equals(node.Type, "UnityEditor.ShaderGraph.Vector2Node", StringComparison.Ordinal))
                node.Vector2 = new ShaderGraphVector2NodeSettingsData();

            return node;
        }

        static ShaderGraphSampleTexture2DNodeSettingsData ParseSampleTexture2DNodeSettings(JsonElement root)
        {
            var textureTypeValue = GetInt(root, "m_TextureType");
            var normalMapSpaceValue = GetInt(root, "m_NormalMapSpace");
            var mipSamplingModeValue = GetInt(root, "m_MipSamplingMode");

            return new ShaderGraphSampleTexture2DNodeSettingsData
            {
                TextureTypeValue = textureTypeValue,
                TextureType = FormatSampleTexture2DTextureType(textureTypeValue),
                NormalMapSpaceValue = normalMapSpaceValue,
                NormalMapSpace = FormatNormalMapSpace(normalMapSpaceValue),
                UseGlobalMipBias = GetBool(root, "m_EnableGlobalMipBias"),
                MipSamplingModeValue = mipSamplingModeValue,
                MipSamplingMode = FormatTexture2DMipSamplingMode(mipSamplingModeValue)
            };
        }

        static ShaderGraphMultiplyNodeSettingsData ParseMultiplyNodeSettings(JsonElement root)
        {
            var multiplyTypeValue = GetInt(root, "m_MultiplyType");

            return new ShaderGraphMultiplyNodeSettingsData
            {
                MultiplyTypeValue = multiplyTypeValue,
                MultiplyType = FormatMultiplyType(multiplyTypeValue)
            };
        }

        static ShaderGraphSpaceNodeSettingsData ParseSpaceNodeSettings(JsonElement root)
        {
            var spaceValue = GetInt(root, "m_Space");

            return new ShaderGraphSpaceNodeSettingsData
            {
                SpaceValue = spaceValue,
                Space = FormatCoordinateSpace(spaceValue)
            };
        }

        static ShaderGraphPositionNodeSettingsData ParsePositionNodeSettings(JsonElement root)
        {
            var spaceValue = GetInt(root, "m_Space");
            var positionSourceValue = GetInt(root, "m_PositionSource");

            return new ShaderGraphPositionNodeSettingsData
            {
                SpaceValue = spaceValue,
                Space = FormatCoordinateSpace(spaceValue),
                PositionSourceValue = positionSourceValue,
                PositionSource = FormatPositionSource(positionSourceValue)
            };
        }

        static ShaderGraphTransformNodeSettingsData ParseTransformNodeSettings(JsonElement root)
        {
            var inputSpaceValue = GetIntAt(root, "m_Conversion", "from");
            var outputSpaceValue = GetIntAt(root, "m_Conversion", "to");
            var transformTypeValue = GetInt(root, "m_ConversionType");

            return new ShaderGraphTransformNodeSettingsData
            {
                InputSpaceValue = inputSpaceValue,
                InputSpace = FormatCoordinateSpace(inputSpaceValue),
                OutputSpaceValue = outputSpaceValue,
                OutputSpace = FormatCoordinateSpace(outputSpaceValue),
                TransformTypeValue = transformTypeValue,
                TransformType = FormatTransformType(transformTypeValue),
                Normalize = GetBool(root, "m_Normalize")
            };
        }

        static ShaderGraphGradientNoiseNodeSettingsData ParseGradientNoiseNodeSettings(JsonElement root)
        {
            var hashTypeValue = GetInt(root, "m_HashType");

            return new ShaderGraphGradientNoiseNodeSettingsData
            {
                HashTypeValue = hashTypeValue,
                HashType = FormatGradientNoiseHashType(hashTypeValue)
            };
        }

        static ShaderGraphUvNodeSettingsData ParseUvNodeSettings(JsonElement root)
        {
            var channelValue = GetInt(root, "m_OutputChannel");

            return new ShaderGraphUvNodeSettingsData
            {
                ChannelValue = channelValue,
                Channel = FormatUvChannel(channelValue)
            };
        }

        static ShaderGraphScreenPositionNodeSettingsData ParseScreenPositionNodeSettings(JsonElement root)
        {
            var modeValue = GetInt(root, "m_ScreenSpaceType");

            return new ShaderGraphScreenPositionNodeSettingsData
            {
                ModeValue = modeValue,
                Mode = FormatScreenPositionMode(modeValue)
            };
        }

        static ShaderGraphSceneDepthNodeSettingsData ParseSceneDepthNodeSettings(JsonElement root)
        {
            var samplingModeValue = GetInt(root, "m_DepthSamplingMode");

            return new ShaderGraphSceneDepthNodeSettingsData
            {
                SamplingModeValue = samplingModeValue,
                SamplingMode = FormatSceneDepthSamplingMode(samplingModeValue)
            };
        }

        static ShaderGraphComparisonNodeSettingsData ParseComparisonNodeSettings(JsonElement root)
        {
            var comparisonTypeValue = GetInt(root, "m_ComparisonType");

            return new ShaderGraphComparisonNodeSettingsData
            {
                ComparisonTypeValue = comparisonTypeValue,
                ComparisonType = FormatComparisonType(comparisonTypeValue)
            };
        }

        static ShaderGraphNormalFromHeightNodeSettingsData ParseNormalFromHeightNodeSettings(JsonElement root)
        {
            var outputSpaceValue = GetInt(root, "m_OutputSpace");

            return new ShaderGraphNormalFromHeightNodeSettingsData
            {
                OutputSpaceValue = outputSpaceValue,
                OutputSpace = FormatNormalFromHeightOutputSpace(outputSpaceValue)
            };
        }

        static ShaderGraphBlendNodeSettingsData ParseBlendNodeSettings(JsonElement root)
        {
            var blendModeValue = GetInt(root, "m_BlendMode");

            return new ShaderGraphBlendNodeSettingsData
            {
                BlendModeValue = blendModeValue,
                BlendMode = FormatBlendMode(blendModeValue)
            };
        }

        static ShaderGraphSwizzleNodeSettingsData ParseSwizzleNodeSettings(JsonElement root)
        {
            return new ShaderGraphSwizzleNodeSettingsData
            {
                Mask = GetString(root, "_maskInput"),
                NormalizedMask = GetString(root, "convertedMask")
            };
        }

        static void PopulateSlotDerivedNodeSettings(ShaderGraphNodeDefinitionData node)
        {
            if (node.GradientNoise != null)
                node.GradientNoise.Scale = ParseSlotFloatValue(node, "Scale");

            if (node.SimpleNoise != null)
                node.SimpleNoise.Scale = ParseSlotFloatValue(node, "Scale");

            if (node.NormalFromHeight != null)
                node.NormalFromHeight.Strength = ParseSlotFloatValue(node, "Strength");

            if (node.Vector2 != null)
            {
                node.Vector2.X = ParseSlotFloatValue(node, "X");
                node.Vector2.Y = ParseSlotFloatValue(node, "Y");
            }

            if (node.Multiply != null)
            {
                node.Multiply.A = ParseSlotVector4Value(node, "A");
                node.Multiply.B = ParseSlotVector4Value(node, "B");
            }

            if (node.Remap != null)
            {
                node.Remap.Input = ParseSlotVector4Value(node, "In");
                node.Remap.InMinMax = ParseSlotVector2Value(node, "In Min Max");
                node.Remap.OutMinMax = ParseSlotVector2Value(node, "Out Min Max");
            }
        }

        static float? ParseSlotFloatValue(ShaderGraphNodeDefinitionData node, string slotDisplayName)
        {
            var slot = node.Slots?.FirstOrDefault(s => string.Equals(s.DisplayName, slotDisplayName, StringComparison.Ordinal));
            if (slot == null)
                return null;

            return ParseScalarJson(slot.ValueJson) ?? ParseScalarJson(slot.DefaultValueJson);
        }

        static ShaderGraphVector2SlotValueData? ParseSlotVector2Value(ShaderGraphNodeDefinitionData node, string slotDisplayName)
        {
            var slot = node.Slots?.FirstOrDefault(s => string.Equals(s.DisplayName, slotDisplayName, StringComparison.Ordinal));
            if (slot == null)
                return null;

            return ParseVector2Json(slot.ValueJson) ?? ParseVector2Json(slot.DefaultValueJson);
        }

        static ShaderGraphVector4SlotValueData? ParseSlotVector4Value(ShaderGraphNodeDefinitionData node, string slotDisplayName)
        {
            var slot = node.Slots?.FirstOrDefault(s => string.Equals(s.DisplayName, slotDisplayName, StringComparison.Ordinal));
            if (slot == null)
                return null;

            return ParseVector4Json(slot.ValueJson) ?? ParseVector4Json(slot.DefaultValueJson);
        }

        static float? ParseScalarJson(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var document = JsonDocument.Parse(json!);
                if (document.RootElement.ValueKind != JsonValueKind.Number)
                    return null;

                if (document.RootElement.TryGetSingle(out var singleValue))
                    return singleValue;

                return document.RootElement.TryGetDouble(out var doubleValue)
                    ? (float)doubleValue
                    : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        static ShaderGraphVector2SlotValueData? ParseVector2Json(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var document = JsonDocument.Parse(json!);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return null;

                return new ShaderGraphVector2SlotValueData
                {
                    X = TryReadFloatComponent(document.RootElement, "x"),
                    Y = TryReadFloatComponent(document.RootElement, "y")
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }

        static ShaderGraphVector4SlotValueData? ParseVector4Json(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return null;

            try
            {
                using var document = JsonDocument.Parse(json!);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                    return null;

                return new ShaderGraphVector4SlotValueData
                {
                    X = TryReadFloatComponent(document.RootElement, "x") ?? TryReadFloatComponent(document.RootElement, "e00"),
                    Y = TryReadFloatComponent(document.RootElement, "y") ?? TryReadFloatComponent(document.RootElement, "e01"),
                    Z = TryReadFloatComponent(document.RootElement, "z") ?? TryReadFloatComponent(document.RootElement, "e02"),
                    W = TryReadFloatComponent(document.RootElement, "w") ?? TryReadFloatComponent(document.RootElement, "e03")
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }

        static float? TryReadFloatComponent(JsonElement root, string componentName)
        {
            if (!root.TryGetProperty(componentName, out var component) || component.ValueKind != JsonValueKind.Number)
                return null;

            if (component.TryGetSingle(out var singleValue))
                return singleValue;

            return component.TryGetDouble(out var doubleValue)
                ? (float)doubleValue
                : null;
        }

        static ShaderGraphSlotDefinitionData ParseSlotDefinition(JsonElement root, string objectId)
        {
            var slotType = GetString(root, "m_Type");
            var textureDefaultTypeValue = GetInt(root, "m_DefaultType");
            var textureAssetGuid = ParseTexture2DInputSlotAssetGuid(root, slotType);

            return new ShaderGraphSlotDefinitionData
            {
                ObjectId = objectId,
                Type = slotType,
                SlotId = GetInt(root, "m_Id"),
                DisplayName = GetString(root, "m_DisplayName"),
                SlotType = GetInt(root, "m_SlotType"),
                ShaderOutputName = GetString(root, "m_ShaderOutputName"),
                StageCapability = GetInt(root, "m_StageCapability"),
                Hidden = GetBool(root, "m_Hidden") ?? false,
                ValueJson = GetRawText(root, "m_Value"),
                DefaultValueJson = GetRawText(root, "m_DefaultValue"),
                TextureDefaultTypeValue = string.Equals(slotType, "UnityEditor.ShaderGraph.Texture2DInputMaterialSlot", StringComparison.Ordinal)
                    ? textureDefaultTypeValue
                    : null,
                TextureDefaultType = string.Equals(slotType, "UnityEditor.ShaderGraph.Texture2DInputMaterialSlot", StringComparison.Ordinal)
                    ? FormatTexture2DDefaultType(textureDefaultTypeValue)
                    : null,
                TextureAssetGuid = textureAssetGuid,
                TextureAssetPath = string.IsNullOrWhiteSpace(textureAssetGuid)
                    ? null
                    : AssetDatabase.GUIDToAssetPath(textureAssetGuid)
            };
        }

        static ShaderGraphTargetDefinitionData ParseTargetDefinition(JsonElement root, string objectId)
        {
            return new ShaderGraphTargetDefinitionData
            {
                ObjectId = objectId,
                Type = GetString(root, "m_Type"),
                ActiveSubTargetId = GetStringAt(root, "m_ActiveSubTarget", "m_Id"),
                DataObjectIds = GetIdArray(root, "m_Datas")
            };
        }

        static ShaderGraphContextDefinitionData? ParseContextDefinition(JsonElement root, string propertyName)
        {
            if (!TryGetPropertyByPath(root, out var context, propertyName) || context.ValueKind != JsonValueKind.Object)
                return null;

            return new ShaderGraphContextDefinitionData
            {
                PositionX = GetFloatAt(context, "m_Position", "x") ?? 0f,
                PositionY = GetFloatAt(context, "m_Position", "y") ?? 0f,
                BlockNodeIds = GetIdArray(context, "m_Blocks")
            };
        }

        static List<ShaderGraphEdgeDefinitionData>? ParseEdgeDefinitions(JsonElement root)
        {
            if (!TryGetPropertyByPath(root, out var edges, "m_Edges") || edges.ValueKind != JsonValueKind.Array)
                return null;

            var result = new List<ShaderGraphEdgeDefinitionData>();
            foreach (var edge in edges.EnumerateArray())
            {
                result.Add(new ShaderGraphEdgeDefinitionData
                {
                    OutputNodeId = GetStringAt(edge, "m_OutputSlot", "m_Node", "m_Id"),
                    OutputSlotId = GetIntAt(edge, "m_OutputSlot", "m_SlotId"),
                    InputNodeId = GetStringAt(edge, "m_InputSlot", "m_Node", "m_Id"),
                    InputSlotId = GetIntAt(edge, "m_InputSlot", "m_SlotId")
                });
            }

            return result;
        }

        static bool IsSlotDefinition(JsonElement root)
        {
            return root.TryGetProperty("m_SlotType", out _)
                && root.TryGetProperty("m_DisplayName", out _)
                && root.TryGetProperty("m_Id", out _);
        }

        static List<string> GetIdArray(JsonElement root, string propertyName)
        {
            if (!TryGetPropertyByPath(root, out var property, propertyName) || property.ValueKind != JsonValueKind.Array)
                return new List<string>();

            var result = new List<string>();
            foreach (var item in property.EnumerateArray())
            {
                var id = GetString(item, "m_Id");
                if (!string.IsNullOrEmpty(id))
                    result.Add(id!);
            }

            return result;
        }

        static int CountArray(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
                return 0;

            var count = 0;
            foreach (var _ in property.EnumerateArray())
                count++;

            return count;
        }

        static int? GetInt(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property))
                return null;

            return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
                ? value
                : null;
        }

        static int? GetIntAt(JsonElement root, params string[] propertyPath)
        {
            if (!TryGetPropertyByPath(root, out var property, propertyPath))
                return null;

            return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out var value)
                ? value
                : null;
        }

        static string? GetString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property))
                return null;

            return property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        static string? GetStringAt(JsonElement root, params string[] propertyPath)
        {
            if (!TryGetPropertyByPath(root, out var property, propertyPath))
                return null;

            return property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
        }

        static float? GetFloatAt(JsonElement root, params string[] propertyPath)
        {
            if (!TryGetPropertyByPath(root, out var property, propertyPath))
                return null;

            if (property.ValueKind != JsonValueKind.Number)
                return null;

            if (property.TryGetSingle(out var singleValue))
                return singleValue;

            return property.TryGetDouble(out var doubleValue)
                ? (float)doubleValue
                : null;
        }

        static bool? GetBool(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False)
                return null;

            return property.GetBoolean();
        }

        static float? GetFloat(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Number)
                return null;

            if (property.TryGetSingle(out var singleValue))
                return singleValue;

            return property.TryGetDouble(out var doubleValue)
                ? (float)doubleValue
                : null;
        }

        static string? GetRawText(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property))
                return null;

            return property.GetRawText();
        }

        static bool TryGetPropertyByPath(JsonElement root, out JsonElement property, params string[] propertyPath)
        {
            property = root;
            for (var i = 0; i < propertyPath.Length; i++)
            {
                if (property.ValueKind != JsonValueKind.Object
                    || !property.TryGetProperty(propertyPath[i], out property))
                {
                    return false;
                }
            }

            return true;
        }

        static string? FormatSampleTexture2DTextureType(int? value)
        {
            return value switch
            {
                0 => "default",
                1 => "normal",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatNormalMapSpace(int? value)
        {
            return value switch
            {
                0 => "tangent",
                1 => "object",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatTexture2DMipSamplingMode(int? value)
        {
            return value switch
            {
                0 => "standard",
                1 => "lod",
                2 => "gradient",
                3 => "bias",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatMultiplyType(int? value)
        {
            return value switch
            {
                0 => "vector",
                1 => "matrix",
                2 => "mixed",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatCoordinateSpace(int? value)
        {
            return value switch
            {
                0 => "object",
                1 => "view",
                2 => "world",
                3 => "tangent",
                4 => "absoluteWorld",
                5 => "screen",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatPositionSource(int? value)
        {
            return value switch
            {
                0 => "default",
                1 => "predisplacement",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatTransformType(int? value)
        {
            return value switch
            {
                0 => "position",
                1 => "direction",
                2 => "normal",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatGradientNoiseHashType(int? value)
        {
            return value switch
            {
                0 => "deterministic",
                1 => "legacyMod",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatUvChannel(int? value)
        {
            return value switch
            {
                0 => "UV0",
                1 => "UV1",
                2 => "UV2",
                3 => "UV3",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatScreenPositionMode(int? value)
        {
            return value switch
            {
                0 => "default",
                1 => "raw",
                2 => "center",
                3 => "tiled",
                4 => "pixel",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatSceneDepthSamplingMode(int? value)
        {
            return value switch
            {
                0 => "linear01",
                1 => "raw",
                2 => "eye",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatComparisonType(int? value)
        {
            return value switch
            {
                0 => "equal",
                1 => "notEqual",
                2 => "less",
                3 => "lessOrEqual",
                4 => "greater",
                5 => "greaterOrEqual",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatNormalFromHeightOutputSpace(int? value)
        {
            return value switch
            {
                0 => "tangent",
                1 => "world",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatBlendMode(int? value)
        {
            return value switch
            {
                0 => "burn",
                1 => "darken",
                2 => "difference",
                3 => "dodge",
                4 => "divide",
                5 => "exclusion",
                6 => "hardLight",
                7 => "hardMix",
                8 => "lighten",
                9 => "linearBurn",
                10 => "linearDodge",
                11 => "linearLight",
                12 => "linearLightAddSub",
                13 => "multiply",
                14 => "negation",
                15 => "overlay",
                16 => "pinLight",
                17 => "screen",
                18 => "softLight",
                19 => "subtract",
                20 => "vividLight",
                21 => "overwrite",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? FormatShaderGraphPropertyKind(string? propertyType)
        {
            return propertyType switch
            {
                "UnityEditor.ShaderGraph.Internal.ColorShaderProperty" => "color",
                "UnityEditor.ShaderGraph.Internal.Vector1ShaderProperty" => "float",
                "UnityEditor.ShaderGraph.Internal.Texture2DShaderProperty" => "texture2D",
                "UnityEditor.ShaderGraph.Internal.Vector2ShaderProperty" => "vector2",
                "UnityEditor.ShaderGraph.Internal.Vector3ShaderProperty" => "vector3",
                "UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty" => "vector4",
                "UnityEditor.ShaderGraph.Internal.BooleanShaderProperty" => "boolean",
                null => null,
                _ => $"unknown({propertyType})"
            };
        }

        static string? FormatTexture2DDefaultType(int? value)
        {
            return value switch
            {
                0 => "white",
                1 => "black",
                2 => "grey",
                3 => "normalMap",
                4 => "linearGrey",
                5 => "red",
                null => null,
                _ => $"unknown({value})"
            };
        }

        static string? ParsePropertyColorHex(JsonElement root, string? propertyType)
        {
            if (!string.Equals(propertyType, "UnityEditor.ShaderGraph.Internal.ColorShaderProperty", StringComparison.Ordinal)
                || !TryGetPropertyByPath(root, out var value, "m_Value")
                || value.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            var r = GetFloatAt(value, "r");
            var g = GetFloatAt(value, "g");
            var b = GetFloatAt(value, "b");
            var a = GetFloatAt(value, "a") ?? 1f;

            if (!r.HasValue || !g.HasValue || !b.HasValue)
                return null;

            return "#" + ColorUtility.ToHtmlStringRGBA(new Color(r.Value, g.Value, b.Value, a));
        }

        static float? ParseVectorPropertyComponent(JsonElement root, string? propertyType, string component)
        {
            return propertyType switch
            {
                "UnityEditor.ShaderGraph.Internal.Vector2ShaderProperty"
                    when component is "x" or "y" => GetFloatAt(root, "m_Value", component),
                "UnityEditor.ShaderGraph.Internal.Vector3ShaderProperty"
                    when component is "x" or "y" or "z" => GetFloatAt(root, "m_Value", component),
                "UnityEditor.ShaderGraph.Internal.Vector4ShaderProperty"
                    when component is "x" or "y" or "z" or "w" => GetFloatAt(root, "m_Value", component),
                _ => null
            };
        }

        static string ResolvePhysicalAssetPath(string assetPath)
        {
            if (assetPath.StartsWith("Assets/", StringComparison.Ordinal))
                return Path.GetFullPath(assetPath);

            if (assetPath.StartsWith("Packages/", StringComparison.Ordinal))
            {
                var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath(assetPath);
                if (packageInfo != null)
                {
                    var packageRoot = $"Packages/{packageInfo.name}";
                    var relativePath = assetPath.Length > packageRoot.Length
                        ? assetPath.Substring(packageRoot.Length).TrimStart('/')
                        : string.Empty;

                    return Path.Combine(packageInfo.resolvedPath, relativePath);
                }
            }

            return Path.GetFullPath(assetPath);
        }

        static IEnumerable<string> EnumerateTopLevelJsonObjects(string sourceText)
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
    }
}
