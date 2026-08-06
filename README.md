# Ubox

<img width="1919" height="1079" alt="image4" src="https://github.com/user-attachments/assets/6e5aaa86-032b-49b6-8e58-6d79c485d247" />

Ubox is a Minecraft-like demo game that shows how to render an infinite voxel world with good performance.
It's made in Unity and achieves nearly 300 FPS on my machine in a world of 256 * 256 * 32 = **2 097 152** 
cubes. 

The world is generated procedurally using a simple noise-sampling algorithm to generate a height map. It's 
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

You can change the mouse sensitivity from the Pause menu.

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

It also felt very silly to have a bunch of entities that... did nothing. There was no real logic running per cube, they just sat there
and applied physics. If you think about it, the world modification logic is not really per cube, it's something that interacts with the 
world itself. 

This made me think that maybe this wasn't the proper approach for this problem.  

### Profiling 

I went back to my basic world with 1 game object per cube and fired up the **profiler**. I wanted to have an informed perspective of what 
was slowing down my game. It was clearly the sheer amount of cubes but I wanted to know exactly what about them was generating the 
problem. 

<img width="1634" height="911" alt="image6" src="https://github.com/user-attachments/assets/10c1f595-fd0b-4efd-810c-475aacae69f2" /> 

1. `Gfx.WaitForGfxCommandsFromMainThread`: This is the key part, it tells us that the render thread is waiting for rendering instructions. It wastes a lot of time in this state.
2. Then we can see that every worker thread is doing some processing related to rendering. This is CPU work that needs to be done before sending GPU commands.
3. `WaitForJobGroupID` means that the main thread is waiting for work that it's being executed in worker threads.

From this profiling we reach the following conclusion: **Our program is CPU-bound**, not GPU-bound. Our bad framerate derives from the CPU not sending
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

This is what we care about in a cube: 

- Type, so that we know what can or can't do with it and how to render it
- Collider, so that the player can interact physically
- Rendering, so that the player can see the cube. Includes mesh and material
- Position, to properly place it

If we split our internal representation of the world from the game objects used to render it, we can use the following strategy to merge cubes into chunks: 

- The world is internally represented as a collection of arrays, one per chunk. Each array has the types of the cubes within its chunk
  - We use 1 byte to represent the types of cubes, saving some space. This affords us 256 cube types, not a lot but is easy to increase to 2 bytes for 65536 cube types
  - You get the type of a cube from its world position by computing its chunk's position and the offset within that chunk  
- Each chunk has a custom collision mesh and a single mesh renderer
- The mesh used for rendering and the collision is regenerated on the fly when:
  - The chunk is rendered for the first time
  - A cube within the chunk has changed

Let's think about the world structure for a moment. If you think about it, there's always a cube in every integer position at any given time. Some are just 'air' 
blocks that can be traversed by the player. 

If we think like that, we can start treating the position of a cube as its reference. Every time we want to get the data of a single cube, we do so by its position. This is a
powerful property because it allows us to imagine the world as infinite, while the cubes don't actually exist on memory or are not loaded from disk. 

This structure also allows us to only have some data of the cubes loaded or used for some processes. For example, generating the mesh only requires the `type` data, nothing more. 
This way we optimize cache usage. 

Finally, the solution goes as follows: 

- The world is represented with [PODs](https://en.wikipedia.org/wiki/Passive_data_structure), not with Game Objects. Those are used for **rendering** the world.
- Terrain is divided in chunks
- When a chunk is loaded, the corresponding mesh is generated on the fly representing the blocks within the chunk
- A manager object constantly checks the player position to decide which chunks should be loaded or unloaded 
  - Chunks are managed through an object pool, to reduce creations and destructions
- The world manager also generates the world while the player is discovering it
  - The "world" here means the internal representation based on PODs
 
### Fast sometimes

This solution was quite effective on the rendering side, when the world is fully loaded the frame rate is very good and stable. However when the player tries to move or modify the world 
we got severe stutters. This was due to the high load on the main thread due to building meshes and generating the world. 

Making this significantly faster so that stutters were impossible didn't seem realistic, mostly because the process itself was fast, it was just called many times: One per rendered chunk. So the next best thing was to **move computation out of the main thread**. 

For this we leveraged [Unity's Job System](https://docs.unity3d.com/6000.3/Documentation/Manual/job-system-overview.html) and [Burst](https://docs.unity3d.com/Packages/com.unity.burst@1.8/manual/index.html). The idea behind the job system is that you can send expensive function calls to a pool of worker threads, and it would get executed in parallel to the main thread (And each other!). On top of that, the Burst compiler allows you to compile your job code into native code, which runs faster than C#'s byte code. For this to work we also used native data structures: `NativeArray`, `NativeHashMap`, `float3`, `int3`.

In fact just the Burst compilation reduced the chunk mesh building time **in half**!

| <img src="https://github.com/user-attachments/assets/f227f2b0-12c9-4a45-8cb7-e7d342cc3ac6" width="316" height="287" /> | <img src="https://github.com/user-attachments/assets/612de6f2-2443-4714-8f15-2dd73cfe6602" width="282" height="241" /> |
|---|---|
| No Burst | Burst |

_Some times indicate 0ms because they are constructing an empty mesh. This is just an illustrative metric._

The next screenshot shows the worker threads with full utilization building meshes:  

<img width="1911" height="1020" alt="image8" src="https://github.com/user-attachments/assets/0933f904-9d71-4806-ad49-aed1ccb1d258" />

Note that this approach to parallel computing is not ideal for every situation. If the game logic requires immediate access to things that are executed in the background, you can 
get consistency errors. In this case our solution works because background tasks only touch terrain that is far away from the player most of the time.  

## Architecture

Given the nature of the game, we decided to go for a [data oriented](https://en.wikipedia.org/wiki/Data-oriented_design) approach for the world representation. This means that chunks are not stored as an array of structs, but as a [struct of arrays](https://en.wikipedia.org/wiki/AoS_and_SoA).  

The main classes in charge of terrain generation are:

- **ChunkMap:** The main data structure representing the world. It's a table mapping `int3` positions to their properties. In our case we only have a type. It's implemented as a native dictionary. 
  - A cube's position is used as its ID. Remember, 1 position = 1 cube. With the position we lookup its properties
  - We store chunks, not cubes, but a cube's data can be retrieved by looking up its chunk and then the block by its offset within the chunk
  - We add new properties to cubes by adding a new map within the `ChunkMap` with the same structure. This is great for optional data, as you don't have to create the chunk if the data is unset.  
  - A chunk is a 3D data structure, but its data is stored in a plain array, with some utility functions to access the data as a 3D array.
  - Thanks to this approach, functions that iterate over all the cube types of the world can only load the data they need, optimizing cache usage
    - This works due to our access patterns, different programs with different access patterns might perform poorly
  - I didn't implement it but this data structure is good for loading/unloading to/from disk unused data
  - The type is represented as a **single byte**, this affords us 256 block types (including a null one), but it reduces memory usage a lot over the default Enum size (4 bytes). Every chunk is 4KB of memory (16^3)

A chunk map looks like this: 
```
struct ChunkMap {
  types : Map<int3, Array3D<ChunkType>[16^3]>
}
```

<img width="275" height="88" alt="image3" src="https://github.com/user-attachments/assets/a888bf29-b05c-4bdb-9e8a-d60987c72834" />

- **ChunkManager:** a `MonoBehaviour` that implements chunk loading and unloading, based on the player position and render view distance.
  - Since many things are done in parallel, this class is in charge of bookkeeping. It checks which tasks are done and which terrain changes haven't taken effect yet
  - When a player puts/takes a cube, it modifies the `ChunkMap` and notifies the corresponding `ChunkRenderer` that it should update itself
  - The chunk renderer update can be slow as well. If the player tries to modify a chunk that is still WIP, the change is noted down and queued
  - Change may not be immediate
  - Offers an API for other classes to modify the world
  - When an unknown chunk is needed, it runs the procedural generation algorithm to fill it, in background

- **ChunkRenderer:** A `MonoBehaviour` that implements the mesh generation and renders it using a `MeshRenderer`.
  - Computes the new mesh in the background.
  - If several changes are received while the mesh is still generating, these are enqueued.
  - Creation and destruction is implemented using object pooling
 
## Procedural Generation

Procedural generation is very simple, it's implemented by sampling simplex noise using the XZ coordinates of each cube, and using the result as height map: If the Y coordinate
is lower than this height, the cube is solid. 

This method has an interesting property: to generate a cube, you **don't need information about its neighbors**. This makes the generation trivial to parallelize. If you need to know
if another cube will be solid, you can sample its position as well, without having to access its memory.

Example Simplex noise texture (from [Wikipedia](https://en.wikipedia.org/wiki/Simplex_noise)):


<img width="256" height="256" alt="image" src="https://github.com/user-attachments/assets/7ab52884-2145-49bb-b8ff-4089658d42c3" />


We mark as "Grass" cubes that don't have any solid cube on top of them.   

New chunks are generated as the player discovers them. 

The generation is actually very fast, but there are usually many chunks generating at the same time. This can generate stutters, and that's why the generation runs in the background.

## Mesh generation 

The mesh generation algorithm went through several iterations until I got one with good performance:

1. Merge all cube meshes into the same mesh: Too slow, too much geometry. A full chunk with all the cubes has 16^3 cubes, each cube has 12 triangles, so this mesh had 4096 * 12 = **49152** triangles per chunk. The reference world of 64x64x16 had **786432** triangles. The real problem is that most of this geometry is **invisible** and will never be visible, it's inside the chunk itself.
2. Only take cubes that are in the outer layer of a chunk: A lot better but still unable to get 60 FPS reliably on Unity. This reduced the triangle count quite a lot, going back to the full chunk example: 16^3 - 14^3 = 1352 cubes, 1352 * 12 triangles = **16224** triangles, this is a reduction of **67%** in triangle count.
3. Only take visible faces: This was a bit harder to code but it had the best performance by far and is the current implementation. The triangle count is: 16 * 16 * 6 faces * 2 triangles = **3072**, this is a reduction of **93.8%** from our original number, it even achieved more than **200 FPS** on a build. 

An important reason for this process to be fast is that since it runs in the background, it can generate **weird graphics and physics bugs** if it takes too long.

The tricky part of generating the mesh is maintaining the structure of the index buffer when choosing only visible faces. The solution we chose was to allocate the full vertices of each cube, and then we only add to the index buffers the indexes of the visible faces. This approach wastes some vertices, but at least they won't be rendered due to the index buffer structure. 

The next problem to solve was textures. Since we only have a single object that represents many, we have to find a way to paint it with many textures. For this we use an atlas, a texture with many textures embedded. The atlas is built in a way such that textures are lined up on the Y axis, and when creating a cube, its UVs are scaled and moved in the Y axis to fit its corresponding texture position. 

<img width="612" height="379" alt="Atlas Texture" src="https://github.com/user-attachments/assets/ec5a8be6-5a85-482f-a217-c712bb40f68b" />

This way, it's not necessary to implement a new shader or material; the material that works for a single cube works for a block as well. We don't add any additional attributes to vertices either. 

Due to Unity's technical limitations, we can't allocate GPU memory in a thread that is not the main thread. As a result, this algorithm is split in two parts: **index count** and **buffer generation**. Both are done on background threads. 

- **Index count** will count how many vertices and indices we will need. The result is sent to the main thread, where a new mesh is allocated and passed to the next task.
- **Buffer generation** will take the buffer allocated on the main thread and fill it using the approach mentioned above. Then the main thread will update the previous mesh with this one.

## Conclusions

This was a fun little project that took me ~5 days and I learned a lot, I want to note down some of the lessons I got: 

- This should be obvious by now but this project was a good reminder: **Never choke the main thread**, always move IO or expensive computations to a different thread. Even if a process doesn't run every frame it's good to send it to a background thread if it risks choking the main thread
- Profile first: It can be daunting to find a performance bottleneck without the profiler, the profiler will always tell you the first place to look at. Everything else is close to guessing
- You don't have to bind your internal game representation to Unity game objects. You can roll your own representation and use game objects as a way to render that into the screen.
- If something doesn't require same-frame or near-same-frame interactivity, you can send it to another thread to offload a lot of work from the main thread
- Unity is not really great for dynamic mesh generation. The sync step where tasks have to go back to the main thread to allocate GPU memory is still a choke point that I couldn't workaround
  - On that lane, Unity wasn't really useful for this project in particular, a lot of the tools that carried the project bypass key properties of Unity. Burst compiles to native code instead of C# code for example, the job system relies on non-managed data structures. The most helpful Unity feature was the profiler
- The first cube I could place was a magical moment:

<img width="1344" height="751" alt="image" src="https://github.com/user-attachments/assets/da3c44bd-9732-40de-bd63-230e34b32b60" />

This was the first house I built with my own game :)
