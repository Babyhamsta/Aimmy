using Microsoft.ML.OnnxRuntime;

namespace Aimmy2.AILogic
{
    internal static class OnnxModelSessionFactory
    {
        private static SessionOptions CreateDefaultOptions()
        {
            return new SessionOptions
            {
                EnableCpuMemArena = true,
                EnableMemoryPattern = false,
                GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL,
                ExecutionMode = ExecutionMode.ORT_SEQUENTIAL,
                InterOpNumThreads = 1,
                IntraOpNumThreads = 4
            };
        }

        internal static OnnxModelLoadResult Load(string modelPath, bool useDirectML)
        {
            using SessionOptions sessionOptions = CreateDefaultOptions();

            if (useDirectML) { sessionOptions.AppendExecutionProvider_DML(); }
            else { sessionOptions.AppendExecutionProvider_CPU(); }

            InferenceSession? session = null;
            try
            {
                session = new InferenceSession(modelPath, sessionOptions);
                var result = new OnnxModelLoadResult(session, new List<string>(session.OutputMetadata.Keys));
                session = null;
                return result;
            }
            finally
            {
                session?.Dispose();
            }
        }
    }

    internal sealed record OnnxModelLoadResult(InferenceSession Session, List<string> OutputNames);
}