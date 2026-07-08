using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using Utils;

namespace World
{
    public struct ChunkData
    {
        public Array3D<BlockType> Blocks;
        public int3 Position;

        public void Dispose() => Blocks.Dispose();
    }

    public class ChunkMap
    {
        public const int ChunkSize = 16;
        NativeHashMap<int3, ChunkData> _map;
        public NativeHashMap<int3, ChunkData> Map => _map;

        public ChunkMap(Allocator allocator)
        {
            _map = new NativeHashMap<int3, ChunkData>(32768, allocator); // 2^15
        }

        public bool GetChunk(int x, int y, int z, out ChunkData data)
        {
            var chunkPosition = WorldToChunkGrid(x, y, z);
            return _map.TryGetValue(chunkPosition, out data);
        }

        public void AddChunk(int x, int y, int z)
        {
            var chunkCoords = WorldToChunkGrid(x, y, z);
            Debug.Assert(!_map.ContainsKey(chunkCoords), "Replacing existent chunk. Do you really want to do this?");
            _map[chunkCoords] = new ChunkData
            {
                // Note that the blocks are 0-initialized, empty block is 0, so the entire chunk is empty
                Blocks = new(
                    ChunkSize, ChunkSize, ChunkSize, Allocator.Persistent
                ),
                Position = chunkCoords
            };
        }

        // Returns whether a new chunk was created to set this block
        public bool SetBlock(int x, int y, int z, BlockType type)
        {
            var chunkExists = GetChunk(x, y, z, out var chunk);
            var coords = ToChunkCoordinates(x, y, z);
            if (chunkExists)
            {
                chunk.Blocks.Set(coords.x, coords.y, coords.z, type);
                return false;
            }

            AddChunk(x, y, z);
            var found = GetChunk(x, y, z, out chunk);
            Debug.Assert(found, "Recently added chunk is not present");

            chunk.Blocks.Set(coords.x, coords.y, coords.z, type);

            return true;
        }


        public void Dispose()
        {
            foreach (var entry in _map)
                entry.Value.Dispose();

            _map.Dispose();
        }


        // converts from world poisition to the corresponding chunk grid coordinates
        public static int3 WorldToChunkGrid(int x, int y, int z)
        {
            int cx = x % ChunkSize;
            int cy = y % ChunkSize;
            int cz = z % ChunkSize;

            return new int3(
                cx < 0 ? x - cx - ChunkSize : x - cx,
                cy < 0 ? y - cy - ChunkSize : y - cy,
                cz < 0 ? z - cz - ChunkSize : z - cz
            );
        }

        public static bool IsChunkCoords(int x, int y, int z)
        {
            return x % ChunkSize == 0 &&
                   y % ChunkSize == 0 &&
                   z % ChunkSize == 0;
        }

        // Returns a position inside a chunk, from 0 to ChunkSize-1
        public static int3 ToChunkCoordinates(int x, int y, int z)
        {
            // Note that % is the remainder, not the modulo.
            // modulo(-15, 16) should be 1, but -15 % 16 is -15
            int cx = x % ChunkSize;
            int cy = y % ChunkSize;
            int cz = z % ChunkSize;

            return new int3(
                cx < 0 ? cx + ChunkSize : cx,
                cy < 0 ? cy + ChunkSize : cy,
                cz < 0 ? cz + ChunkSize : cz
            );
        }
    }

    public enum BlockType : byte
    {
        Empty = 0,
        Grass,
        Dirt,
        None // Like "Null"
    }
}
