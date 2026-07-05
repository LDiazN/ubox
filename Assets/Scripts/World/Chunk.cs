using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;
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
        private Dictionary<int3, ChunkData> _map = new();

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

            var localChunkCoords = new int3(x % WorldChunkV2.ChunkSize, y % WorldChunkV2.ChunkSize, z % WorldChunkV2.ChunkSize);
            data = chunkData.Blocks.Get(localChunkCoords.x, localChunkCoords.y, localChunkCoords.z);

            return true;
        }

        public void AddChunk(int x, int y, int z)
        {
            var chunkCoords = WorldToChunkGrid(x,y,z);
            Debug.Assert(!_map.ContainsKey(chunkCoords), "Replacing existent chunk. Do you really want to do this?");
            _map[chunkCoords] = new ChunkData
            {
                // Note that the blocks are 0-initialized, empty block is 0, so the entire chunk is empty
                Blocks = new(
                    WorldChunkV2.ChunkSize, WorldChunkV2.ChunkSize, WorldChunkV2.ChunkSize, Allocator.Persistent
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
                chunk.Blocks.Set(coords.x,coords.y,coords.z, data);
                return false;
            }

            AddChunk(x,y,z);
            GetChunk(x, y, z, out chunk);

            chunk.Blocks.Set(coords.x, coords.y, coords.z, data);

            return true;
        }

        public void Dispose()
        {
            foreach(var entry in _map.Values)
                entry.Dispose();
        }


        // converts from world poisition to the corresponding chunk grid coordinates
        public static int3 WorldToChunkGrid(int x, int y, int z)
        {
            return new int3(
                x - x % WorldChunkV2.ChunkSize,
                y - y % WorldChunkV2.ChunkSize,
                z - z % WorldChunkV2.ChunkSize
            );
        }

        // Returns a position inside a chunk, from 0 to ChunkSize-1
        private int3 ToChunkCoordinates(int x, int y, int z)
        {
            return new int3(x % WorldChunkV2.ChunkSize, y % WorldChunkV2.ChunkSize, z % WorldChunkV2.ChunkSize);
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
