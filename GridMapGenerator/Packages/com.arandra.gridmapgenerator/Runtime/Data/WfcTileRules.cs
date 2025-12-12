using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GridMapGenerator.Data
{
    [CreateAssetMenu(fileName = "WfcTileRules", menuName = "Grid Map Generator/WFC Tile Rules")]
    public sealed class WfcTileRules : ScriptableObject
    {
        public List<WfcTileRule> Tiles = new();

        public bool TryGetJoker(out WfcTileRule joker)
        {
            joker = Tiles.FirstOrDefault(t => t != null && t.IsJoker);
            return joker != null;
        }
    }

    [Serializable]
    public sealed class WfcTileRule
    {
        public string TypeId;
        [Min(0f)]
        public float Weight = 1f;
        [Tooltip("참이면 모순 해소용 조커 타일. 일반 선택에서는 제외된다.")]
        public bool IsJoker;

        [Tooltip("좌(-X) 방향으로 허용되는 TypeId 목록. 비어 있으면 모든 이웃을 허용.")]
        public List<string> AllowedLeftNeighbors = new();
        [Tooltip("우(+X) 방향으로 허용되는 TypeId 목록. 비어 있으면 모든 이웃을 허용.")]
        public List<string> AllowedRightNeighbors = new();
        [Tooltip("앞(+Z) 방향으로 허용되는 TypeId 목록. 비어 있으면 모든 이웃을 허용.")]
        public List<string> AllowedForwardNeighbors = new();
        [Tooltip("뒤(-Z) 방향으로 허용되는 TypeId 목록. 비어 있으면 모든 이웃을 허용.")]
        public List<string> AllowedBackwardNeighbors = new();

        public bool AllowsNeighbor(string neighborTypeId, WfcDirection direction)
        {
            if (string.IsNullOrWhiteSpace(neighborTypeId))
            {
                return false;
            }

            var list = GetAllowedList(direction);
            if (list == null || list.Count == 0)
            {
                return true;
            }

            return list.Contains(neighborTypeId);
        }

        private List<string> GetAllowedList(WfcDirection direction)
        {
            return direction switch
            {
                WfcDirection.Left => AllowedLeftNeighbors,
                WfcDirection.Right => AllowedRightNeighbors,
                WfcDirection.Forward => AllowedForwardNeighbors,
                WfcDirection.Backward => AllowedBackwardNeighbors,
                _ => null
            };
        }
    }

    public enum WfcDirection
    {
        Left,
        Right,
        Forward,
        Backward
    }
}
