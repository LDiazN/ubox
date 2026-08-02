# Ubox

<img width="1919" height="1079" alt="image4" src="https://github.com/user-attachments/assets/6e5aaa86-032b-49b6-8e58-6d79c485d247" />

Ubox is a Minecraft-like demo game that shows how to render an infinite voxel world with good performance.
It's made in Unity and achieves nearly 300 FPS on my machine in a world of 256 * 256 * 32 = **2 097 152** 
cubes. 

The world is generated procedurally using a simple noise-sampling algorithm to generate a height map, it's 
infinite in all directions.

The secret for the good performance is a combination of: 

- Chunking blocks of 16x16x16 cubes into a single game object, and dynamically generating the mesh (and collision mesh) on the fly

- Heavily leveraging parallelism to optimize unrelated computations: Burst and the Unity Job System

- Offload as much work possible out of the main thread to avoid stutters: Again, the Unity Job System

- Using the right internal representation for world data
  
The remainder of this readme is a detailed explanation about how this project was done.

## Game Description

Ubox tries to recreate the core mechanics of Minecraft: Procedural generation and cube-based construction. 
The player controls a first person character in an infinite world generated procedurally. They can place 
or take blocks on any position to create buildings. 

### Controls

| **Key Bindings** | **Actions**                                                           |
| ---------------- | --------------------------------------------------------------------- |
| WASD             | Move                                                                  |
| Mouse Movement   | Move camera                                                           |
| Left click       | Place block                                                           |
| Right Click      | Remove block                                                          |
| Space            | Jump                                                                  |
| Numbers          | Choose block (we only have two blocks, so only 1 and 2 are available) |
| ESC              | Pause / Resume                                                        |
| Tab              | Stats panel                                                           |

You can change the mouse sensibility from the Pause menu.

You can see interesting stats about the game in the stats panel, like memory footprint, loaded chunks, pending jobs and so on.

## Problem statement

I started this project expecting that rendering so many cube objects would be expensive, 
so to get a sense of how much I started with a simple program to fill a space of 64x64x16 
with cloned cubes in Unity: 

<img width="466" height="336" alt="image1" src="https://github.com/user-attachments/assets/d4fcf264-86d2-47f2-b408-efcb603e1ab5" />

I only got **8 fps**. This world is equivalent to 4 squared chunks in Minecraft, it's quite small. 
And yet it was already hard to move around the Unity editor.

My next best idea was to use the [entity system](https://docs.unity3d.com/Packages/com.unity.entities@1.0/manual/index.html) from 
[Unity DOTS](https://unity.com/es/dots). My understanding was that the entity system was designed to handle a huge entity count, the 
engine could handle this problem automagically.

I only got **30 FPS** on the Unity editor. It was good progress for very little effort, but the world was still small and if I wanted 
to add more features in the future I would need more frame budget. 

It also felt very silly to have a bunch of entities that... did nothing, there was no real logic running per cube, they just sat there
and applied physics. If you think about it, the world modification logic is not really per cube, is something that interacts with the 
world itself. 

This made me think that maybe this wasn't the proper approach for this problem.  

### Profiling 

I went back to my basic world with 1 game object per cube and fired up the **profiler**. I wanted to have an informed perspective of what 
was slowing down my game. It was clearly the shear amount of cubes but I wanted to know exactly what about them was generating the 
problem. 
a
<img width="1634" height="911" alt="image6" src="https://github.com/user-attachments/assets/10c1f595-fd0b-4efd-810c-475aacae69f2" /> 

1. `Gfx.WaitForGfxCommandsFromMainThread`: This is the key part, it tells us that the render thread is waiting for rendering instructions. It wastes a lot of time in this state.
2. Tehn we can see that every worker thread is doing some processing related to rendering. This is CPU work that needs to be done before sending GPU commands.
3. `WaitForJobGroupID` means that the main thread is waiting for work that it's being executed in worker threads.

From this profiling we reach the following conclusion: **Our program is CPU-bound**, not GPU-boud. Our bad framerate derives from the CPU not sending
data to the GPU fast enough, not due to the GPU having too much work. This is for sure due to the high game object count we have at any moment, the objects themselves are already quite simple.

So now our problem is reduced to: **How can we reduce the game object count in the game to reduce CPU pressure?**

## Solution

Since the problem is having too many cubes, a direct solution would be to group cubes into a single object, that we will call **chunk**.

From now on, we define: 

- **cube**: the smallest terrain unit
- **chunk**: a spatial group of 16x16x16 cubes

Let's say for now the chunk size is 
16 x 16 x 16. Let's see what's the reduction in object count by doing this:   

- 16 x 16 x 16 represents 4096 cubes, and therefore game objects
- A chunk is a single game object
- So we go from 4096 to 1, that's a 99.98% reduction in object count per chunk!

  
<img width="800" height="600" alt="ubox(1)" src="https://github.com/user-attachments/assets/49e302e1-1d96-4d42-bd0d-eb4657ff5ca4" />


This approach is overwhelmingly effective in reducing object count, but now we have to solve a new problem. **How can we represent many objects as a single one?**

This is what we care about a cube: 

- Type, so that we know what can or can't do with it and how to render it
- Collider, so that the player can interact physically
- Rendering, so that the player can see the cube. Includes mesh and material
- Position, to properly place it

If we split our internal representation of the world from the game objects used to render it, we can use the following strategy to merge cube into chunks: 

- The world is internally represented as a collection of arrays, one per chunk. Each array has the types of the cubes within its chunk
  - We use 1 byte to represent the types of cubes, saving some space. This affords us 256 cube types, not a lot but is easy to increase to 2 bytes for 65536 cube types
  - You get the type of a cube from its world position by computing its chunk's position and the offset within that chunk  
- Each chunk has a custom collision mesh and a single mesh renderer
- The mesh used for rendering and the collision is regenerated on the fly when:
  - The chunk is rendered for the first time
  - A cube within the chunk has changed

Let's think about the world structure for a moment. If you think about it, there's always a cube in every integer position at any given time. Some are just 'air', 
blocks that can be traversed by the player. 

If we think like that, we can start treating the position of a cube as its reference. Every time we want to get the data of a single cube, we do so by its position. This is a
powerful property because it allows us to imagine the world as infinite, while the cubes don't actually exist on memory or are not loaded from disk. 

This structure also allows us to only have some data of the cubes loaded or used for some processes. For example, generating the mesh only requires the `type` data, nothing more. 
This way we optimize cache usage. 

Finally, the solution goes as follows: 

- The world is represented with [PODs](https://en.wikipedia.org/wiki/Passive_data_structure), not with Game Objects. Those are used for **rendering** the world.
- Terrain is divided in chunks
- When a chunk is loaded, the corresponding mesh is generated on the fly representing the blocks within the chunk
- A manager objects constantly checks the player position to decide which chunks should be loaded or unloaded 
  - Chunks are managed through an object pool, to reduce creations and destructions
- The world manager also generates the world while the player is discovering it
  - The "world" here means the internal representation based on PODs
 
### Fast sometimes

This solution was quite effective on the rendering side, when the world is fully loaded the frame rate is very good and stable. However when the player tries to move or modify the world 
we got sever stutters. This was due to the high load on the main thread due to building meshes and generating the world. 

Making this significantly faster so that stutters were impossible didn't seem realistic, mostly because the process itself was fast, it was just called many times: One per rendered chunk. So the next best thing was to **move computation out of the main thread**. 

For this we leveraged [Unity's Job System](https://docs.unity3d.com/6000.3/Documentation/Manual/job-system-overview.html) and [Burst](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/index.html). The idea behind the job system is that you can send expensive function calls to a pool of worker threads, and it would get executed in parallel to the main thread (And each other!). On top of that, the Burst compiler allows you to compile your job code into native code, which runs faster than C#'s byte code. For this to work we also used native data structures: `NativeArray`, `NativeHashmpa`, `float3`, `int3`.

In fact just the Burst compilation reduced the chunk mesh building time **in half**!

| <img src="https://github.com/user-attachments/assets/f227f2b0-12c9-4a45-8cb7-e7d342cc3ac6" width="316" height="287" /> | <img src="https://github.com/user-attachments/assets/612de6f2-2443-4714-8f15-2dd73cfe6602" width="282" height="241" /> |
|---|---|
| No Burst | Burst |






