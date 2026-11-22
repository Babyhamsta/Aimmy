using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Aimmy2.AILogic
{
    // One owner per inference gate and input size. Only fixed-shape model outputs can be preallocated.
    internal sealed class FixedModelBuffers : IDisposable
    {
        private readonly string[] _inputNames, _outputNames;
        private readonly OrtValue[] _inputs, _outputs;
        private readonly TensorBase _output;

        internal static FixedModelBuffers? TryCreate<T>(InferenceSession session, int size, T[] input) where T : unmanaged
        {
            var modelInput = session.InputMetadata.First();
            if (modelInput.Value.ElementType != typeof(T))
                throw new ArgumentException("Input buffer precision must match the model.", nameof(input));
            var output = session.OutputMetadata.First();
            int[] shape = output.Value.Dimensions;
            if (shape.Length != 3 || shape.Any(dimension => dimension <= 0))
                return null;

            long[] outputShape = Array.ConvertAll(shape, d => (long)d);
            var pinnedInput = OrtValue.CreateTensorValueFromMemory(input, [1, 3, size, size]);
            try
            {
                return output.Value.ElementType == typeof(Float16) ? BindOutput<Float16>() :
                    output.Value.ElementType == typeof(float) ? BindOutput<float>() :
                    throw new NotSupportedException($"Unsupported output precision {output.Value.ElementType}.");
            }
            catch
            {
                pinnedInput.Dispose();
                throw;
            }

            FixedModelBuffers BindOutput<TOutput>() where TOutput : unmanaged
            {
                var tensor = new DenseTensor<TOutput>(shape);
                var pinnedOutput = OrtValue.CreateTensorValueFromMemory(OrtMemoryInfo.DefaultInstance, tensor.Buffer, outputShape);
                try
                {
                    return new FixedModelBuffers(modelInput.Key, pinnedInput, output.Key, pinnedOutput, tensor);
                }
                catch
                {
                    pinnedOutput.Dispose();
                    throw;
                }
            }
        }

        private FixedModelBuffers(string inputName, OrtValue input, string outputName, OrtValue output, TensorBase tensor)
        {
            _inputNames = [inputName];
            _inputs = [input];
            _outputNames = [outputName];
            _outputs = [output];
            _output = tensor;
        }

        internal TensorBase Run(InferenceSession session, RunOptions options)
        {
            session.Run(options, _inputNames, _inputs, _outputNames, _outputs);
            return _output;
        }

        public void Dispose()
        {
            _outputs[0].Dispose();
            _inputs[0].Dispose();
        }
    }
}
