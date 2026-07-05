using Unity.Collections;
using UnityEngine;

namespace Utils
{
    public struct Array3D<T> where T : struct
    {
        private NativeArray<T> _data;
        public int XSize { get; private set; }
        public int YSize { get; private set; }
        public int ZSize { get; private set; }

        public Array3D(int xSize, int ySize, int zSize, Allocator allocator)
        {
            XSize = xSize;
            YSize = ySize;
            ZSize = zSize;

            _data = new NativeArray<T>(xSize * ySize * zSize, allocator);
        }

        private readonly int GetIndex(int x, int y, int z)
        {
            Debug.Assert(x >= 0 && x < XSize && y >= 0 && y < YSize && z >= 0 && z < ZSize);
            return x + y * XSize + z * XSize * YSize;
        }

        public T Get(int x, int y, int z)
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
