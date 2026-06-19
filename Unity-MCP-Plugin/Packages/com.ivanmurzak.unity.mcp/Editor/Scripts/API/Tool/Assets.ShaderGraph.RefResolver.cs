/*
┌──────────────────────────────────────────────────────────────────┐
│  Author: Ivan Murzak (https://github.com/IvanMurzak)             │
│  Repository: GitHub (https://github.com/IvanMurzak)              │
│  Copyright (c) 2025 Ivan Murzak                                  │
│  Licensed under the Apache License, Version 2.0.                 │
│  See the LICENSE file in the project root for more information.  │
└──────────────────────────────────────────────────────────────────┘
*/

#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using AIGD;

namespace com.IvanMurzak.Unity.MCP.Editor.API
{
    public partial class Tool_Assets_ShaderGraph
    {
        // In-batch alias dictionaries. Maps alias -> serialized object id.
        // Passed by the batch tool; single-op tools default to null (no aliases).
        internal sealed class ShaderGraphAliasBag
        {
            public Dictionary<string, string> Nodes { get; } = new(StringComparer.Ordinal);
            public Dictionary<string, string> Properties { get; } = new(StringComparer.Ordinal);
        }

        internal static string ResolveNodeObjectId(
            ShaderGraphNodeRef? nodeRef,
            ShaderGraphStructureData structure,
            ShaderGraphAliasBag? aliases = null,
            string fieldPath = "node")
        {
            if (nodeRef == null)
                throw new ArgumentException($"{fieldPath} reference must be provided.");

            if (!string.IsNullOrWhiteSpace(nodeRef.ObjectId))
                return nodeRef.ObjectId!.Trim();

            if (!string.IsNullOrWhiteSpace(nodeRef.Alias))
            {
                var alias = nodeRef.Alias!.Trim();
                if (aliases == null || !aliases.Nodes.TryGetValue(alias, out var aliased))
                    throw new InvalidOperationException(
                        $"{fieldPath}.alias '{alias}' was not registered earlier in this batch.");
                return aliased;
            }

            if (!string.IsNullOrWhiteSpace(nodeRef.DisplayName))
            {
                var displayName = nodeRef.DisplayName!.Trim();
                var matches = structure.Nodes?
                    .Where(n => string.Equals(n.Name, displayName, StringComparison.Ordinal))
                    .ToList() ?? new List<ShaderGraphNodeDefinitionData>();
                if (matches.Count == 0)
                    throw new InvalidOperationException(
                        $"{fieldPath}.displayName '{displayName}' did not match any node in the current graph.");
                if (matches.Count > 1)
                    throw new InvalidOperationException(
                        $"{fieldPath}.displayName '{displayName}' is ambiguous: {matches.Count} nodes share that name. Use ObjectId or Alias instead.");
                return matches[0].ObjectId
                    ?? throw new InvalidOperationException(
                        $"{fieldPath}.displayName '{displayName}' matched a node with no object id.");
            }

            throw new ArgumentException(
                $"{fieldPath} must specify one of: ObjectId, Alias, or DisplayName.");
        }

        internal static (string NodeObjectId, string SlotObjectId, int SlotId) ResolveSlotRef(
            ShaderGraphSlotRef? slotRef,
            ShaderGraphStructureData structure,
            ShaderGraphAliasBag? aliases = null,
            string fieldPath = "slot")
        {
            if (slotRef == null)
                throw new ArgumentException($"{fieldPath} reference must be provided.");

            if (!string.IsNullOrWhiteSpace(slotRef.ObjectId))
            {
                var slotObjectId = slotRef.ObjectId!.Trim();
                var owner = FindSlotOwnerNode(structure, slotObjectId, fieldPath);
                var slot = owner.Slots!.First(s => string.Equals(s.ObjectId, slotObjectId, StringComparison.Ordinal));
                return (owner.ObjectId!, slotObjectId, slot.SlotId ?? -1);
            }

            if (slotRef.Node == null)
                throw new ArgumentException(
                    $"{fieldPath}.Node is required when resolving a slot by DisplayName.");

            if (string.IsNullOrWhiteSpace(slotRef.DisplayName))
                throw new ArgumentException(
                    $"{fieldPath} must specify either ObjectId or both Node and DisplayName.");

            var nodeObjectId = ResolveNodeObjectId(slotRef.Node, structure, aliases, $"{fieldPath}.Node");
            var node = structure.Nodes?.FirstOrDefault(n =>
                string.Equals(n.ObjectId, nodeObjectId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException(
                    $"{fieldPath}.Node resolved to '{nodeObjectId}' but that node was not found in the current graph.");

            var displayName = slotRef.DisplayName!.Trim();
            var slotMatches = node.Slots?
                .Where(s => string.Equals(s.DisplayName, displayName, StringComparison.Ordinal))
                .ToList() ?? new List<ShaderGraphSlotDefinitionData>();
            if (slotMatches.Count == 0)
                throw new InvalidOperationException(
                    $"{fieldPath}.DisplayName '{displayName}' did not match any slot on node '{node.Name ?? nodeObjectId}'.");
            if (slotMatches.Count > 1)
                throw new InvalidOperationException(
                    $"{fieldPath}.DisplayName '{displayName}' is ambiguous on node '{node.Name ?? nodeObjectId}': {slotMatches.Count} slots share that name.");

            var resolved = slotMatches[0];
            return (nodeObjectId,
                resolved.ObjectId ?? throw new InvalidOperationException(
                    $"{fieldPath}.DisplayName '{displayName}' matched a slot with no object id."),
                resolved.SlotId ?? -1);
        }

        internal static string ResolvePropertyObjectId(
            ShaderGraphPropertyRef? propertyRef,
            ShaderGraphStructureData structure,
            ShaderGraphAliasBag? aliases = null,
            string fieldPath = "property")
        {
            if (propertyRef == null)
                throw new ArgumentException($"{fieldPath} reference must be provided.");

            if (!string.IsNullOrWhiteSpace(propertyRef.ObjectId))
                return propertyRef.ObjectId!.Trim();

            if (!string.IsNullOrWhiteSpace(propertyRef.Alias))
            {
                var alias = propertyRef.Alias!.Trim();
                if (aliases == null || !aliases.Properties.TryGetValue(alias, out var aliased))
                    throw new InvalidOperationException(
                        $"{fieldPath}.alias '{alias}' was not registered earlier in this batch.");
                return aliased;
            }

            if (!string.IsNullOrWhiteSpace(propertyRef.ReferenceName))
            {
                var referenceName = propertyRef.ReferenceName!.Trim();
                var matches = structure.Properties?
                    .Where(p => string.Equals(p.EffectiveReferenceName, referenceName, StringComparison.Ordinal))
                    .ToList() ?? new List<ShaderGraphPropertyDefinitionData>();
                if (matches.Count == 0)
                    throw new InvalidOperationException(
                        $"{fieldPath}.referenceName '{referenceName}' did not match any property in the current graph.");
                if (matches.Count > 1)
                    throw new InvalidOperationException(
                        $"{fieldPath}.referenceName '{referenceName}' is ambiguous: {matches.Count} properties share that reference name. Use ObjectId or Alias instead.");
                return matches[0].ObjectId
                    ?? throw new InvalidOperationException(
                        $"{fieldPath}.referenceName '{referenceName}' matched a property with no object id.");
            }

            if (!string.IsNullOrWhiteSpace(propertyRef.DisplayName))
            {
                var displayName = propertyRef.DisplayName!.Trim();
                var matches = structure.Properties?
                    .Where(p => string.Equals(p.Name, displayName, StringComparison.Ordinal))
                    .ToList() ?? new List<ShaderGraphPropertyDefinitionData>();
                if (matches.Count == 0)
                    throw new InvalidOperationException(
                        $"{fieldPath}.displayName '{displayName}' did not match any property in the current graph.");
                if (matches.Count > 1)
                    throw new InvalidOperationException(
                        $"{fieldPath}.displayName '{displayName}' is ambiguous: {matches.Count} properties share that display name. Use ObjectId, Alias, or ReferenceName instead.");
                return matches[0].ObjectId
                    ?? throw new InvalidOperationException(
                        $"{fieldPath}.displayName '{displayName}' matched a property with no object id.");
            }

            throw new ArgumentException(
                $"{fieldPath} must specify one of: ObjectId, Alias, ReferenceName, or DisplayName.");
        }

        static ShaderGraphNodeDefinitionData FindSlotOwnerNode(
            ShaderGraphStructureData structure,
            string slotObjectId,
            string fieldPath)
        {
            var owner = structure.Nodes?.FirstOrDefault(n =>
                n.Slots?.Any(s => string.Equals(s.ObjectId, slotObjectId, StringComparison.Ordinal)) == true);
            if (owner == null)
                throw new InvalidOperationException(
                    $"{fieldPath}.ObjectId '{slotObjectId}' did not match any slot in the current graph.");
            return owner;
        }
    }
}
