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

        [Description("Serialized GUID of the default Texture2D asset assigned to this property, when present.")]
        public string? TextureAssetGuid { get; set; }

        [Description("Project asset path of the default Texture2D asset assigned to this property, when present.")]
        public string? TextureAssetPath { get; set; }

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

        [Description("Resolved Remap settings when the node is a supported Remap node.")]
        public ShaderGraphRemapNodeSettingsData? Remap { get; set; }

        [Description("Resolved source-vector settings when the node exposes a coordinate-space selector.")]
        public ShaderGraphSpaceNodeSettingsData? SourceVector { get; set; }

        [Description("Resolved Position settings when the node is a supported Position node.")]
        public ShaderGraphPositionNodeSettingsData? Position { get; set; }

        [Description("Resolved Transform settings when the node is a supported Transform node.")]
        public ShaderGraphTransformNodeSettingsData? Transform { get; set; }

        [Description("Resolved Gradient Noise settings when the node is a supported Gradient Noise node.")]
        public ShaderGraphGradientNoiseNodeSettingsData? GradientNoise { get; set; }

        [Description("Resolved Simple Noise settings when the node is a supported Simple Noise node.")]
        public ShaderGraphSimpleNoiseNodeSettingsData? SimpleNoise { get; set; }

        [Description("Resolved UV settings when the node is a supported UV node.")]
        public ShaderGraphUvNodeSettingsData? Uv { get; set; }

        [Description("Resolved Screen Position settings when the node is a supported Screen Position node.")]
        public ShaderGraphScreenPositionNodeSettingsData? ScreenPosition { get; set; }

        [Description("Resolved Scene Depth settings when the node is a supported Scene Depth node.")]
        public ShaderGraphSceneDepthNodeSettingsData? SceneDepth { get; set; }

        [Description("Resolved Comparison settings when the node is a supported Comparison node.")]
        public ShaderGraphComparisonNodeSettingsData? Comparison { get; set; }

        [Description("Resolved Normal From Height settings when the node is a supported Normal From Height node.")]
        public ShaderGraphNormalFromHeightNodeSettingsData? NormalFromHeight { get; set; }

        [Description("Resolved Blend settings when the node is a supported Blend node.")]
        public ShaderGraphBlendNodeSettingsData? Blend { get; set; }

        [Description("Resolved Swizzle settings when the node is a supported Swizzle node.")]
        public ShaderGraphSwizzleNodeSettingsData? Swizzle { get; set; }

        [Description("Resolved Vector 2 settings when the node is a supported Vector 2 node.")]
        public ShaderGraphVector2NodeSettingsData? Vector2 { get; set; }

        [Description("Resolved Smoothstep settings when the node is a supported Smoothstep node.")]
        public ShaderGraphSmoothstepNodeSettingsData? Smoothstep { get; set; }

        [Description("Resolved Step settings when the node is a supported Step node.")]
        public ShaderGraphStepNodeSettingsData? Step { get; set; }

        [Description("Resolved Invert Colors settings when the node is a supported Invert Colors node.")]
        public ShaderGraphInvertColorsNodeSettingsData? InvertColors { get; set; }

        [Description("Resolved Exponential settings when the node is a supported Exponential node.")]
        public ShaderGraphExponentialNodeSettingsData? Exponential { get; set; }
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

        [Description("Default value for the A input slot when readable.")]
        public ShaderGraphVector4SlotValueData? A { get; set; }

        [Description("Default value for the B input slot when readable.")]
        public ShaderGraphVector4SlotValueData? B { get; set; }
    }

    public class ShaderGraphRemapNodeSettingsData
    {
        [Description("Default value for the In input slot when readable.")]
        public ShaderGraphVector4SlotValueData? Input { get; set; }

        [Description("Default value for the In Min Max input slot when readable.")]
        public ShaderGraphVector2SlotValueData? InMinMax { get; set; }

        [Description("Default value for the Out Min Max input slot when readable.")]
        public ShaderGraphVector2SlotValueData? OutMinMax { get; set; }
    }

    public class ShaderGraphVector2SlotValueData
    {
        [Description("X component.")]
        public float? X { get; set; }

        [Description("Y component.")]
        public float? Y { get; set; }
    }

    public class ShaderGraphVector4SlotValueData
    {
        [Description("X component.")]
        public float? X { get; set; }

        [Description("Y component.")]
        public float? Y { get; set; }

        [Description("Z component.")]
        public float? Z { get; set; }

        [Description("W component.")]
        public float? W { get; set; }
    }

    public class ShaderGraphSpaceNodeSettingsData
    {
        [Description("Serialized coordinate space enum value.")]
        public int? SpaceValue { get; set; }

        [Description("Formatted coordinate space when recognized.")]
        public string? Space { get; set; }
    }

    public class ShaderGraphPositionNodeSettingsData
    {
        [Description("Serialized coordinate space enum value.")]
        public int? SpaceValue { get; set; }

        [Description("Formatted coordinate space when recognized.")]
        public string? Space { get; set; }

        [Description("Serialized position source enum value.")]
        public int? PositionSourceValue { get; set; }

        [Description("Formatted position source when recognized.")]
        public string? PositionSource { get; set; }
    }

    public class ShaderGraphTransformNodeSettingsData
    {
        [Description("Serialized input coordinate space enum value.")]
        public int? InputSpaceValue { get; set; }

        [Description("Formatted input coordinate space when recognized.")]
        public string? InputSpace { get; set; }

        [Description("Serialized output coordinate space enum value.")]
        public int? OutputSpaceValue { get; set; }

        [Description("Formatted output coordinate space when recognized.")]
        public string? OutputSpace { get; set; }

        [Description("Serialized transform conversion type enum value.")]
        public int? TransformTypeValue { get; set; }

        [Description("Formatted transform conversion type when recognized.")]
        public string? TransformType { get; set; }

        [Description("Whether the transform result is normalized where the selected transform type supports it.")]
        public bool? Normalize { get; set; }
    }

    public class ShaderGraphGradientNoiseNodeSettingsData
    {
        [Description("Serialized hash type enum value.")]
        public int? HashTypeValue { get; set; }

        [Description("Formatted hash type when recognized.")]
        public string? HashType { get; set; }

        [Description("Default value for the Scale input slot when readable.")]
        public float? Scale { get; set; }
    }

    public class ShaderGraphSimpleNoiseNodeSettingsData
    {
        [Description("Default value for the Scale input slot when readable.")]
        public float? Scale { get; set; }
    }

    public class ShaderGraphUvNodeSettingsData
    {
        [Description("Serialized UV channel enum value.")]
        public int? ChannelValue { get; set; }

        [Description("Formatted UV channel when recognized.")]
        public string? Channel { get; set; }
    }

    public class ShaderGraphScreenPositionNodeSettingsData
    {
        [Description("Serialized screen-space mode enum value.")]
        public int? ModeValue { get; set; }

        [Description("Formatted screen-space mode when recognized.")]
        public string? Mode { get; set; }
    }

    public class ShaderGraphSceneDepthNodeSettingsData
    {
        [Description("Serialized scene-depth sampling mode enum value.")]
        public int? SamplingModeValue { get; set; }

        [Description("Formatted scene-depth sampling mode when recognized.")]
        public string? SamplingMode { get; set; }
    }

    public class ShaderGraphComparisonNodeSettingsData
    {
        [Description("Serialized comparison operator enum value.")]
        public int? ComparisonTypeValue { get; set; }

        [Description("Formatted comparison operator when recognized.")]
        public string? ComparisonType { get; set; }
    }

    public class ShaderGraphNormalFromHeightNodeSettingsData
    {
        [Description("Serialized output-space enum value.")]
        public int? OutputSpaceValue { get; set; }

        [Description("Formatted output space when recognized.")]
        public string? OutputSpace { get; set; }

        [Description("Default value for the Strength input slot when readable.")]
        public float? Strength { get; set; }
    }

    public class ShaderGraphBlendNodeSettingsData
    {
        [Description("Serialized blend mode enum value.")]
        public int? BlendModeValue { get; set; }

        [Description("Formatted blend mode when recognized.")]
        public string? BlendMode { get; set; }
    }

    public class ShaderGraphSwizzleNodeSettingsData
    {
        [Description("Serialized swizzle mask string.")]
        public string? Mask { get; set; }

        [Description("Normalized swizzle mask after Unity resolves rgba aliases to xyzw when available.")]
        public string? NormalizedMask { get; set; }
    }

    public class ShaderGraphVector2NodeSettingsData
    {
        [Description("Default value for the X input slot when readable.")]
        public float? X { get; set; }

        [Description("Default value for the Y input slot when readable.")]
        public float? Y { get; set; }
    }

    public class ShaderGraphSmoothstepNodeSettingsData
    {
        [Description("Default value for the Edge1 input slot when readable.")]
        public ShaderGraphVector4SlotValueData? Edge1 { get; set; }

        [Description("Default value for the Edge2 input slot when readable.")]
        public ShaderGraphVector4SlotValueData? Edge2 { get; set; }

        [Description("Default value for the In input slot when readable.")]
        public ShaderGraphVector4SlotValueData? Input { get; set; }
    }

    public class ShaderGraphStepNodeSettingsData
    {
        [Description("Default value for the Edge input slot when readable.")]
        public ShaderGraphVector4SlotValueData? Edge { get; set; }

        [Description("Default value for the In input slot when readable.")]
        public ShaderGraphVector4SlotValueData? Input { get; set; }
    }

    public class ShaderGraphInvertColorsNodeSettingsData
    {
        [Description("Whether the Red channel is inverted.")]
        public bool? Red { get; set; }

        [Description("Whether the Green channel is inverted.")]
        public bool? Green { get; set; }

        [Description("Whether the Blue channel is inverted.")]
        public bool? Blue { get; set; }

        [Description("Whether the Alpha channel is inverted, when Unity serializes it. Current Unity Shader Graph packages may leave this unavailable.")]
        public bool? Alpha { get; set; }
    }

    public class ShaderGraphExponentialNodeSettingsData
    {
        [Description("Serialized exponential base enum value.")]
        public int? BaseValue { get; set; }

        [Description("Formatted exponential base when recognized.")]
        public string? Base { get; set; }

        [Description("Default value for the In input slot when readable.")]
        public ShaderGraphVector4SlotValueData? Input { get; set; }
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

        [Description("Texture slot default texture type enum value, when this is a texture input slot.")]
        public int? TextureDefaultTypeValue { get; set; }

        [Description("Texture slot default texture type, when recognized.")]
        public string? TextureDefaultType { get; set; }

        [Description("Serialized GUID of the Texture asset assigned directly to this texture input slot, when present.")]
        public string? TextureAssetGuid { get; set; }

        [Description("Project asset path of the Texture asset assigned directly to this texture input slot, when present.")]
        public string? TextureAssetPath { get; set; }
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
