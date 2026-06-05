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
                        typesByObjectId[objectId] = objectType;
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
                        activeTargetIds.Add(targetId);
                }
            }
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

        static string? GetString(JsonElement root, string propertyName)
        {
            if (!root.TryGetProperty(propertyName, out var property))
                return null;

            return property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;
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
