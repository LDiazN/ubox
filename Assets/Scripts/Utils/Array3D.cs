using Unity.Collections;
using UnityEngine;

namespace Utils
{
    public struct Array3D<T> where T : struct
    {
        private NativeArray<T> _data;
        private readonly int _xSize;
        private readonly int _ySize;
        private readonly int _zSize;

        public Array3D(int xSize, int ySize, int zSize, Allocator allocator)
        {
            _xSize = xSize;
            _ySize = ySize;
            _zSize = zSize;

            _data = new NativeArray<T>(xSize * ySize * zSize, allocator);
        }

        private readonly int GetIndex(int x, int y, int z)
        {
            Debug.Assert(x >= 0 && x < _xSize && y >= 0 && y < _ySize && z >= 0 && z < _zSize);
            return x + y * _xSize + z * _xSize * _ySize;
        }

        public readonly T Get(int x, int y, int z)
        {
            return _data[GetIndex(x, y, z)];
        }

        public void Set(int x, int y, int z, T value)
        {
            _data[GetIndex(x, y, z)] = value;
        }

        public void Dispose()
        {
            if (_data.IsCreated)
                _data.Dispose();
        }
    }
}
