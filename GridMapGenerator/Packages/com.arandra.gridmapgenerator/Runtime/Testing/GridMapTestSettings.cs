using System;
using System.Collections.Generic;
using GridMapGenerator.Core;
using GridMapGenerator.Data;
using UnityEngine;

namespace GridMapGenerator.Testing
{
    /// <summary>
    /// 에디터 테스트용 설정: 파이프라인, 프리뷰 크기, 타일-프리팹 매핑을 정의한다.
    /// </summary>
    [CreateAssetMenu(fileName = "GridMapTestSettings", menuName = "Grid Map Generator/Test Settings")]
    public sealed class GridMapTestSettings : ScriptableObject
    {
        [Header("Pipeline")]
        public GridPipelineProfile PipelineProfile;

        [Tooltip("생성 결과를 배치할 루트 게임오브젝트 이름. 존재하지 않으면 자동 생성한다.")]
        public string RootObjectName = "GridMapRoot";

        [Tooltip("GridMeta가 무한(폭/높이 0)일 때 사용할 프리뷰 크기. 0이면 무한으로 간주되므로 반드시 1 이상으로 설정하세요.")]
        public Vector2Int PreviewSize = new(16, 16);

        [Header("Tiles")]
        public TileSetData TileSet;
    }
}
