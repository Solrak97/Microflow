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

