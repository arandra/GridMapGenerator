# Grid Map Generator

그리드 기반 절차적 지형 파이프라인을 제공하는 Unity 패키지입니다.  
`GridContext`를 중심으로 3단계(Shape → Generation → Constraint)의 모듈을 정의하고 Core 파이프라인이 이를 순서대로 실행합니다.

## 설치
- **Git:** Unity Package Manager에서 `Add package from git URL...`을 선택한 뒤 저장소 주소를 입력합니다.  
  서브 폴더를 참조하려면 `https://<repo>.git?path=/Packages/com.arandra.gridmapgenerator` 형식을 사용합니다.
- **로컬:** `Packages/manifest.json`에 `"com.arandra.gridmapgenerator": "file:../Packages/com.arandra.gridmapgenerator"`와 같이 기록하면 현재 프로젝트에서 개발 버전을 즉시 테스트할 수 있습니다.

## 구성 요소
- `GridContext` 및 관련 데이터 모델 (`Runtime/Data`)
- Core 파이프라인과 모듈 실행기 (`Runtime/Core`)
- 단계별 모듈 인터페이스와 기본 구현 예시 (`Runtime/Modules`)
- 에디터 테스트 설정 및 창 (`Runtime/Testing`, `Editor`)

## 빠른 시작
```csharp
var pipeline = new GridPipeline();
pipeline.RegisterModule(new OneDirectionalStripGridModule(new GridMeta(...)));
pipeline.RegisterModule(new FlatTerrainModule());
pipeline.RegisterModule(new BasicUsageModule());
pipeline.RegisterModule(new SimpleVariantModule());
pipeline.RegisterModule(new ConnectivityConstraintModule());

GridContext context = pipeline.Run();
```

모듈을 원하는 순서로 조합하여 다양한 그리드 지형을 생성할 수 있습니다.

## 모듈 단계 요약
- Shape(1단계): `OneDirectionalStripGridModule` — GridContext 생성 및 기본 셀 초기화.
- Generation(2단계):
  - `FlatTerrainModule` — Perlin 노이즈로 TerrainNoise 설정.
  - `BasicUsageModule` — Usage.IsBlocked를 전부 false로 초기화.
  - `SimpleVariantModule` — Detail Variant/Noise/Tags(`default`) 채우기.
  - `ScrollingCorridorModule` — N×M 물체가 지나갈 수 있는 코어 통로를 45도 이하 곡률로 생성하고, 좌우 대칭 여유 폭을 적용해 굴곡진 길을 만듦(통로 태그 `corridor` 추가).
- `TileAssignmentModule` — `TileAssignmentRules`+`TileSetData` 기반으로 비어 있는 `Terrain.TypeId`를 가중치로 배정.
  - `TileAssignmentRules`는 모듈 마스크별 규칙 + 조건부 규칙(Usage.IsBlocked)을 지원합니다. 조건부 규칙의 Override TypeId는 에디터에서 TileSetData 기반 팝업으로 선택할 수 있습니다.
- `WfcGenerationModule` — `WfcTileRules` 인접 규칙을 따라 Collapse, 모순 시 조커 타일로 해소.
- Constraint(3단계): `ConnectivityConstraintModule` — Entry/Exit/Checkpoint 연결 경로를 보장.

## 인스펙터에서 모듈 선택 (Pipeline Profile)
1. Project 뷰에서 `Create > Grid Map Generator > Pipeline Profile`을 선택해 프로필 에셋을 생성합니다.  
2. 인스펙터에서 GridMeta/Seeds/Constraints를 입력하고, Shape/Generation/Constraint 모듈을 enum(Mask)으로 선택합니다.  
   - ScrollingCorridor를 선택하면 Core Object Size(N=폭, M=높이), Minimum Core Width/Hold Rows, Max Lateral Step(0~1), Symmetric Margin Range, Margin Change Limit, Difficulty(0~1), Initial Center Offset을 설정합니다.  
   - Wfc를 선택하면 `WFC Tile Rules` 자산을 지정해야 합니다(조커 타일 포함).  
   - FlatTerrain을 포함하면 Scale 값을 설정합니다.  
3. 코드에서 프로필을 참조해 실행합니다:
```csharp
using GridMapGenerator.Core;
// 예: public GridPipelineProfile profile;
GridContext grid = profile.Run();
```
프로필을 씬 오브젝트나 컴포넌트에 할당하면 비개발자도 모듈 구성을 UI로 조정할 수 있습니다.

## 타일 배정/규칙 자산
- `TileSetData`: TypeId ↔ Prefab 매핑. TypeId/Prefab이 비어 있으면 무시됩니다.
- `TileAssignmentRules`: Generation 모듈 마스크별 가중치 기반 TypeId 배정 규칙. 기본 전략은 Weight 비례 추첨(Weight ≤ 0 제외).
- `WfcTileRules`: WFC 인접 허용 규칙(좌/우/앞/뒤 방향별)과 가중치. `IsJoker`=true인 타일은 모순 시에만 사용되도록 설계되어 있으므로 일반 Weight는 0 또는 매우 낮게 두는 것을 권장합니다.
- `WFC Rule Grid Editor`: 샘플 그리드로 규칙을 만들 때 자동 회전 생성(0/90/180/270) 옵션과 타일별 회전 허용 토글을 제공해 네 방향 입력 부담을 줄일 수 있습니다.

## 에디터 테스트 모드 (Test Runner)
1. `Create > Grid Map Generator > Test Settings`로 설정 자산을 만든 뒤, Pipeline Profile, TileSetData, TileAssignmentRules(필요 시 WfcTileRules)를 등록합니다.  
2. `Window > Grid Map Generator > Test Runner` 창을 열어 Test Settings를 지정하고 Root Object Name, Preview Size(폭/높이 0이면 무한 → Preview Size 사용), 시드를 입력/랜덤 선택합니다.  
3. “Generate”로 현재 씬에 프리팹을 배치합니다. 루트에 자식이 있으면 경고 후 삭제 여부를 묻고, “확인 및 다음에 보지 않음”을 선택하면 이후 경고를 생략합니다. 규칙 누락/WFC 규칙 미지정/Prefab 없는 TypeId 등은 세팅 단계에서 오류로 안내합니다.  
4. “Delete”는 루트 하위 자식만 삭제해 초기 상태를 복원합니다. 마지막 시드는 EditorPrefs에 저장되어 Unity 재시작 후에도 복원됩니다.
