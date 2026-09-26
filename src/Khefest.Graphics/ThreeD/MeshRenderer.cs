using System.Numerics;
using System.Runtime.InteropServices;
using Khefest.Core.Errors;
using Khefest.Core.Resources;
using Khefest.Graphics.Camera;
using Khefest.Graphics.Diagnostics;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.Texturing;
using Khefest.Graphics.ThreeD.Lighting;

namespace Khefest.Graphics.ThreeD;

/// <summary>
/// High-level 3D mesh and model renderer supporting depth buffering, Blinn-Phong lighting, and materials.
/// </summary>
public sealed class MeshRenderer : ResourceBase
{
    [StructLayout(LayoutKind.Sequential)]
    public struct SceneBufferData
    {
        public Matrix4x4 View;
        public Matrix4x4 Projection;
        public Vector3 CameraPosition;
        public float Pad0;
        public Vector3 DirLightDirection;
        public float DirLightIntensity;
        public Vector3 DirLightColor;
        public float Pad1;
        public Vector3 AmbientLightColor;
        public float AmbientLightIntensity;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct ObjectBufferData
    {
        public Matrix4x4 World;
        public Vector4 TintColor;
        public float SpecularPower;
        public float SpecularIntensity;
        public Vector2 PadObj;
    }

    private const string Standard3DShaderSource = @"
cbuffer SceneBuffer : register(b0)
{
    row_major float4x4 View;
    row_major float4x4 Projection;
    float3 CameraPosition;
    float Pad0;
    float3 DirLightDirection;
    float DirLightIntensity;
    float3 DirLightColor;
    float Pad1;
    float3 AmbientLightColor;
    float AmbientLightIntensity;
};

cbuffer ObjectBuffer : register(b1)
{
    row_major float4x4 World;
    float4 TintColor;
    float SpecularPower;
    float SpecularIntensity;
    float2 PadObj;
};

struct VSInput
{
    float3 Position : POSITION;
    float3 Normal   : NORMAL;
    float2 TexCoord : TEXCOORD;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float3 WorldPos : POSITION0;
    float3 Normal   : NORMAL;
    float2 TexCoord : TEXCOORD;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    float4 worldPos = mul(float4(input.Position, 1.0f), World);
    output.WorldPos = worldPos.xyz;
    float4 viewPos = mul(worldPos, View);
    output.Position = mul(viewPos, Projection);
    output.Normal = normalize(mul(float4(input.Normal, 0.0f), World).xyz);
    output.TexCoord = input.TexCoord;
    return output;
}

Texture2D DiffuseTexture : register(t0);
SamplerState Sampler : register(s0);

float4 PSMain(PSInput input) : SV_TARGET
{
    float3 N = normalize(input.Normal);
    float3 L = normalize(-DirLightDirection);
    float3 V = normalize(CameraPosition - input.WorldPos);

    // Ambient component
    float3 ambient = AmbientLightColor * AmbientLightIntensity;

    // Diffuse component
    float NdotL = max(dot(N, L), 0.0f);
    float3 diffuse = DirLightColor * (DirLightIntensity * NdotL);

    // Blinn-Phong Specular component
    float3 H = normalize(L + V);
    float NdotH = max(dot(N, H), 0.0f);
    float spec = pow(NdotH, max(1.0f, SpecularPower)) * (NdotL > 0.0f ? SpecularIntensity : 0.0f);
    float3 specular = DirLightColor * spec;

    float4 texColor = DiffuseTexture.Sample(Sampler, input.TexCoord);
    float4 baseColor = texColor * TintColor;

    float3 finalRgb = (ambient + diffuse) * baseColor.rgb + specular;
    return float4(finalRgb, baseColor.a);
}
";

    private readonly IGpuDevice _device;
    private readonly IGpuShader _vs;
    private readonly IGpuShader _ps;
    private readonly IPipeline _pipeline;
    private readonly IGpuBuffer _sceneConstantBuffer;
    private readonly IGpuBuffer _objectConstantBuffer;
    private readonly Texture2D _whitePixelTexture;
    private readonly Material _defaultMaterial;

    private ICommandRecorder? _recorder;
    private IGpuTexture? _currentColorTarget;
    private IGpuTexture? _currentDepthTarget;
    private bool _hasBegun;

    public Texture2D WhiteTexture => _whitePixelTexture;
    public Material DefaultMaterial => _defaultMaterial;

    public MeshRenderer(IGpuDevice device, ResourceManager? manager = null)
        : base("MeshRenderer", ResourceType.Custom, 0, manager?.Tracker.EnableLeakTracking ?? false)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));

        _vs = _device.CompileShader("Mesh3D_VS", ShaderStage.Vertex, Standard3DShaderSource, "VSMain");
        _ps = _device.CompileShader("Mesh3D_PS", ShaderStage.Pixel, Standard3DShaderSource, "PSMain");

        var pipelineDesc = new PipelineDesc
        {
            VertexShader = _vs,
            PixelShader = _ps,
            VertexLayout = VertexPositionNormalTexture.Layout,
            Topology = PrimitiveTopology.TriangleList,
            FillMode = FillMode.Solid,
            CullMode = CullMode.Back,
            BlendMode = BlendMode.Opaque,
            DepthTestEnabled = true,
            DepthWriteEnabled = true,
            DepthFunction = CompareFunction.Less
        };

        _pipeline = _device.CreatePipeline("Mesh3D_Pipeline", pipelineDesc);

        _sceneConstantBuffer = _device.CreateBuffer("Mesh3D_SceneCB", Marshal.SizeOf<SceneBufferData>(), BufferUsage.Uniform);
        _objectConstantBuffer = _device.CreateBuffer("Mesh3D_ObjectCB", Marshal.SizeOf<ObjectBufferData>(), BufferUsage.Uniform);

        _whitePixelTexture = Texture2D.CreateSolid(_device, "Mesh3D_WhitePixel", 1, 1, Color4.White, manager);
        _defaultMaterial = new Material("DefaultMaterial", _whitePixelTexture.GpuTexture, Vector4.One, 32f, 0.4f, manager);

        manager?.Register(this);
    }

    /// <summary>
    /// Begins a 3D rendering session bound to the specified color and optional depth targets.
    /// </summary>
    public void Begin(
        IGpuTexture colorTarget,
        IGpuTexture? depthTarget,
        PerspectiveCamera camera,
        DirectionalLight? dirLight = null,
        AmbientLight? ambientLight = null,
        bool clearColor = true,
        bool clearDepth = true,
        Color4? clearColorValue = null)
    {
        ThrowIfDisposed();
        if (_hasBegun)
        {
            throw new InvalidOperationException("Begin cannot be called while a rendering session is already active.");
        }

        ArgumentNullException.ThrowIfNull(colorTarget);
        ArgumentNullException.ThrowIfNull(camera);

        _currentColorTarget = colorTarget;
        _currentDepthTarget = depthTarget;
        _hasBegun = true;

        var dLight = dirLight ?? DirectionalLight.Default;
        var aLight = ambientLight ?? AmbientLight.Default;

        var sceneData = new SceneBufferData
        {
            View = camera.ViewMatrix,
            Projection = camera.ProjectionMatrix,
            CameraPosition = camera.Position,
            DirLightDirection = dLight.Direction,
            DirLightIntensity = dLight.Intensity,
            DirLightColor = dLight.Color,
            AmbientLightColor = aLight.Color,
            AmbientLightIntensity = aLight.Intensity
        };

        _sceneConstantBuffer.SetData(MemoryMarshal.CreateReadOnlySpan(ref sceneData, 1));

        _recorder = _device.CreateCommandRecorder();
        var pass = new RenderPassDesc
        {
            ColorTarget = _currentColorTarget,
            DepthTarget = _currentDepthTarget,
            ClearColorTarget = clearColor,
            ClearColor = clearColorValue ?? new Color4(0.1f, 0.12f, 0.16f, 1f),
            ClearDepthTarget = clearDepth && _currentDepthTarget != null,
            ClearDepth = 1.0f
        };

        _recorder.BeginPass(pass);
        _recorder.SetPipeline(_pipeline);
        _recorder.SetUniformBuffer(0, _sceneConstantBuffer);
        _recorder.SetUniformBuffer(1, _objectConstantBuffer);

        RenderStatistics.Current.RecordRenderPass();
        RenderStatistics.Current.RecordPipelineSwitch();
    }

    /// <summary>
    /// Draws a 3D mesh transformed by the specified world matrix with the given material.
    /// </summary>
    public void Draw(Mesh mesh, Material? material, in Matrix4x4 world)
    {
        ThrowIfDisposed();
        if (!_hasBegun || _recorder == null)
        {
            throw new InvalidOperationException("Draw must be called between Begin and End.");
        }

        ArgumentNullException.ThrowIfNull(mesh);
        var mat = material ?? _defaultMaterial;

        var objData = new ObjectBufferData
        {
            World = world,
            TintColor = mat.Color,
            SpecularPower = mat.SpecularPower,
            SpecularIntensity = mat.SpecularIntensity
        };

        _objectConstantBuffer.SetData(MemoryMarshal.CreateReadOnlySpan(ref objData, 1));

        var tex = mat.DiffuseTexture ?? _whitePixelTexture.GpuTexture;
        _recorder.SetTexture(0, tex);

        _recorder.SetVertexBuffer(0, mesh.VertexBuffer);
        _recorder.SetIndexBuffer(mesh.IndexBuffer, mesh.IndexFormat);
        _recorder.DrawIndexed(mesh.IndexCount, 0, 0);

        RenderStatistics.Current.RecordTextureBind();
        RenderStatistics.Current.RecordDrawIndexed(mesh.IndexCount, mesh.VertexCount);
    }

    /// <summary>
    /// Draws all constituent parts of a 3D model transformed by the specified world matrix.
    /// </summary>
    public void Draw(Model model, in Matrix4x4 world)
    {
        ThrowIfDisposed();
        if (!_hasBegun || _recorder == null)
        {
            throw new InvalidOperationException("Draw must be called between Begin and End.");
        }

        ArgumentNullException.ThrowIfNull(model);

        foreach (var part in model.Parts)
        {
            var partWorld = part.Transform * world;
            Draw(part.Mesh, part.Material, in partWorld);
        }
    }

    /// <summary>
    /// Ends the current 3D rendering session, completing the render pass and submitting commands.
    /// </summary>
    public void End()
    {
        ThrowIfDisposed();
        if (!_hasBegun || _recorder == null)
        {
            throw new InvalidOperationException("End cannot be called without an active Begin.");
        }

        _recorder.EndPass();
        _device.Submit(_recorder);

        _recorder.Dispose();
        _recorder = null;
        _currentColorTarget = null;
        _currentDepthTarget = null;
        _hasBegun = false;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _pipeline?.Dispose();
            _vs?.Dispose();
            _ps?.Dispose();
            _sceneConstantBuffer?.Dispose();
            _objectConstantBuffer?.Dispose();
            _whitePixelTexture?.Dispose();
            _defaultMaterial?.Dispose();
            _recorder?.Dispose();
        }
    }
}
