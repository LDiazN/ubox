using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Utils;

namespace World
{
    public struct ChunkData
    {
        public Array3D<BlockData> Blocks;
        public int3 Position;

        public void Dispose() => Blocks.Dispose();
    }

    public struct BlockData
    {
        public BlockType Type;
    }

    public class ChunkMap
    {
        Dictionary<int3, ChunkData> _map = new();
        public Dictionary<int3, ChunkData> Map => _map;


        public bool GetChunk(int x, int y, int z, out ChunkData data)
        {
            var chunkPosition = WorldToChunkGrid(x, y, z);
            return _map.TryGetValue(chunkPosition, out data);
        }

        public bool GetBlockData(int x, int y, int z, out BlockData data)
        {
            var found = GetChunk(x, y, z, out var chunkData);
            if (!found)
            {
                data = default;
                return false;
            }

            var localChunkCoords = ToChunkCoordinates(x, y, z);
            data = chunkData.Blocks.Get(localChunkCoords.x, localChunkCoords.y, localChunkCoords.z);

            return true;
        }

        public void AddChunk(int x, int y, int z)
        {
            var chunkCoords = WorldToChunkGrid(x, y, z);
            Debug.Assert(!_map.ContainsKey(chunkCoords), "Replacing existent chunk. Do you really want to do this?");
            _map[chunkCoords] = new ChunkData
            {
                // Note that the blocks are 0-initialized, empty block is 0, so the entire chunk is empty
                Blocks = new(
                    ChunkRenderer.ChunkSize, ChunkRenderer.ChunkSize, ChunkRenderer.ChunkSize, Allocator.Persistent
                    ),
                Position = chunkCoords
            };
        }

        // Returns whether a new chunk was created to set this block
        public bool SetBlock(int x, int y, int z, BlockData data)
        {
            var chunkExists = GetChunk(x, y, z, out var chunk);
            var coords = ToChunkCoordinates(x, y, z);
            if (chunkExists)
            {
                chunk.Blocks.Set(coords.x, coords.y, coords.z, data);
                return false;
            }

            AddChunk(x, y, z);
            var found = GetChunk(x, y, z, out chunk);
            Debug.Assert(found, "Recently added chunk is not present");

            chunk.Blocks.Set(coords.x, coords.y, coords.z, data);

            return true;
        }


        public void Dispose()
        {
            foreach (var entry in _map.Values)
                entry.Dispose();
        }


        // converts from world poisition to the corresponding chunk grid coordinates
        public static int3 WorldToChunkGrid(int x, int y, int z)
        {
            int cx = x % ChunkRenderer.ChunkSize;
            int cy = y % ChunkRenderer.ChunkSize;
            int cz = z % ChunkRenderer.ChunkSize;

            return new int3(
                cx < 0 ? x - cx - ChunkRenderer.ChunkSize : x - cx,
                cy < 0 ? y - cy - ChunkRenderer.ChunkSize : y - cy,
                cz < 0 ? z - cz - ChunkRenderer.ChunkSize : z - cz
            );
        }

        public static bool IsChunkCoords(int x, int y, int z)
        {
            return x % ChunkRenderer.ChunkSize == 0 &&
                   y % ChunkRenderer.ChunkSize == 0 &&
                   z % ChunkRenderer.ChunkSize == 0;
        }

        public static bool IsChunkCoords(int3 pos) => IsChunkCoords(pos.x, pos.y, pos.z);

        // Returns a position inside a chunk, from 0 to ChunkSize-1
        public static int3 ToChunkCoordinates(int x, int y, int z)
        {
            // Note that % is the remainder, not the modulo.
            // modulo(-15, 16) should be 1, but -15 % 16 is -15
            int cx = x % ChunkRenderer.ChunkSize;
            int cy = y % ChunkRenderer.ChunkSize;
            int cz = z % ChunkRenderer.ChunkSize;

            return new int3(
                cx < 0 ? cx + ChunkRenderer.ChunkSize : cx,
                cy < 0 ? cy + ChunkRenderer.ChunkSize : cy,
                cz < 0 ? cz + ChunkRenderer.ChunkSize : cz
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
