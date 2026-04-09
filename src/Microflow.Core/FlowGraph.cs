using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microflow.Core
{
    public enum NodeOutcome
    {
        Success = 0,
        Continue = 1,
        Retry = 2,
        Failed = 3,
        Halt = 4
    }

    public readonly struct NodeContext
    {
        public NodeContext(string nodeId, int step)
        {
            NodeId = nodeId;
            Step = step;
        }

        public string NodeId { get; }
        public int Step { get; }
    }

    public readonly struct NodeResult
    {
        public NodeResult(NodeOutcome outcome, string? message = null)
        {
            Outcome = outcome;
            Message = message;
        }

        public NodeOutcome Outcome { get; }
        public string? Message { get; }

        public static NodeResult Success(string? message = null) => new NodeResult(NodeOutcome.Success, message);
        public static NodeResult Continue(string? message = null) => new NodeResult(NodeOutcome.Continue, message);
        public static NodeResult Retry(string? message = null) => new NodeResult(NodeOutcome.Retry, message);
        public static NodeResult Failed(string? message = null) => new NodeResult(NodeOutcome.Failed, message);
        public static NodeResult Halt(string? message = null) => new NodeResult(NodeOutcome.Halt, message);
    }

    public delegate Task<NodeResult> NodeHandler<TState>(TState state, NodeContext context, CancellationToken ct);
    public delegate bool TransitionGuard<TState>(TState state, NodeResult result);

    public sealed class FlowTransition<TState>
    {
        public FlowTransition(string from, string to, TransitionGuard<TState> when)
        {
            From = from;
            To = to;
            When = when ?? throw new ArgumentNullException(nameof(when));
        }

        public string From { get; }
        public string To { get; }
        public TransitionGuard<TState> When { get; }
    }

    public sealed class FlowGraph<TState>
    {
        readonly Dictionary<string, NodeHandler<TState>> _nodes = new Dictionary<string, NodeHandler<TState>>(StringComparer.Ordinal);
        readonly List<FlowTransition<TState>> _transitions = new List<FlowTransition<TState>>();

        public FlowGraph(string startNodeId)
        {
            if (string.IsNullOrWhiteSpace(startNodeId))
                throw new ArgumentException("Start node id is required.", nameof(startNodeId));
            StartNodeId = startNodeId;
        }

        public string StartNodeId { get; }
        public IReadOnlyDictionary<string, NodeHandler<TState>> Nodes => _nodes;
        public IReadOnlyList<FlowTransition<TState>> Transitions => _transitions;

        public FlowGraph<TState> AddNode(string nodeId, NodeHandler<TState> handler)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
                throw new ArgumentException("Node id is required.", nameof(nodeId));
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            _nodes[nodeId] = handler;
            return this;
        }

        public FlowGraph<TState> AddTransition(string from, string to, TransitionGuard<TState> when)
        {
            if (string.IsNullOrWhiteSpace(from))
                throw new ArgumentException("From node id is required.", nameof(from));
            if (string.IsNullOrWhiteSpace(to))
                throw new ArgumentException("To node id is required.", nameof(to));
            _transitions.Add(new FlowTransition<TState>(from, to, when));
            return this;
        }

        public NodeHandler<TState> RequireNode(string nodeId)
        {
            if (!_nodes.TryGetValue(nodeId, out var handler))
                throw new InvalidOperationException($"Node '{nodeId}' is not registered.");
            return handler;
        }

        public string? ResolveNextNode(string from, TState state, NodeResult result)
        {
            for (var i = 0; i < _transitions.Count; i++)
            {
                var t = _transitions[i];
                if (!string.Equals(t.From, from, StringComparison.Ordinal))
                    continue;
                if (t.When(state, result))
                    return t.To;
            }

            return null;
        }
    }
}
