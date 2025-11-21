using System;
using System.Collections.Generic;
using UnityEngine;

namespace Codex.GridMapGenerator
{
    [Serializable]
    public enum CoordinatePlane
    {
        XY,
        XZ
    }

    [Serializable]
    public struct GridMeta
    {
        public int Width;
        public int Height;
        public float CellSize;
        public Vector3 Origin;
        public CoordinatePlane CoordinatePlane;
        public int? ChunkSize;

        public readonly int CellCount => Mathf.Max(0, Width) * Mathf.Max(0, Height);
    }

    [Serializable]
    public struct Seeds
    {
        public int GlobalSeed;
        public int LocalSeed;

        public readonly System.Random CreateGlobalRandom() => new(GlobalSeed);
        public readonly System.Random CreateLocalRandom() => new(LocalSeed == 0 ? GlobalSeed : LocalSeed);
    }

    [Serializable]
    public class ConstraintsInfo
    {
        public bool RequireConnectivity;
        public Vector2Int EntryPoint;
        public Vector2Int ExitPoint;
        public List<Vector2Int> Checkpoints = new();
    }

    [Serializable]
    public class GridContext
    {
        private readonly CellData[] _cells;

        public GridMeta Meta { get; }
        public Seeds Seeds { get; }
        public ConstraintsInfo Constraints { get; }

        public GridContext(GridMeta meta, Seeds seeds, ConstraintsInfo constraints)
        {
            if (meta.Width <= 0 || meta.Height <= 0)
            {
                throw new ArgumentException("GridMeta must define a positive width and height.");
            }

            Meta = meta;
            Seeds = seeds;
            Constraints = constraints ?? new ConstraintsInfo();

            _cells = new CellData[meta.CellCount];
            for (var i = 0; i < _cells.Length; i++)
            {
                _cells[i] = new CellData();
            }
        }

        public int CellCount => _cells.Length;

        public CellData this[int x, int y]
        {
            get => _cells[ToIndex(x, y)];
            set => _cells[ToIndex(x, y)] = value;
        }

        public CellData this[int index]
        {
            get => _cells[index];
            set => _cells[index] = value;
        }

        public IEnumerable<(Vector2Int coordinates, CellData cell)> EnumerateCells()
        {
            for (var y = 0; y < Meta.Height; y++)
            {
                for (var x = 0; x < Meta.Width; x++)
                {
                    var index = ToIndex(x, y);
                    yield return (new Vector2Int(x, y), _cells[index]);
                }
            }
        }

        public bool TryGetCell(int x, int y, out CellData cell)
        {
            if (IsOutOfBounds(x, y))
            {
                cell = null;
                return false;
            }

            cell = _cells[ToIndex(x, y)];
            return true;
        }

        public bool IsOutOfBounds(int x, int y)
        {
            return x < 0 || x >= Meta.Width || y < 0 || y >= Meta.Height;
        }

        public int ToIndex(int x, int y)
        {
            if (IsOutOfBounds(x, y))
            {
                throw new IndexOutOfRangeException($"({x}, {y}) is outside the grid.");
            }

            return (y * Meta.Width) + x;
        }
    }

    [Serializable]
    public class CellData
    {
        public TerrainLayer Terrain = new();
        public UsageLayer Usage = new();
        public DetailLayer Detail = new();
    }

    [Serializable]
    public struct TerrainLayer
    {
        public TerrainType TerrainType;
        public float TerrainNoise;
    }

    [Flags]
    public enum UsageChannel
    {
        None = 0,
        Walkable = 1 << 0,
        Driveable = 1 << 1,
        Blocking = 1 << 2
    }

    [Serializable]
    public struct UsageLayer
    {
        public UsageChannel Channels;

        public readonly bool Contains(UsageChannel channel) => (Channels & channel) == channel;

        public void AddChannel(UsageChannel channel) => Channels |= channel;
    }

    [Serializable]
    public struct DetailLayer
    {
        public int VariantIndex;
        public float DetailNoise;
        public List<string> Tags;

        public void EnsureTags()
        {
            Tags ??= new List<string>();
        }
    }

    public enum TerrainType
    {
        Unknown = 0,
        Plain = 1,
        Hill = 2,
        Mountain = 3,
        Water = 4
    }
}
