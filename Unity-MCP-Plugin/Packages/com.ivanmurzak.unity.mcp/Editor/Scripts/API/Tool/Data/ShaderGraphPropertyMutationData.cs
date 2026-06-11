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
    [Description("Structured input for updating an existing Shader Graph blackboard property.")]
    public class ShaderGraphPropertyUpdateInput
    {
        [Description("Serialized object id of the property to update. Optional if propertyReferenceName is provided.")]
        public string? PropertyObjectId { get; set; }

        [Description("Effective property reference name to update, such as '_BaseColor'. Optional if propertyObjectId is provided.")]
        public string? PropertyReferenceName { get; set; }

        [Description("New display name for the property.")]
        public string? DisplayName { get; set; }

        [Description("New override reference name for the property, such as '_TintColor'.")]
        public string? OverrideReferenceName { get; set; }

        [Description("Whether the property should be hidden.")]
        public bool? Hidden { get; set; }

        [Description("Whether the property should generate a material property block entry.")]
        public bool? GeneratePropertyBlock { get; set; }

        [Description("New default color for ColorShaderProperty values. Example: '#FF7A00CC'. Only supported for color properties.")]
        public string? ColorHex { get; set; }

        [Description("New default float value. Only supported for float properties.")]
        public float? FloatValue { get; set; }

        [Description("New vector X component. Supported for vector2, vector3, and vector4 properties.")]
        public float? VectorX { get; set; }

        [Description("New vector Y component. Supported for vector2, vector3, and vector4 properties.")]
        public float? VectorY { get; set; }

        [Description("New vector Z component. Supported for vector3 and vector4 properties.")]
        public float? VectorZ { get; set; }

        [Description("New vector W component. Supported for vector4 properties.")]
        public float? VectorW { get; set; }

        [Description("New default boolean value. Only supported for boolean properties.")]
        public bool? BooleanValue { get; set; }

        [Description("New Texture2D default texture type. Supported values: white, black, grey, normalMap, bump, linearGrey, red. Only supported for texture2D properties.")]
        public string? TextureDefaultType { get; set; }

        [Description("Whether Texture2D properties should generate tiling and offset data.")]
        public bool? TextureUseTilingAndOffset { get; set; }

        [Description("Whether Texture2D properties should generate texel size data.")]
        public bool? TextureUseTexelSize { get; set; }

        [Description("Whether the Texture2D property should be marked as the graph's main texture.")]
        public bool? TextureIsMainTexture { get; set; }

        [Description("Whether the Texture2D property should be marked HDR.")]
        public bool? TextureIsHdr { get; set; }

        [Description("Whether the Texture2D property should be modifiable.")]
        public bool? TextureModifiable { get; set; }
    }

    [Description("Structured input for adding a new Shader Graph blackboard property.")]
    public class ShaderGraphAddPropertyInput
    {
        [Description("Property type to create. Supported values: color, float, texture2D, vector2, vector3, vector4, boolean.")]
        public string? PropertyType { get; set; }

        [Description("Display name for the new property.")]
        public string? DisplayName { get; set; }

        [Description("Optional override reference name, such as '_MyColor'. If omitted, a default name is generated from the display name.")]
        public string? OverrideReferenceName { get; set; }

        [Description("Whether the new property should be hidden.")]
        public bool? Hidden { get; set; }

        [Description("Whether the new property should generate a material property block entry. Default: true.")]
        public bool? GeneratePropertyBlock { get; set; }

        [Description("Default color for a color property. Example: '#FF7A00CC'. Used only when propertyType is color.")]
        public string? ColorHex { get; set; }

        [Description("Default float value for a float property. Used only when propertyType is float.")]
        public float? FloatValue { get; set; }

        [Description("Default vector X component. Used when propertyType is vector2, vector3, or vector4.")]
        public float? VectorX { get; set; }

        [Description("Default vector Y component. Used when propertyType is vector2, vector3, or vector4.")]
        public float? VectorY { get; set; }

        [Description("Default vector Z component. Used when propertyType is vector3 or vector4.")]
        public float? VectorZ { get; set; }

        [Description("Default vector W component. Used when propertyType is vector4.")]
        public float? VectorW { get; set; }

        [Description("Default boolean value. Used only when propertyType is boolean.")]
        public bool? BooleanValue { get; set; }

        [Description("Texture2D default texture type. Supported values: white, black, grey, normalMap, bump, linearGrey, red. Used only when propertyType is texture2D.")]
        public string? TextureDefaultType { get; set; }

        [Description("Whether Texture2D properties should generate tiling and offset data. Default: false.")]
        public bool? TextureUseTilingAndOffset { get; set; }

        [Description("Whether Texture2D properties should generate texel size data. Default: true.")]
        public bool? TextureUseTexelSize { get; set; }

        [Description("Whether the Texture2D property should be marked as the graph's main texture. Default: false.")]
        public bool? TextureIsMainTexture { get; set; }

        [Description("Whether the Texture2D property should be marked HDR. Default: false.")]
        public bool? TextureIsHdr { get; set; }

        [Description("Whether the Texture2D property should be modifiable. Default: true.")]
        public bool? TextureModifiable { get; set; }
    }

    [Description("Result of updating a Shader Graph blackboard property and re-importing the graph.")]
    public class ShaderGraphPropertyMutationResultData
    {
        [Description("Updated property snapshot after the mutation was applied.")]
        public ShaderGraphPropertyDefinitionData? Property { get; set; }

        [Description("Updated read-only graph structure after the mutation.")]
        public ShaderGraphStructureData? Structure { get; set; }

        [Description("Post-import Shader Graph summary and diagnostics.")]
        public ShaderGraphData? Graph { get; set; }

        [Description("List of property fields that actually changed.")]
        public List<string>? ChangedFields { get; set; }
    }
}
