---
name: sf-pools
description: Use when loading config-defined SFramework prefab pools or spawning and despawning reusable GameObjects.
---

# sf-pools

Read [the package guide](references/package-readme.md) and [installed source](SOURCE.md).

Define SFPoolsConfig and full prefab IDs, mark the prefab Addressable, and initialize Configs before ISFPoolsService. The pool indexes SFPoolsConfig entries from mixed config collections.

Await Load(fullId) before Spawn. Unknown load IDs throw; an unloaded pool makes Spawn return false. Generic Spawn also returns false if the clone's root lacks T.

Reset object state in application-owned spawn/despawn code. Despawn returns a checked-out instance even if the caller disabled it.

Call the service on Unity's main thread. Concurrent Load callers share one Addressables operation, can observe progress, and cancel their own waits. Unload and Dispose invalidate pending loads, destroy active and inactive clones, and release the service's handle after teardown. The caller does not need a second Addressables handle.

New pooled instances traverse plain ISFInjectable components including inactive children. SFView is skipped by the pool traversal because it owns Awake injection. Reuse does not repeat injection.

Verify loaded/unloaded spawning, component presence, reset behavior, reuse, addressable lifetime and cleanup in the consuming project's scene.
