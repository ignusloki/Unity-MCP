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
using System.Collections.Generic;
using System.ComponentModel;

namespace AIGD
{
    [Description("Expected material texture binding used by Shader Graph texture workflow validation.")]
    public class ShaderGraphExpectedMaterialTextureInput
    {
        [Description("Material texture property name, such as '_BaseMap'.")]
        public string? PropertyName { get; set; }

        [Description("Expected project Texture asset path, such as 'Assets/Textures/BaseMap.png'.")]
        public string? TextureAssetPath { get; set; }
    }

    public class ShaderGraphTextureReferenceData
    {
        [Description("Where the texture reference came from. Values include 'blackboardProperty' and 'nodeSlot'.")]
        public string? SourceKind { get; set; }

        [Description("Owning Shader Graph object id, such as a property object id or node object id.")]
        public string? OwnerObjectId { get; set; }

        [Description("Owning display name, such as the property name or node name.")]
        public string? OwnerName { get; set; }

        [Description("Blackboard property reference name when SourceKind is 'blackboardProperty'.")]
        public string? PropertyReferenceName { get; set; }

        [Description("Slot id when SourceKind is 'nodeSlot'.")]
        public int? SlotId { get; set; }

        [Description("Slot display name when SourceKind is 'nodeSlot'.")]
        public string? SlotName { get; set; }

        [Description("Resolved Texture asset GUID, when available.")]
        public string? TextureAssetGuid { get; set; }

        [Description("Resolved project Texture asset path, when available.")]
        public string? TextureAssetPath { get; set; }

        [Description("Shader Graph texture default type, such as white, black, normalMap, or red.")]
        public string? TextureDefaultType { get; set; }
    }

    public class ShaderGraphMaterialTexturePropertyData
    {
        [Description("Material texture property name.")]
        public string? PropertyName { get; set; }

        [Description("Shader property display description.")]
        public string? Description { get; set; }

        [Description("Shader property flags.")]
        public string? Flags { get; set; }

        [Description("Shader default texture name, when Unity exposes one.")]
        public string? DefaultTextureName { get; set; }

        [Description("Whether the material currently has an assigned texture object for this property.")]
        public bool HasTexture { get; set; }

        [Description("Assigned Texture asset GUID, when the texture resolves to a project asset.")]
        public string? TextureAssetGuid { get; set; }

        [Description("Assigned project Texture asset path, when available.")]
        public string? TextureAssetPath { get; set; }

        [Description("Whether this material texture property was assigned from a matching Shader Graph blackboard texture default during validation.")]
        public bool WasAppliedFromGraph { get; set; }
    }

    public class ShaderGraphTextureExpectationResultData
    {
        [Description("Expectation scope. Values include 'graph' and 'material'.")]
        public string? Scope { get; set; }

        [Description("Material texture property name for material expectations.")]
        public string? PropertyName { get; set; }

        [Description("Expected project Texture asset path.")]
        public string? ExpectedTextureAssetPath { get; set; }

        [Description("Expected project Texture asset GUID, when available.")]
        public string? ExpectedTextureAssetGuid { get; set; }

        [Description("Actual project Texture asset path that was found.")]
        public string? ActualTextureAssetPath { get; set; }

        [Description("Actual project Texture asset GUID that was found.")]
        public string? ActualTextureAssetGuid { get; set; }

        [Description("Whether the expectation was satisfied.")]
        public bool Matched { get; set; }
    }

    public class ShaderGraphTextureWorkflowValidationResultData
    {
        [Description("Reference to the Shader Graph asset.")]
        public AssetObjectRef? GraphReference { get; set; }

        [Description("Project-relative Shader Graph asset path.")]
        public string? GraphAssetPath { get; set; }

        [Description("Reference to the created or overwritten Material asset.")]
        public AssetObjectRef? MaterialReference { get; set; }

        [Description("Project-relative Material asset path.")]
        public string? MaterialAssetPath { get; set; }

        [Description("Compiled Shader Graph shader name.")]
        public string? GraphShaderName { get; set; }

        [Description("Shader name assigned to the material.")]
        public string? MaterialShaderName { get; set; }

        [Description("Whether the material uses the Shader resolved from the Shader Graph asset.")]
        public bool ShaderMatchesGraph { get; set; }

        [Description("Whether texture defaults from Shader Graph blackboard properties were copied into matching material texture properties.")]
        public bool AppliedGraphTextureDefaultsToMaterial { get; set; }

        [Description("Material texture property names assigned from Shader Graph blackboard texture defaults.")]
        public List<string>? AppliedMaterialTexturePropertyNames { get; set; }

        [Description("Texture references found in the Shader Graph source.")]
        public List<ShaderGraphTextureReferenceData>? GraphTextureReferences { get; set; }

        [Description("Texture properties exposed by the created material and their assigned textures.")]
        public List<ShaderGraphMaterialTexturePropertyData>? MaterialTextureProperties { get; set; }

        [Description("Expectation checks requested by the caller.")]
        public List<ShaderGraphTextureExpectationResultData>? Expectations { get; set; }

        [Description("Whether every requested expectation was satisfied.")]
        public bool AllExpectationsMatched { get; set; }

        [Description("Shader Graph structure readback. Null if not requested.")]
        public ShaderGraphStructureData? Structure { get; set; }

        [Description("Shader Graph import and compiled shader diagnostics.")]
        public ShaderGraphData? Graph { get; set; }
    }
}
