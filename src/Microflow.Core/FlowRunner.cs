using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microflow.Core
{
    public sealed class FlowTraceEvent
    {
        public DateTimeOffset TimestampUtc { get; set; }
        public int Step { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public NodeOutcome Outcome { get; set; }
        public string? Message { get; set; }
        public string? NextNodeId { get; set; }
    }

    public sealed class FlowRunResult<TState>
    {
        public TState State { get; set; } = default!;
        public string FinalNodeId { get; set; } = string.Empty;
        public int Steps { get; set; }
        public bool Halted { get; set; }
        public IReadOnlyList<FlowTraceEvent> Trace { get; set; } = Array.Empty<FlowTraceEvent>();
        public string? Error { get; set; }
    }

    public sealed class FlowRunner<TState>
    {
        readonly FlowGraph<TState> _graph;

        public FlowRunner(FlowGraph<TState> graph)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
        }

        public async Task<FlowRunResult<TState>> RunAsync(
            TState state,
            int maxSteps,
            CancellationToken ct = default,
            string? startNodeOverride = null)
        {
            if (maxSteps <= 0)
                throw new ArgumentOutOfRangeException(nameof(maxSteps), "maxSteps must be > 0.");

            var trace = new List<FlowTraceEvent>(Math.Min(maxSteps, 128));
            var nodeId = string.IsNullOrWhiteSpace(startNodeOverride) ? _graph.StartNodeId : startNodeOverride!;
            var step = 0;

            while (step < maxSteps)
            {
                ct.ThrowIfCancellationRequested();
                var handler = _graph.RequireNode(nodeId);
                var context = new NodeContext(nodeId, step);

                NodeResult result;
                try
                {
                    result = await handler(state, context, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    return new FlowRunResult<TState>
                    {
                        State = state,
                        FinalNodeId = nodeId,
                        Steps = step + 1,
                        Halted = true,
                        Trace = trace,
                        Error = ex.ToString()
                    };
                }

                var next = _graph.ResolveNextNode(nodeId, state, result);
                trace.Add(new FlowTraceEvent
                {
                    TimestampUtc = DateTimeOffset.UtcNow,
                    Step = step,
                    NodeId = nodeId,
                    Outcome = result.Outcome,
                    Message = result.Message,
                    NextNodeId = next
                });

                if (result.Outcome == NodeOutcome.Halt || next == null)
                {
                    return new FlowRunResult<TState>
                    {
                        State = state,
                        FinalNodeId = nodeId,
                        Steps = step + 1,
                        Halted = true,
                        Trace = trace,
                        Error = null
                    };
                }

                nodeId = next;
                step++;
            }

            return new FlowRunResult<TState>
            {
                State = state,
                FinalNodeId = nodeId,
                Steps = maxSteps,
                Halted = false,
                Trace = trace,
                Error = $"Reached maxSteps ({maxSteps}) without halting."
            };
        }
    }
}
