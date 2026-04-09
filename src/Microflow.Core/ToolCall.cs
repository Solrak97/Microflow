using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace Microflow.Core
{
    public sealed class ToolCallRequest
    {
        public string ToolName { get; set; } = string.Empty;
        public string ArgsJson { get; set; } = "{}";
        public string CorrelationId { get; set; } = Guid.NewGuid().ToString("n");
    }

    public sealed class ToolCallResult
    {
        public bool Success { get; set; }
        public bool Retryable { get; set; }
        public string? Error { get; set; }
        public string OutputJson { get; set; } = "{}";
        public long DurationMs { get; set; }

        // Optional observability fields for hosts that track token usage.
        public long PromptTokens { get; set; }
        public long CompletionTokens { get; set; }
        public long TotalTokens => PromptTokens > 0 || CompletionTokens > 0 ? PromptTokens + CompletionTokens : 0;
    }

    public interface IToolInvoker
    {
        Task<ToolCallResult> InvokeAsync(ToolCallRequest request, CancellationToken ct);
    }

    public static class ToolCallNode
    {
        /// <summary>
        /// Creates a node handler that invokes a tool request from state and writes the result back to state.
        /// </summary>
        public static NodeHandler<TState> Create<TState>(
            IToolInvoker invoker,
            Func<TState, ToolCallRequest?> requestSelector,
            Action<TState, ToolCallResult> resultWriter)
        {
            if (invoker == null) throw new ArgumentNullException(nameof(invoker));
            if (requestSelector == null) throw new ArgumentNullException(nameof(requestSelector));
            if (resultWriter == null) throw new ArgumentNullException(nameof(resultWriter));

            return async (state, context, ct) =>
            {
                var req = requestSelector(state);
                if (req == null)
                    return NodeResult.Failed("ToolCallNode: request selector returned null.");
                if (string.IsNullOrWhiteSpace(req.ToolName))
                    return NodeResult.Failed("ToolCallNode: ToolName is required.");

                var sw = Stopwatch.StartNew();
                ToolCallResult toolResult;
                try
                {
                    toolResult = await invoker.InvokeAsync(req, ct).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    toolResult = new ToolCallResult
                    {
                        Success = false,
                        Retryable = true,
                        Error = ex.Message,
                        OutputJson = "{}"
                    };
                }
                sw.Stop();

                toolResult.DurationMs = toolResult.DurationMs > 0 ? toolResult.DurationMs : sw.ElapsedMilliseconds;
                resultWriter(state, toolResult);

                if (toolResult.Success)
                    return NodeResult.Success($"tool={req.ToolName} ok duration_ms={toolResult.DurationMs}");
                if (toolResult.Retryable)
                    return NodeResult.Retry($"tool={req.ToolName} retry error={toolResult.Error}");
                return NodeResult.Failed($"tool={req.ToolName} failed error={toolResult.Error}");
            };
        }
    }
}
