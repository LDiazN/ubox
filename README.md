# Ubox
<img width="1919" height="1079" alt="image4" src="https://github.com/user-attachments/assets/6e5aaa86-032b-49b6-8e58-6d79c485d247" />


Ubox is a minecraft-like demo game that shows how to render an infinite voxel world with good performance.
It's made in Unity and achieves nearly 300 FPS on my machine in a world of 256 * 256 * 32 = **2 097 152** 
cubes. 

The world is generated procedurally using a simple noise-sampling algorithm to generate a height map, it's 
infinite in all directions.

The secret for the good performance is a combination of: 

- Chunking blocks of 16x16x16 cubes into a single game object, and dynamically generating the mesh (and collision mesh) on the fly
- Heavily leveraging parallelism to optimize unrelated computations
- Offload as much work possible out of the main thread to avoid stutters
- Using the right internal representation for world data

 The remainder of this readme is a detailed explanation about how this project was done.


