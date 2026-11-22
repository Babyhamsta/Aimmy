using AILogic;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using SharpGen.Runtime;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using Vortice;
using Vortice.Direct3D;
using Vortice.Direct3D12;
using Vortice.DXGI;

namespace Aimmy2.AILogic;

// All submissions and changes to descriptors are serialized by the inference gate.
internal sealed class DirectMlGpuContext : IDisposable
{
    private ID3D12Device _device = null!;
    private ID3D12CommandQueue _queue = null!;
    private nint _dmlDevice;
    private ID3D12CommandAllocator _allocator = null!;
    private ID3D12GraphicsCommandList _list = null!;
    private ID3D12Fence _completion = null!;
    private readonly AutoResetEvent _ready = new(false);
    private ulong _submitted;
    private ID3D12RootSignature _root = null!;
    private ID3D12PipelineState _pipeline = null!;
    private ID3D12DescriptorHeap _heap = null!;
    private ID3D12Resource? _texture, _buffer;
    private ID3D12Fence? _sharedFence;
    private nint _allocation;
    private OrtValue? _input;
    private int _size;
    private bool _half, _disposed;
    internal Luid AdapterLuid { get; private set; }

    private DirectMlGpuContext()
    { }

    internal static DirectMlGpuContext Create()
    {
        var (adapter, output) = CaptureManager.FindDisplayOutput();
        using (adapter)
        using (output)
            return new DirectMlGpuContext().Initialize(adapter);
    }

    private DirectMlGpuContext Initialize(IDXGIAdapter1 adapter)
    {
        try
        {
            AdapterLuid = adapter.Description1.Luid;
            _device = D3D12.D3D12CreateDevice<ID3D12Device>(adapter, FeatureLevel.Level_11_0);

            try
            {
                _queue = _device.CreateCommandQueue(new CommandQueueDescription(
                    CommandListType.Direct, CommandQueuePriority.High));
            }
            catch
            {
                _queue = _device.CreateCommandQueue(CommandListType.Direct);
            }

            Guid iid = new("6dbd6437-96fd-423f-a98c-ae5e7c2a573f");
            Marshal.ThrowExceptionForHR(DMLCreateDevice(_device.NativePointer, 0, in iid, out _dmlDevice));
            _completion = _device.CreateFence(0);
            _allocator = _device.CreateCommandAllocator(CommandListType.Direct);
            _list = _device.CreateCommandList<ID3D12GraphicsCommandList>(CommandListType.Direct, _allocator);
            _list.Close();
            var ranges = new RootDescriptorTable(
                new DescriptorRange(DescriptorRangeType.ShaderResourceView, 1, 0),
                new DescriptorRange(DescriptorRangeType.UnorderedAccessView, 1, 0));
            _root = _device.CreateRootSignature(new RootSignatureDescription(RootSignatureFlags.None,
                [new(ranges, ShaderVisibility.All), new(new RootConstants(0, 0, 7), ShaderVisibility.All)]), RootSignatureVersion.Version10);
            _pipeline = _device.CreateComputePipelineState(new ComputePipelineStateDescription
            {
                RootSignature = _root,
                ComputeShader = ShaderBytecode.Value
            });
            _heap = _device.CreateDescriptorHeap(new(DescriptorHeapType.ConstantBufferViewShaderResourceViewUnorderedAccessView,
                2, DescriptorHeapFlags.ShaderVisible));
            return this;
        }
        catch { Dispose(); throw; }
    }

    internal void AppendExecutionProvider(SessionOptions options) =>
        DmlInterop.AppendExecutionProviderDml1(options, _dmlDevice, _queue.NativePointer);

    // The context owns the OrtValue and its backing allocation. FixedModelBuffers borrows it
    // for one size, and is released before another size can replace the allocation.
    internal OrtValue GetInputTensor(int imageSize, bool isFp16)
    {
        if (_input != null && imageSize == _size && isFp16 == _half) return _input;
        if (imageSize <= 0 || imageSize % 16 != 0)
            throw new NotSupportedException($"Unsupported GPU preprocessing size {imageSize}.");
        Drain();
        ReleaseTensor();
        uint bytes = checked((uint)(3 * imageSize * imageSize * (isFp16 ? 2 : 4)));
        try
        {
            _buffer = _device.CreateCommittedResource(HeapType.Default,
                ResourceDescription.Buffer(bytes, ResourceFlags.AllowUnorderedAccess), ResourceStates.UnorderedAccess);
            var uav = new UnorderedAccessViewDescription
            {
                Format = Format.R32_Typeless,
                ViewDimension = UnorderedAccessViewDimension.Buffer,
                Buffer = new BufferUnorderedAccessView { NumElements = bytes / 4, Flags = BufferUnorderedAccessViewFlags.Raw }
            };
            var start = _heap.GetCPUDescriptorHandleForHeapStart();
            uint stride = _device.GetDescriptorHandleIncrementSize(_heap.Description.Type);
            _device.CreateUnorderedAccessView(_buffer, null, uav, new CpuDescriptorHandle(start, 1, stride));
            _allocation = DmlInterop.CreateGpuAllocation(_buffer);
            _input = DmlInterop.CreateGpuInputTensor(_allocation, bytes, [1, 3, imageSize, imageSize], isFp16);
            _size = imageSize;
            _half = isFp16;
            return _input;
        }
        catch { ReleaseTensor(); throw; }
    }

    internal void BindSharedResources(nint textureHandle, nint fenceHandle)
    {
        ReleaseSharedResources();
        try
        {
            _texture = _device.OpenSharedHandle<ID3D12Resource>(textureHandle);
            _sharedFence = _device.OpenSharedHandle<ID3D12Fence>(fenceHandle);
            _device.CreateShaderResourceView(_texture, null, _heap.GetCPUDescriptorHandleForHeapStart());
        }
        catch { ReleaseSharedResources(); throw; }
    }

    internal unsafe void EnqueuePreprocess(ulong waitFenceValue, int imageSize, Rectangle visible,
        Point destination, bool maskThirdPerson, bool isFp16)
    {
        if (_texture == null || _sharedFence == null || _buffer == null || _input == null || imageSize != _size || isFp16 != _half)
            throw new InvalidOperationException("Capture and model input bindings do not match.");

        WaitForCompletion();
        _allocator.Reset();
        _list.Reset(_allocator, _pipeline);
        _list.ResourceBarrierTransition(_texture, ResourceStates.Common, ResourceStates.NonPixelShaderResource);
        _list.SetComputeRootSignature(_root);
        _list.SetDescriptorHeaps(_heap);
        _list.SetComputeRootDescriptorTable(0, _heap.GetGPUDescriptorHandleForHeapStart());
        int* constants = stackalloc int[] { imageSize, destination.X, destination.Y,
            destination.X + visible.Width, destination.Y + visible.Height, maskThirdPerson ? 1 : 0, isFp16 ? 1 : 0 };
        _list.SetComputeRoot32BitConstants(1, 7, constants, 0);
        _list.Dispatch((uint)imageSize / 16, (uint)imageSize / 8, 1);
        _list.ResourceBarrierUnorderedAccessView(_buffer);
        _list.ResourceBarrierTransition(_texture, ResourceStates.NonPixelShaderResource, ResourceStates.Common);
        _list.Close();
        _queue.Wait(_sharedFence, waitFenceValue).CheckError();
        _queue.ExecuteCommandList(_list);
        SignalCompletion();
        // Even values hand the texture back to D3D11. It must not start the next copy while we read.
        _queue.Signal(_sharedFence, waitFenceValue + 1).CheckError();
    }

    private void SignalCompletion()
    {
        ulong next = checked(_submitted + 1);
        _queue.Signal(_completion, next).CheckError();
        _submitted = next;
    }

    private void WaitForCompletion()
    {
        if (_completion.CompletedValue < _submitted)
        {
            _completion.SetEventOnCompletion(_submitted, _ready).CheckError();
            _ready.WaitOne();
        }
    }

    internal void Drain()
    {
        if (_completion == null || _disposed) return;
        // Device removal terminates outstanding GPU work and sets all fences to UINT64_MAX.
        if (_completion.CompletedValue == ulong.MaxValue) return;
        try
        {
            SignalCompletion();
            WaitForCompletion();
        }
        catch (SharpGenException) when (_completion.CompletedValue == ulong.MaxValue) { }
    }

    internal void ReleaseSharedResources()
    {
        if (_texture == null && _sharedFence == null) return;
        try { Drain(); }
        finally
        {
            _texture?.Dispose();
            _texture = null;
            _sharedFence?.Dispose();
            _sharedFence = null;
        }
    }

    private void ReleaseTensor()
    {
        _input?.Dispose();
        _input = null;
        try { if (_allocation != 0) DmlInterop.FreeGpuAllocation(_allocation); }
        finally
        {
            _allocation = 0;
            _buffer?.Dispose();
            _buffer = null;
        }
    }

    public void Dispose()
    {
        if (_disposed) return;
        try { Drain(); }
        finally
        {
            _disposed = true;
            ReleaseSharedResources();
            try { ReleaseTensor(); }
            finally
            {
                _list?.Dispose();
                _allocator?.Dispose();
                _pipeline?.Dispose();
                _root?.Dispose();
                _heap?.Dispose();
                _completion?.Dispose();
                if (_dmlDevice != 0) Marshal.Release(_dmlDevice);
                _queue?.Dispose();
                _device?.Dispose();
                _ready.Dispose();
            }
        }
    }

    [DllImport("DirectML.dll", ExactSpelling = true)]
    private static extern int DMLCreateDevice(nint device, uint flags, in Guid iid, out nint dmlDevice);

    [DllImport("d3dcompiler_47.dll", ExactSpelling = true, CharSet = CharSet.Ansi)]
    private static extern int D3DCompile(byte[] source, nuint length, string? name, nint defines, nint include,
        string entry, string target, uint flags, uint effectFlags, out nint code, out nint errors);

    private static readonly Lazy<byte[]> ShaderBytecode = new(() =>
    {
        byte[] source = Encoding.ASCII.GetBytes(Shader);
        int hr = D3DCompile(source, (nuint)source.Length, null, 0, 0, "main", "cs_5_0", 0, 0, out var code, out var errors);
        using var diagnostics = errors != 0 ? new Blob(errors) : null;
        using var bytecode = code != 0 ? new Blob(code) : null;
        if (hr < 0) throw new InvalidOperationException(diagnostics?.AsString() ?? $"D3DCompile failed (0x{hr:X8}).");
        return bytecode!.AsSpan().ToArray();
    });

    private const string Shader = """
        Texture2D<float4> srcTex : register(t0);
        RWByteAddressBuffer dstTensor : register(u0);
        cbuffer Crop : register(b0) {
            uint imageSize, destX, destY, visRight, visBottom, maskThirdPerson, isFp16;
        };

        float3 pixel(uint x, uint y) {
            if (x < destX || x >= visRight || y < destY || y >= visBottom ||
                (maskThirdPerson && x < imageSize / 2 && y >= imageSize - imageSize / 2))
                return 0;
            return srcTex.Load(int3(x, y, 0)).rgb;
        }

        [numthreads(8, 8, 1)]
        void main(uint3 id : SV_DispatchThreadID) {
            uint x = id.x * 2, y = id.y;
            float3 a = pixel(x, y), b = pixel(x + 1, y);
            [unroll] for (uint c = 0; c < 3; ++c) {
                uint i = (c * imageSize + y) * imageSize + x;
                if (isFp16)
                    dstTensor.Store(2 * i, f32tof16(a[c]) | (f32tof16(b[c]) << 16));
                else
                    dstTensor.Store2(4 * i, uint2(asuint(a[c]), asuint(b[c])));
            }
        }
        """;
}

// Pointer indices verified using offsetof against both native headers shipped with ORT 1.24.4.
// The managed 1.24.4 wrappers handle OrtApi slots 68 (memory info), 49 (external tensor)
// and 9 (Run), including their native lifetimes and UTF-8 names.
internal static unsafe class DmlInterop
{
    private const uint ApiVersion = 24;
    private static readonly nint* Ort;
    private static readonly nint* Dml;

    [DllImport("onnxruntime", ExactSpelling = true)]
    private static extern nint* OrtGetApiBase();

    static DmlInterop()
    {
        var getApi = (delegate* unmanaged[Stdcall]<uint, nint*>)OrtGetApiBase()[0];
        Ort = getApi(ApiVersion);
        if (Ort == null) throw new NotSupportedException($"ONNX Runtime API {ApiVersion} is unavailable.");
        nint* provider;
        fixed (byte* name = "DML\0"u8)
            Check(((delegate* unmanaged[Stdcall]<byte*, uint, nint**, nint>)Ort[195])(name, ApiVersion, &provider));
        Dml = provider;
    }

    internal static void AppendExecutionProviderDml1(SessionOptions options, nint device, nint queue)
    {
        Check(((delegate* unmanaged[Stdcall]<nint, nint, nint, nint>)Dml[1])(options.DangerousGetHandle(), device, queue));
        GC.KeepAlive(options);
    }

    internal static nint CreateGpuAllocation(ID3D12Resource resource)
    {
        nint allocation;
        Check(((delegate* unmanaged[Stdcall]<nint, nint*, nint>)Dml[2])(resource.NativePointer, &allocation));
        return allocation;
    }

    internal static void FreeGpuAllocation(nint allocation) =>
        Check(((delegate* unmanaged[Stdcall]<nint, nint>)Dml[3])(allocation));

    internal static OrtValue CreateGpuInputTensor(nint allocation, nuint bytes, long[] shape, bool isFp16)
    {
        using var info = new OrtMemoryInfo("DML", OrtAllocatorType.DeviceAllocator, 0, OrtMemType.Default);
        return OrtValue.CreateTensorValueWithData(info, isFp16 ? TensorElementType.Float16 : TensorElementType.Float,
            shape, allocation, checked((long)bytes));
    }

    private static void Check(nint status)
    {
        if (status == 0) return;
        try
        {
            nint message = ((delegate* unmanaged[Stdcall]<nint, nint>)Ort[2])(status);
            throw new InvalidOperationException(Marshal.PtrToStringUTF8(message));
        }
        finally { ((delegate* unmanaged[Stdcall]<nint, void>)Ort[93])(status); }
    }
}