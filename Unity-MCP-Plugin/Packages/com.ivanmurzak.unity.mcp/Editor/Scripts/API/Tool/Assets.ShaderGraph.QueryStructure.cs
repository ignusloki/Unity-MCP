/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak/Unity-MCP)    │
│  Copyright (c) 2025 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
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

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        public const string AssetsShaderGraphQueryStructureToolId = "assets-shadergraph-query-structure";

        [AiTool
        (
            AssetsShaderGraphQueryStructureToolId,
            Title = "Assets / Shader Graph / Query Structure",
            ReadOnlyHint = true,
            IdempotentHint = true
        )]
        [AiSkillDescription("Read a filtered subset of a Shader Graph source file. Returns just the slice you need (statsOnly, propertiesOnly, by node id/type/displayName, by edge endpoints) so you do not pay tokens for the whole graph.")]
        [AiSkillBody("Filtered read of a '.shadergraph' asset.\n\n" +
            "## When to call this instead of '" + AssetsShaderGraphGetStructureToolId + "'\n\n" +
            "- You only need counts → set StatsOnly = true (cheapest).\n" +
            "- You only need blackboard data → set PropertiesOnly = true.\n" +
            "- You only need a few specific nodes → set NodeObjectIds / NodeTypeSubstrings / NodeDisplayNames.\n" +
            "- You only care about edges touching a given node set → set EdgesTouchingNodeIds.\n" +
            "- You do not need typed node settings or slot lists → set IncludeNodeSettings = false and/or IncludeSlots = false.\n\n" +
            "## Inputs\n\n" +
            "- `assetRef` — reference to a '.shadergraph' asset.\n" +
            "- `query` — filter object. All fields are optional.\n\n" +
            "## Output\n\n" +
            "Returns Stats (always) plus the projected Properties, Categories, Nodes, Edges, Targets, VertexContext, FragmentContext lists. Lists are null when filtered out.\n\n" +
            "Use '" + AssetsShaderGraphFindToolId + "' to locate a valid graph asset first. Use '" + AssetsShaderGraphGetStructureToolId + "' when you need the full graph.")]
        [Description("Read a filtered subset of a Shader Graph source file.")]
        public ShaderGraphQueryStructureData QueryStructure(AssetObjectRef assetRef, ShaderGraphQueryStructureInput? query)
        {
            if (assetRef == null)
                throw new ArgumentNullException(nameof(assetRef));

            if (!assetRef.IsValid(out var error))
                throw new ArgumentException(error, nameof(assetRef));

            var effectiveQuery = query ?? new ShaderGraphQueryStructureInput();

            return MainThread.Instance.Run(() =>
            {
                var full = BuildShaderGraphStructureData(assetRef);
                return ProjectQueryStructure(full, effectiveQuery);
            });
        }

        internal static ShaderGraphQueryStructureData ProjectQueryStructure(
            ShaderGraphStructureData full,
            ShaderGraphQueryStructureInput query)
        {
            var result = new ShaderGraphQueryStructureData
            {
                Reference = full.Reference,
                AssetPath = full.AssetPath,
                Stats = new ShaderGraphQueryStatsData
                {
                    SourceParsed = full.SourceParsed,
                    ParseError = full.ParseError,
                    NodeCount = full.Nodes?.Count ?? 0,
                    EdgeCount = full.Edges?.Count ?? 0,
                    PropertyCount = full.Properties?.Count ?? 0,
                    CategoryCount = full.Categories?.Count ?? 0,
                    TargetCount = full.Targets?.Count ?? 0
                }
            };

            if (query.StatsOnly == true)
                return result;

            result.Properties = full.Properties;
            result.Categories = full.Categories;

            if (query.PropertiesOnly == true)
                return result;

            var includeSlots = query.IncludeSlots ?? true;
            var includeNodeSettings = query.IncludeNodeSettings ?? true;
            var includeEdges = query.IncludeEdges ?? true;
            var includeTargets = query.IncludeTargets ?? true;

            result.Nodes = FilterNodes(full.Nodes, query, includeSlots, includeNodeSettings);

            if (includeEdges)
                result.Edges = FilterEdges(full.Edges, query.EdgesTouchingNodeIds);

            if (includeTargets)
            {
                result.Targets = full.Targets;
                result.VertexContext = full.VertexContext;
                result.FragmentContext = full.FragmentContext;
            }

            return result;
        }

        static List<ShaderGraphNodeDefinitionData>? FilterNodes(
            List<ShaderGraphNodeDefinitionData>? source,
            ShaderGraphQueryStructureInput query,
            bool includeSlots,
            bool includeNodeSettings)
        {
            if (source == null)
                return null;

            var idFilter = ToOrdinalSet(query.NodeObjectIds);
            var nameFilter = ToOrdinalSet(query.NodeDisplayNames);
            var typeSubstrings = query.NodeTypeSubstrings?
                .Where(s => !string.IsNullOrEmpty(s))
                .ToList();
            var hasAnyFilter = idFilter != null || nameFilter != null || (typeSubstrings != null && typeSubstrings.Count > 0);

            var result = new List<ShaderGraphNodeDefinitionData>(source.Count);
            foreach (var node in source)
            {
                if (hasAnyFilter && !MatchesNodeFilter(node, idFilter, nameFilter, typeSubstrings))
                    continue;

                result.Add(includeSlots && includeNodeSettings
                    ? node
                    : ProjectNode(node, includeSlots, includeNodeSettings));
            }

            return result;
        }

        static List<ShaderGraphEdgeDefinitionData>? FilterEdges(
            List<ShaderGraphEdgeDefinitionData>? source,
            List<string>? edgesTouchingNodeIds)
        {
            if (source == null)
                return null;

            var touching = ToOrdinalSet(edgesTouchingNodeIds);
            if (touching == null)
                return source;

            var result = new List<ShaderGraphEdgeDefinitionData>(source.Count);
            foreach (var edge in source)
            {
                if ((edge.OutputNodeId != null && touching.Contains(edge.OutputNodeId)) ||
                    (edge.InputNodeId != null && touching.Contains(edge.InputNodeId)))
                {
                    result.Add(edge);
                }
            }
            return result;
        }

        static bool MatchesNodeFilter(
            ShaderGraphNodeDefinitionData node,
            HashSet<string>? idFilter,
            HashSet<string>? nameFilter,
            List<string>? typeSubstrings)
        {
            if (idFilter != null && node.ObjectId != null && idFilter.Contains(node.ObjectId))
                return true;
            if (nameFilter != null && node.Name != null && nameFilter.Contains(node.Name))
                return true;
            if (typeSubstrings != null && node.Type != null)
            {
                foreach (var sub in typeSubstrings)
                {
                    if (node.Type.IndexOf(sub, StringComparison.Ordinal) >= 0)
                        return true;
                }
            }
            return false;
        }

        static ShaderGraphNodeDefinitionData ProjectNode(
            ShaderGraphNodeDefinitionData source,
            bool includeSlots,
            bool includeNodeSettings)
        {
            var projected = new ShaderGraphNodeDefinitionData
            {
                ObjectId = source.ObjectId,
                Type = source.Type,
                Name = source.Name,
                GroupId = source.GroupId,
                PositionX = source.PositionX,
                PositionY = source.PositionY,
                Width = source.Width,
                Height = source.Height,
                Precision = source.Precision,
                SerializedDescriptor = source.SerializedDescriptor,
                PropertyObjectId = source.PropertyObjectId,
                PropertyReferenceName = source.PropertyReferenceName,
                SlotObjectIds = source.SlotObjectIds
            };

            if (includeSlots)
                projected.Slots = source.Slots;

            if (includeNodeSettings)
            {
                projected.SampleTexture2D = source.SampleTexture2D;
                projected.Multiply = source.Multiply;
                projected.Remap = source.Remap;
                projected.SourceVector = source.SourceVector;
                projected.Position = source.Position;
                projected.Transform = source.Transform;
                projected.GradientNoise = source.GradientNoise;
                projected.SimpleNoise = source.SimpleNoise;
                projected.Uv = source.Uv;
                projected.ScreenPosition = source.ScreenPosition;
                projected.SceneDepth = source.SceneDepth;
                projected.Comparison = source.Comparison;
                projected.NormalFromHeight = source.NormalFromHeight;
                projected.Blend = source.Blend;
                projected.Swizzle = source.Swizzle;
                projected.Vector2 = source.Vector2;
                projected.Smoothstep = source.Smoothstep;
                projected.InvertColors = source.InvertColors;
                projected.Exponential = source.Exponential;
            }

            return projected;
        }

        static HashSet<string>? ToOrdinalSet(List<string>? source)
        {
            if (source == null || source.Count == 0)
                return null;
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var item in source)
            {
                if (!string.IsNullOrEmpty(item))
                    set.Add(item);
            }
            return set.Count == 0 ? null : set;
        }
    }
}
