using System;
using System.Threading;
using System.Threading.Tasks;

namespace Microflow.Core
{
    public sealed class FlowStepResult<TState>
    {
        public TState State { get; set; } = default!;
        public int Step { get; set; }
        public string NodeId { get; set; } = string.Empty;
        public string? NextNodeId { get; set; }
        public NodeResult Result { get; set; }
        public bool Halted { get; set; }
        public string? Error { get; set; }
        public FlowTraceEvent TraceEvent { get; set; } = new FlowTraceEvent();
    }

    /// <summary>
    /// Incremental runner for UI-driven hosts (e.g., React stores) that need one step at a time.
    /// </summary>
    public sealed class FlowStepper<TState>
    {
        readonly FlowGraph<TState> _graph;
        int _step;

        public FlowStepper(FlowGraph<TState> graph, string? startNodeOverride = null)
        {
            _graph = graph ?? throw new ArgumentNullException(nameof(graph));
            CurrentNodeId = string.IsNullOrWhiteSpace(startNodeOverride) ? graph.StartNodeId : startNodeOverride!;
        }

        public string CurrentNodeId { get; private set; }
        public bool Halted { get; private set; }
        public int StepsExecuted => _step;

        public async Task<FlowStepResult<TState>> StepAsync(TState state, CancellationToken ct = default)
        {
            if (Halted)
            {
                return new FlowStepResult<TState>
                {
                    State = state,
                    Step = _step,
                    NodeId = CurrentNodeId,
                    NextNodeId = null,
                    Result = NodeResult.Halt("Flow already halted."),
                    Halted = true,
                    TraceEvent = new FlowTraceEvent
                    {
                        TimestampUtc = DateTimeOffset.UtcNow,
                        Step = _step,
                        NodeId = CurrentNodeId,
                        Outcome = NodeOutcome.Halt,
                        Message = "Flow already halted.",
                        NextNodeId = null
                    }
                };
            }

            var nodeId = CurrentNodeId;
            var handler = _graph.RequireNode(nodeId);
            NodeResult result;

            try
            {
                result = await handler(state, new NodeContext(nodeId, _step), ct).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                Halted = true;
                return new FlowStepResult<TState>
                {
                    State = state,
                    Step = _step,
                    NodeId = nodeId,
                    NextNodeId = null,
                    Result = NodeResult.Failed(ex.Message),
                    Halted = true,
                    Error = ex.ToString(),
                    TraceEvent = new FlowTraceEvent
                    {
                        TimestampUtc = DateTimeOffset.UtcNow,
                        Step = _step,
                        NodeId = nodeId,
                        Outcome = NodeOutcome.Failed,
                        Message = ex.Message,
                        NextNodeId = null
                    }
                };
            }

            var next = _graph.ResolveNextNode(nodeId, state, result);
            var halted = result.Outcome == NodeOutcome.Halt || next == null;
            if (halted)
            {
                Halted = true;
            }
            else
            {
                CurrentNodeId = next!;
            }

            var trace = new FlowTraceEvent
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Step = _step,
                NodeId = nodeId,
                Outcome = result.Outcome,
                Message = result.Message,
                NextNodeId = next
            };

            var response = new FlowStepResult<TState>
            {
                State = state,
                Step = _step,
                NodeId = nodeId,
                NextNodeId = next,
                Result = result,
                Halted = halted,
                TraceEvent = trace
            };

            _step++;
            return response;
        }
    }
}
