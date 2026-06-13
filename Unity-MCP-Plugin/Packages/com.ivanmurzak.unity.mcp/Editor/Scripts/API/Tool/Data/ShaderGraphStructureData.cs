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
    [Description("Read-only structural view of a Shader Graph source file.")]
    public class ShaderGraphStructureData
    {
        [Description("Reference to the imported Shader Graph asset for future operations.")]
        public AssetObjectRef? Reference { get; set; }

        [Description("Project-relative asset path of the Shader Graph source file.")]
        public string? AssetPath { get; set; }

        [Description("Whether the Shader Graph source file was successfully parsed.")]
        public bool SourceParsed { get; set; }

        [Description("Parse error if source parsing failed.")]
        public string? ParseError { get; set; }

        [Description("Root serialized graph type stored in the source file.")]
        public string? GraphType { get; set; }

        [Description("Shader Graph source version from the graph file.")]
        public int? GraphVersion { get; set; }

        [Description("Shader menu/category path declared by the graph source.")]
        public string? ShaderMenuPath { get; set; }

        [Description("Serialized graph precision enum value.")]
        public int? GraphPrecision { get; set; }

        [Description("Serialized graph preview mode enum value.")]
        public int? PreviewMode { get; set; }

        [Description("Serialized output node id if explicitly set.")]
        public string? OutputNodeId { get; set; }

        [Description("Blackboard properties declared by the graph.")]
        public List<ShaderGraphPropertyDefinitionData>? Properties { get; set; }

        [Description("Blackboard categories declared by the graph.")]
        public List<ShaderGraphCategoryDefinitionData>? Categories { get; set; }

        [Description("Nodes declared by the graph.")]
        public List<ShaderGraphNodeDefinitionData>? Nodes { get; set; }

        [Description("Edges declared by the graph.")]
        public List<ShaderGraphEdgeDefinitionData>? Edges { get; set; }

        [Description("Active targets declared by the graph.")]
        public List<ShaderGraphTargetDefinitionData>? Targets { get; set; }

        [Description("Vertex context block references.")]
        public ShaderGraphContextDefinitionData? VertexContext { get; set; }

        [Description("Fragment context block references.")]
        public ShaderGraphContextDefinitionData? FragmentContext { get; set; }
    }

    public class ShaderGraphPropertyDefinitionData
    {
        [Description("Serialized object id of the property object.")]
        public string? ObjectId { get; set; }

        [Description("Serialized property object type.")]
        public string? Type { get; set; }

        [Description("Display name shown in the blackboard.")]
        public string? Name { get; set; }

        [Description("Generated default reference name.")]
        public string? DefaultReferenceName { get; set; }

        [Description("Overridden reference name if set.")]
        public string? OverrideReferenceName { get; set; }

        [Description("Effective reference name used by the generated shader.")]
        public string? EffectiveReferenceName { get; set; }

        [Description("Serialized GUID for the property definition.")]
        public string? Guid { get; set; }

        [Description("Whether the property is hidden.")]
        public bool Hidden { get; set; }

        [Description("Whether the property generates a material property block entry.")]
        public bool GeneratePropertyBlock { get; set; }

        [Description("Serialized property value JSON.")]
        public string? ValueJson { get; set; }

        [Description("Normalized property kind when recognized, such as color, float, texture2D, vector2, vector3, vector4, or boolean.")]
        public string? PropertyKind { get; set; }

        [Description("Formatted color value for color properties. Example: '#FF7A00CC'.")]
        public string? ColorHex { get; set; }

        [Description("Float default value for float properties.")]
        public float? FloatValue { get; set; }

        [Description("Vector X default component for vector properties.")]
        public float? VectorX { get; set; }

        [Description("Vector Y default component for vector properties.")]
        public float? VectorY { get; set; }

        [Description("Vector Z default component for vector properties.")]
        public float? VectorZ { get; set; }

        [Description("Vector W default component for vector properties.")]
        public float? VectorW { get; set; }

        [Description("Boolean default value for boolean properties.")]
        public bool? BooleanValue { get; set; }

        [Description("Texture2D default texture type enum value.")]
        public int? TextureDefaultTypeValue { get; set; }

        [Description("Texture2D default texture type when recognized.")]
        public string? TextureDefaultType { get; set; }

        [Description("Whether Texture2D properties generate tiling and offset data.")]
        public bool? TextureUseTilingAndOffset { get; set; }

        [Description("Whether Texture2D properties generate texel size data.")]
        public bool? TextureUseTexelSize { get; set; }

        [Description("Whether this Texture2D property is marked as the graph's main texture.")]
        public bool? TextureIsMainTexture { get; set; }

        [Description("Whether this Texture2D property is marked HDR.")]
        public bool? TextureIsHdr { get; set; }

        [Description("Whether this Texture2D property is modifiable.")]
        public bool? TextureModifiable { get; set; }

        [Description("Serialized object id of the blackboard category containing this property, if any.")]
        public string? CategoryObjectId { get; set; }

        [Description("Display name of the blackboard category containing this property, if any.")]
        public string? CategoryName { get; set; }

        [Description("Zero-based index of this property inside its blackboard category, if any.")]
        public int? CategoryIndex { get; set; }
    }

    public class ShaderGraphCategoryDefinitionData
    {
        [Description("Serialized object id of the category object.")]
        public string? ObjectId { get; set; }

        [Description("Serialized category object type.")]
        public string? Type { get; set; }

        [Description("Category display name. The default category usually has an empty name.")]
        public string? Name { get; set; }

        [Description("Referenced blackboard property object ids in category order.")]
        public List<string>? PropertyObjectIds { get; set; }
    }

    public class ShaderGraphNodeDefinitionData
    {
        [Description("Serialized object id of the node object.")]
        public string? ObjectId { get; set; }

        [Description("Serialized node object type.")]
        public string? Type { get; set; }

        [Description("Node display name.")]
        public string? Name { get; set; }

        [Description("Serialized group id if the node belongs to a group.")]
        public string? GroupId { get; set; }

        [Description("Serialized node X position.")]
        public float PositionX { get; set; }

        [Description("Serialized node Y position.")]
        public float PositionY { get; set; }

        [Description("Serialized node width.")]
        public float Width { get; set; }

        [Description("Serialized node height.")]
        public float Height { get; set; }

        [Description("Serialized node precision enum value if present.")]
        public int? Precision { get; set; }

        [Description("Serialized descriptor for block nodes if present.")]
        public string? SerializedDescriptor { get; set; }

        [Description("Referenced blackboard property object id for Property nodes if present.")]
        public string? PropertyObjectId { get; set; }

        [Description("Effective referenced blackboard property name for Property nodes if present.")]
        public string? PropertyReferenceName { get; set; }

        [Description("Referenced slot object ids in declaration order.")]
        public List<string>? SlotObjectIds { get; set; }

        [Description("Resolved slot definitions attached to the node.")]
        public List<ShaderGraphSlotDefinitionData>? Slots { get; set; }

        [Description("Resolved Sample Texture 2D settings when the node is a supported Sample Texture 2D node.")]
        public ShaderGraphSampleTexture2DNodeSettingsData? SampleTexture2D { get; set; }

        [Description("Resolved Multiply settings when the node is a supported Multiply node.")]
        public ShaderGraphMultiplyNodeSettingsData? Multiply { get; set; }
    }

    public class ShaderGraphSampleTexture2DNodeSettingsData
    {
        [Description("Serialized texture type enum value.")]
        public int? TextureTypeValue { get; set; }

        [Description("Formatted texture type when recognized.")]
        public string? TextureType { get; set; }

        [Description("Serialized normal map space enum value.")]
        public int? NormalMapSpaceValue { get; set; }

        [Description("Formatted normal map space when recognized.")]
        public string? NormalMapSpace { get; set; }

        [Description("Whether Use Global Mip Bias is enabled.")]
        public bool? UseGlobalMipBias { get; set; }

        [Description("Serialized mip sampling mode enum value.")]
        public int? MipSamplingModeValue { get; set; }

        [Description("Formatted mip sampling mode when recognized.")]
        public string? MipSamplingMode { get; set; }
    }

    public class ShaderGraphMultiplyNodeSettingsData
    {
        [Description("Serialized multiply type enum value.")]
        public int? MultiplyTypeValue { get; set; }

        [Description("Formatted multiply type when recognized.")]
        public string? MultiplyType { get; set; }
    }

    public class ShaderGraphSlotDefinitionData
    {
        [Description("Serialized object id of the slot object.")]
        public string? ObjectId { get; set; }

        [Description("Serialized slot object type.")]
        public string? Type { get; set; }

        [Description("Numeric slot id used by edge connections.")]
        public int? SlotId { get; set; }

        [Description("Slot display name.")]
        public string? DisplayName { get; set; }

        [Description("Serialized slot type enum value.")]
        public int? SlotType { get; set; }

        [Description("Shader output name for the slot if present.")]
        public string? ShaderOutputName { get; set; }

        [Description("Serialized stage-capability enum value if present.")]
        public int? StageCapability { get; set; }

        [Description("Whether the slot is hidden.")]
        public bool Hidden { get; set; }

        [Description("Serialized current slot value JSON.")]
        public string? ValueJson { get; set; }

        [Description("Serialized default slot value JSON.")]
        public string? DefaultValueJson { get; set; }
    }

    public class ShaderGraphEdgeDefinitionData
    {
        [Description("Output node object id.")]
        public string? OutputNodeId { get; set; }

        [Description("Output slot numeric id.")]
        public int? OutputSlotId { get; set; }

        [Description("Input node object id.")]
        public string? InputNodeId { get; set; }

        [Description("Input slot numeric id.")]
        public int? InputSlotId { get; set; }
    }

    public class ShaderGraphTargetDefinitionData
    {
        [Description("Serialized object id of the active target object.")]
        public string? ObjectId { get; set; }

        [Description("Serialized active target object type.")]
        public string? Type { get; set; }

        [Description("Serialized active sub-target id if present.")]
        public string? ActiveSubTargetId { get; set; }

        [Description("Referenced target data object ids.")]
        public List<string>? DataObjectIds { get; set; }

        [Description("Resolved target data object types, if available.")]
        public List<string>? DataObjectTypes { get; set; }
    }

    public class ShaderGraphContextDefinitionData
    {
        [Description("Serialized context X position.")]
        public float PositionX { get; set; }

        [Description("Serialized context Y position.")]
        public float PositionY { get; set; }

        [Description("Referenced block node ids.")]
        public List<string>? BlockNodeIds { get; set; }
    }
}
