# BehaviorTreeEngine

BehaviorTreeEngine (BTEngine) is a Space Engineers behavior-tree engine intended to be used by other mods.

## Current API status

The repository currently contains an experimental callback-based client API:

- A heartbeat lets a client mod detect when BTEngine is running.
- Client mods register custom node callbacks.
- BTEngine wraps each registered callback set in its own native `BehaviorNode`.
- Client mods describe trees using `BTTree.Sequence`, `BTTree.Selector`, `BTTree.Loop`, and `BTTree.Custom`.
- Client mods can request that BTEngine start or stop a tree for a character.

This is a callback bridge, not direct inheritance from the provider's `BehaviorNode`. The client contract defines callbacks that BTEngine invokes through the mod-message registration API.

**Experimental:** the code still needs to be compiled and tested in Space Engineers with BTEngine and a separate consumer mod enabled together. Successful message registration alone does not prove that a custom node executes correctly.

## Client mod setup

Copy `Content/Data/Scripts/BehaviorTreeAPI/API/BehaviorTreeAPI.cs` into the consumer mod's source project. Keep the file's namespace `BehaviorTreeAPI.API`. Do not copy the engine's core implementation into the consumer mod.

Call `BTAPI.Init` from the consumer mod's load lifecycle and call `BTAPI.Update` from its per-frame session update. Call `BTAPI.Dispose` during unload.

Example registration pattern:

```csharp
using BehaviorTreeAPI.API;
using System;
using VRage.Game.ModAPI;

private BTAPI btApi;

private void OnBTEngineDetected()
{
    btApi.RegisterCustomNode(
        "MyMod.WaitNode",
        CreateWaitState,
        StartWaitNode,
        TickWaitNode,
        EndWaitNode,
        OnNodeRegistered);
}

private object CreateWaitState()
{
    return new WaitNodeState();
}

private void StartWaitNode(IMyCharacter character, object state)
{
    WaitNodeState wait = (WaitNodeState)state;
    wait.Elapsed = 0f;
}

private int TickWaitNode(IMyCharacter character, object state, float dt)
{
    WaitNodeState wait = (WaitNodeState)state;
    wait.Elapsed += dt;

    if (wait.Elapsed < 2f)
        return BTAPI.CustomNodeRunning;

    return BTAPI.CustomNodeSuccess;
}

private void EndWaitNode(IMyCharacter character, object state)
{
}

private void OnNodeRegistered(bool accepted)
{
    // Register a tree only after the node is accepted.
}

private class WaitNodeState
{
    public float Elapsed;
}
```

The example methods must be members of the consumer mod's class. Initialize `btApi` and call `btApi.Init(OnBTEngineDetected)` before the callback can run.

Use globally distinctive node IDs, such as `MyMod.WaitNode`, because registrations are currently keyed by node ID.

## Register and start a tree

After the node registration callback reports success, register a tree definition:

```csharp
bool sent = btApi.RegisterTree(
    "MyMod.TestTree",
    BTTree.Sequence(
        BTTree.Custom("MyMod.WaitNode")
    ),
    OnTreeRegistered);
```

When `OnTreeRegistered` reports success and the target character is available, start it:

```csharp
private void OnTreeRegistered(bool accepted)
{
    if (!accepted)
        return;

    IMyCharacter character = GetTargetCharacter();
    if (character != null)
        btApi.StartTree(character, "MyMod.TestTree");
}
```

`GetTargetCharacter()` above is a placeholder for the consumer mod's own character lookup.

Custom-node tick results are:

- `BTAPI.CustomNodeRunning` (0): keep ticking the node.
- `BTAPI.CustomNodeSuccess` (1): finish successfully.
- `BTAPI.CustomNodeFailure` (2): finish with failure.

## Current limitations / testing checklist

- Verify the heartbeat is received when BTEngine is enabled and times out when it is absent.
- Verify a client custom-node registration is accepted and rejected duplicate IDs are handled.
- Verify a tree containing the custom node starts, ticks, and stops as expected.
- Verify callbacks and trees are cleaned up correctly when the consumer mod unloads or the world closes.
- Test multiplayer and dedicated-server behavior separately before treating the bridge as multiplayer-safe.

The bridge is experimental until these checks pass in-game.
