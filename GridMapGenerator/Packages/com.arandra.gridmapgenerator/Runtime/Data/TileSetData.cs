using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace GridMapGenerator.Data
{
    [CreateAssetMenu(fileName = "TileSetData", menuName = "Grid Map Generator/Tile Set Data")]
    public sealed class TileSetData : ScriptableObject
    {
        public List<TilePrefabBinding> Tiles = new();
    }

    [Serializable]
    public sealed class TilePrefabBinding
    {
        [FormerlySerializedAs("Id")]
        [FormerlySerializedAs("TerrainType")]
        public string TypeId = "Tile";
        public GameObject Prefab;
    }
}
