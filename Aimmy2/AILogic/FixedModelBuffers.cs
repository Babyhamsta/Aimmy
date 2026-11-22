using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;

namespace Aimmy2.AILogic
{
    // One owner per inference gate and input size. Only fixed-shape model outputs can be preallocated.
    internal sealed class FixedModelBuffers : IDisposable
    {
        private readonly string[] _inputNames, _outputNames;
        private readonly OrtValue[] _inputs, _outputs;
        private readonly DenseTensor<float> _output;

        internal static FixedModelBuffers? TryCreate(InferenceSession session, int size, float[] input)
        {
            var output = session.OutputMetadata.First();
            int[] shape = output.Value.Dimensions;
            if (shape.Length != 3 || shape.Any(dimension => dimension <= 0) || output.Value.ElementType != typeof(float))
                return null;

            return new FixedModelBuffers(session.InputMetadata.Keys.First(), size, input, output.Key, shape);
        }

        private FixedModelBuffers(string inputName, int size, float[] input, string outputName, int[] dimensions)
        {
            _inputNames = [inputName];
            _outputNames = [outputName];
            _output = new DenseTensor<float>(dimensions);
            _inputs = [OrtValue.CreateTensorValueFromMemory(input, [1, 3, size, size])];
            try
            {
                _outputs = [OrtValue.CreateTensorValueFromMemory(OrtMemoryInfo.DefaultInstance,
                    _output.Buffer, Array.ConvertAll(dimensions, d => (long)d))];
            }
            catch
            {
                _inputs[0].Dispose();
                throw;
            }
        }

        internal Tensor<float> Run(InferenceSession session, RunOptions options)
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