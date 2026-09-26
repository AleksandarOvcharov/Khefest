using System.Numerics;
using System.Runtime.InteropServices;
using Khefest.Core.Errors;
using Khefest.Core.Logging;
using Khefest.Core.Resources;
using Khefest.Graphics.Camera;
using Khefest.Graphics.Diagnostics;
using Khefest.Graphics.LowLevel;
using Khefest.Graphics.Text;
using Khefest.Graphics.Texturing;

namespace Khefest.Graphics.TwoD;

/// <summary>
/// High-performance 2D dynamic batch renderer for sprites, shapes, and fonts.
/// Minimizes draw calls and GPU state transitions through automated vertex buffering.
/// </summary>
public sealed class SpriteBatch : IDisposable
{
    private static readonly ILogger Logger = LogManager.GetLogger(KhefestSubsystem.Graphics, "SpriteBatch");

    public const int MaxBatchQuads = 2048;
    public const int MaxBatchVertices = MaxBatchQuads * 4;
    public const int MaxBatchIndices = MaxBatchQuads * 6;

    private readonly IGpuDevice _device;
    private readonly IGpuShader _vs;
    private readonly IGpuShader _ps;
    private readonly IGpuBuffer _vertexBuffer;
    private readonly IGpuBuffer _indexBuffer;
    private readonly IGpuBuffer _constantBuffer;
    private readonly IPipeline _pipeline;
    private readonly Texture2D _whitePixelTexture;
    private readonly TextureRegion _whitePixelRegion;
    private readonly BitmapFont _defaultFont;

    private readonly Vertex2D[] _vertices = new Vertex2D[MaxBatchVertices];
    private readonly List<QueuedSprite> _spriteQueue = new(MaxBatchQuads);

    private IGpuTexture? _currentTarget;
    private ICommandRecorder? _recorder;
    private Matrix4x4 _transformMatrix;
    private SpriteSortMode _sortMode;
    private Texture2D? _currentBatchTexture;
    private int _quadCount;
    private bool _hasBegun;
    private int _disposed;

    public Texture2D WhiteTexture => _whitePixelTexture;
    public TextureRegion WhiteTextureRegion => _whitePixelRegion;
    public BitmapFont DefaultFont => _defaultFont;

    private const string SpriteShaderSource = @"
cbuffer ViewProjectionBuffer : register(b0)
{
    row_major float4x4 ViewProjection;
};

struct VSInput
{
    float2 Position : POSITION;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
};

struct PSInput
{
    float4 Position : SV_POSITION;
    float2 TexCoord : TEXCOORD0;
    float4 Color : COLOR0;
};

PSInput VSMain(VSInput input)
{
    PSInput output;
    output.Position = mul(float4(input.Position, 0.0f, 1.0f), ViewProjection);
    output.TexCoord = input.TexCoord;
    output.Color = input.Color;
    return output;
}

Texture2D SpriteTexture : register(t0);
SamplerState LinearSampler : register(s0);

float4 PSMain(PSInput input) : SV_TARGET
{
    return SpriteTexture.Sample(LinearSampler, input.TexCoord) * input.Color;
}
";

    public SpriteBatch(IGpuDevice device, ResourceManager? resourceManager = null)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));

        // 1. Compile Shaders
        _vs = _device.CompileShader("SpriteBatchVS", ShaderStage.Vertex, SpriteShaderSource, "VSMain");
        _ps = _device.CompileShader("SpriteBatchPS", ShaderStage.Pixel, SpriteShaderSource, "PSMain");

        // 2. Create Pipeline
        var pipeDesc = new PipelineDesc
        {
            VertexShader = _vs,
            PixelShader = _ps,
            VertexLayout = Vertex2D.Layout,
            Topology = PrimitiveTopology.TriangleList,
            CullMode = CullMode.None,
            FillMode = FillMode.Solid,
            BlendMode = BlendMode.AlphaBlend,
            DepthTestEnabled = false,
            DepthWriteEnabled = false
        };
        _pipeline = _device.CreatePipeline("SpriteBatchPipeline", pipeDesc);

        // 3. Create Vertex Buffer
        _vertexBuffer = _device.CreateBuffer("SpriteBatchVBO", MaxBatchVertices * Marshal.SizeOf<Vertex2D>(), BufferUsage.Vertex);

        // 4. Create and populate static Index Buffer (0,1,2, 0,2,3 pattern)
        var indices = new ushort[MaxBatchIndices];
        for (int i = 0, v = 0; i < MaxBatchIndices; i += 6, v += 4)
        {
            indices[i + 0] = (ushort)(v + 0);
            indices[i + 1] = (ushort)(v + 1);
            indices[i + 2] = (ushort)(v + 2);
            indices[i + 3] = (ushort)(v + 0);
            indices[i + 4] = (ushort)(v + 2);
            indices[i + 5] = (ushort)(v + 3);
        }
        _indexBuffer = _device.CreateBuffer("SpriteBatchIBO", indices.Length * sizeof(ushort), BufferUsage.Index);
        _indexBuffer.SetData<ushort>(indices);

        // 5. Create Constant Buffer for ViewProjection matrix
        _constantBuffer = _device.CreateBuffer("SpriteBatchCBO", Marshal.SizeOf<Matrix4x4>(), BufferUsage.Uniform);

        // 6. 1x1 White Texture for shapes and tinting
        _whitePixelTexture = Texture2D.CreateSolid(_device, "SpriteBatchWhitePixel", 1, 1, Color4.White, resourceManager);
        _whitePixelRegion = new TextureRegion(_whitePixelTexture);

        // 7. Embedded Default Font
        _defaultFont = Text.DefaultFont.Create(_device, resourceManager);
    }

    public void Begin(
        IGpuTexture renderTarget,
        Camera2D? camera = null,
        SpriteSortMode sortMode = SpriteSortMode.Deferred)
    {
        var matrix = camera?.GetViewProjectionMatrix() ??
            Matrix4x4.CreateOrthographicOffCenter(0f, renderTarget.Width, renderTarget.Height, 0f, 0f, 1f);

        Begin(renderTarget, matrix, sortMode);
    }

    public void Begin(
        IGpuTexture renderTarget,
        Matrix4x4 transformMatrix,
        SpriteSortMode sortMode = SpriteSortMode.Deferred)
    {
        if (_hasBegun)
        {
            throw new InvalidOperationException("End() must be called before Begin() can be called again.");
        }

        _currentTarget = renderTarget ?? throw new ArgumentNullException(nameof(renderTarget));
        _transformMatrix = transformMatrix;
        _sortMode = sortMode;
        _hasBegun = true;
        _quadCount = 0;
        _currentBatchTexture = null;
        _spriteQueue.Clear();

        // Update constant buffer with transformation matrix
        _constantBuffer.SetData(MemoryMarshal.CreateReadOnlySpan(ref _transformMatrix, 1));

        // Create command recorder for this batch session
        _recorder = _device.CreateCommandRecorder();

        var pass = new RenderPassDesc
        {
            ColorTarget = _currentTarget,
            ClearColorTarget = false
        };
        _recorder.BeginPass(pass);
        _recorder.SetPipeline(_pipeline);
        _recorder.SetVertexBuffer(0, _vertexBuffer);
        _recorder.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        _recorder.SetUniformBuffer(0, _constantBuffer);

        RenderStatistics.Current.RecordRenderPass();
        RenderStatistics.Current.RecordPipelineSwitch();
    }

    public void Draw(Texture2D texture, Vector2 position, Color4? color = null)
    {
        ArgumentNullException.ThrowIfNull(texture);
        Draw(texture, position, new Vector2(texture.Width, texture.Height), color ?? Color4.White);
    }

    public void Draw(Texture2D texture, Vector2 position, Vector2 size, Color4 color)
    {
        ArgumentNullException.ThrowIfNull(texture);
        Draw(new TextureRegion(texture), position, size, color);
    }

    public void Draw(
        Texture2D texture,
        Vector2 position,
        Vector2 size,
        Color4 color,
        float rotation,
        Vector2 origin,
        SpriteEffects effects,
        float depth = 0.0f)
    {
        ArgumentNullException.ThrowIfNull(texture);
        Draw(new TextureRegion(texture), position, size, color, rotation, origin, effects, depth);
    }

    public void Draw(TextureRegion region, Vector2 position, Color4? color = null)
    {
        ArgumentNullException.ThrowIfNull(region);
        Draw(region, position, new Vector2(region.Width, region.Height), color ?? Color4.White);
    }

    public void Draw(TextureRegion region, Vector2 position, Vector2 size, Color4 color)
    {
        ArgumentNullException.ThrowIfNull(region);
        Draw(region, position, size, color, 0.0f, Vector2.Zero, SpriteEffects.None, 0.0f);
    }

    public void Draw(Sprite sprite)
    {
        ArgumentNullException.ThrowIfNull(sprite);
        Draw(
            sprite.Region,
            sprite.Position,
            new Vector2(sprite.Width, sprite.Height),
            sprite.Color,
            sprite.Rotation,
            sprite.Origin,
            sprite.Effects,
            sprite.Depth);
    }

    public void Draw(
        TextureRegion region,
        Vector2 position,
        Vector2 size,
        Color4 color,
        float rotation,
        Vector2 origin,
        SpriteEffects effects,
        float depth = 0.0f)
    {
        if (!_hasBegun)
        {
            throw new InvalidOperationException("Begin() must be called before Draw().");
        }

        var item = new QueuedSprite(region, position, size, color, rotation, origin, effects, depth);

        if (_sortMode == SpriteSortMode.Deferred)
        {
            RenderQuad(item);
        }
        else
        {
            _spriteQueue.Add(item);
        }
    }

    public void DrawString(
        string? text,
        Vector2 position,
        Color4 color,
        float scale = 1.0f)
    {
        _defaultFont.DrawString(this, text, position, color, scale);
    }

    public void DrawString(
        BitmapFont font,
        string? text,
        Vector2 position,
        Color4 color,
        float scale = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(font);
        font.DrawString(this, text, position, color, scale);
    }

    private void RenderQuad(in QueuedSprite item)
    {
        // Flush if switching texture or vertex buffer is full
        if (_currentBatchTexture != null && _currentBatchTexture != item.Region.Texture)
        {
            Flush();
        }

        if (_quadCount >= MaxBatchQuads)
        {
            Flush();
        }

        _currentBatchTexture = item.Region.Texture;

        float u0 = item.Region.U0;
        float v0 = item.Region.V0;
        float u1 = item.Region.U1;
        float v1 = item.Region.V1;

        if (item.Effects.HasFlag(SpriteEffects.FlipHorizontally))
        {
            (u0, u1) = (u1, u0);
        }
        if (item.Effects.HasFlag(SpriteEffects.FlipVertically))
        {
            (v0, v1) = (v1, v0);
        }

        float originX = item.Origin.X;
        float originY = item.Origin.Y;
        float w = item.Size.X;
        float h = item.Size.Y;

        // Quad corners relative to origin
        float x0 = -originX;
        float y0 = -originY;
        float x1 = w - originX;
        float y1 = h - originY;

        Vector2 p0, p1, p2, p3;

        if (Math.Abs(item.Rotation) > 0.0001f)
        {
            float cos = MathF.Cos(item.Rotation);
            float sin = MathF.Sin(item.Rotation);

            p0 = item.Position + new Vector2(x0 * cos - y0 * sin, x0 * sin + y0 * cos);
            p1 = item.Position + new Vector2(x1 * cos - y0 * sin, x1 * sin + y0 * cos);
            p2 = item.Position + new Vector2(x1 * cos - y1 * sin, x1 * sin + y1 * cos);
            p3 = item.Position + new Vector2(x0 * cos - y1 * sin, x0 * sin + y1 * cos);
        }
        else
        {
            p0 = item.Position + new Vector2(x0, y0);
            p1 = item.Position + new Vector2(x1, y0);
            p2 = item.Position + new Vector2(x1, y1);
            p3 = item.Position + new Vector2(x0, y1);
        }

        int vi = _quadCount * 4;
        _vertices[vi + 0] = new Vertex2D(p0, new Vector2(u0, v0), item.Color);
        _vertices[vi + 1] = new Vertex2D(p1, new Vector2(u1, v0), item.Color);
        _vertices[vi + 2] = new Vertex2D(p2, new Vector2(u1, v1), item.Color);
        _vertices[vi + 3] = new Vertex2D(p3, new Vector2(u0, v1), item.Color);

        _quadCount++;
    }

    public void Flush()
    {
        if (_quadCount == 0 || _currentBatchTexture == null || _recorder == null) return;

        // Upload active vertex span
        var activeVertices = _vertices.AsSpan(0, _quadCount * 4);
        _vertexBuffer.SetData<Vertex2D>(activeVertices);

        // Bind active texture and draw
        _recorder.SetTexture(0, _currentBatchTexture.GpuTexture);
        _recorder.DrawIndexed(_quadCount * 6, 0, 0);

        RenderStatistics.Current.RecordTextureBind();
        RenderStatistics.Current.RecordDrawIndexed(_quadCount * 6, _quadCount * 4);

        _quadCount = 0;
    }

    public void End()
    {
        if (!_hasBegun)
        {
            throw new InvalidOperationException("Begin() must be called before End().");
        }

        if (_sortMode != SpriteSortMode.Deferred && _spriteQueue.Count > 0)
        {
            SortQueue();
            foreach (var item in _spriteQueue)
            {
                RenderQuad(item);
            }
        }

        Flush();

        _recorder?.EndPass();
        if (_recorder != null)
        {
            _device.Submit(_recorder);
            _recorder.Dispose();
            _recorder = null;
        }

        _hasBegun = false;
        _currentTarget = null;
        _currentBatchTexture = null;
        _spriteQueue.Clear();
    }

    private void SortQueue()
    {
        switch (_sortMode)
        {
            case SpriteSortMode.Texture:
                _spriteQueue.Sort((a, b) => a.Region.Texture.Id.CompareTo(b.Region.Texture.Id));
                break;
            case SpriteSortMode.FrontToBack:
                _spriteQueue.Sort((a, b) => a.Depth.CompareTo(b.Depth));
                break;
            case SpriteSortMode.BackToFront:
                _spriteQueue.Sort((a, b) => b.Depth.CompareTo(a.Depth));
                break;
        }
    }

    private readonly struct QueuedSprite
    {
        public readonly TextureRegion Region;
        public readonly Vector2 Position;
        public readonly Vector2 Size;
        public readonly Color4 Color;
        public readonly float Rotation;
        public readonly Vector2 Origin;
        public readonly SpriteEffects Effects;
        public readonly float Depth;

        public QueuedSprite(
            TextureRegion region,
            Vector2 position,
            Vector2 size,
            Color4 color,
            float rotation,
            Vector2 origin,
            SpriteEffects effects,
            float depth)
        {
            Region = region;
            Position = position;
            Size = size;
            Color = color;
            Rotation = rotation;
            Origin = origin;
            Effects = effects;
            Depth = depth;
        }
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _defaultFont.Texture.Dispose();
            _whitePixelTexture.Dispose();
            _constantBuffer.Dispose();
            _indexBuffer.Dispose();
            _vertexBuffer.Dispose();
            _pipeline.Dispose();
            _vs.Dispose();
            _ps.Dispose();
            _recorder?.Dispose();
        }
    }
}
