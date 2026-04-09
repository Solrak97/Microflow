# Microflow

Tiny deterministic state-machine / graph runtime for agent workflows.

`Microflow` is designed for projects that need:

- explicit node transitions
- typed shared state
- async-friendly node execution
- predictable step-by-step execution
- simple trace events for debugging

## Packages

- `Microflow.Core`: graph, runner, node contracts, trace events
- `Microflow.Sample`: tiny console sample showing a planner/executor loop

## Quick Example

```csharp
var graph = new FlowGraph<AgentState>("planner")
    .AddNode("planner", AgentNodes.PlanAsync)
    .AddNode("executor", AgentNodes.ExecuteAsync)
    .AddNode("done", AgentNodes.DoneAsync)
    .AddTransition("planner", "executor", (s, o) => o == NodeOutcome.Success)
    .AddTransition("executor", "planner", (s, o) => o == NodeOutcome.Continue)
    .AddTransition("executor", "done", (s, o) => o == NodeOutcome.Success);

var runner = new FlowRunner<AgentState>(graph);
var result = await runner.RunAsync(state, maxSteps: 32, cancellationToken);
```

## Design Goals

- deterministic by default
- no Unity dependency in `Core`
- easy to embed in game loops or services
- minimal API surface

## React-Friendly Pattern

Use `FlowStepper<TState>` to execute one node at a time and push events into a React store (Redux/Zustand/etc.).

```csharp
var graph = EmbodiedAgentPattern.Build<MyState>(
    plan: Nodes.PlanAsync,
    execute: Nodes.ExecuteAsync,
    observe: Nodes.ObserveAsync,
    recover: Nodes.RecoverAsync,
    done: Nodes.DoneAsync);

var stepper = new FlowStepper<MyState>(graph);

// Called from a UI loop / button / interval:
var step = await stepper.StepAsync(state, ct);
traceEvents.Add(step.TraceEvent); // render in timeline
currentNode = stepper.CurrentNodeId; // render active node
```

This enables:

- planner/executor loops with full UI visibility
- pause/resume and manual stepping
- human approval checkpoints between steps

## Tool Call Node

`Microflow.Core` includes reusable tool-call contracts and a node factory:

- `ToolCallRequest`
- `ToolCallResult`
- `IToolInvoker`
- `ToolCallNode.Create(...)`

Example:

```csharp
var invokeTool = ToolCallNode.Create<MyState>(
    invoker: myToolInvoker,
    requestSelector: s => s.PendingToolCall,
    resultWriter: (s, r) => s.LastToolCall = r);
```

The generated node maps tool outcomes to flow outcomes:

- `Success -> NodeOutcome.Success`
- `Retryable failure -> NodeOutcome.Retry`
- `Non-retryable failure -> NodeOutcome.Failed`

