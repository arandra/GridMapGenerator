# GridMapGenerator
Unity에서 그리드 기반 절차적 지형을 만드는 패키지(`com.arandra.gridmapgenerator`)와 이를 테스트할 샘플 프로젝트를 포함합니다.

## 레포 구조
- `GridMapGenerator/` : Unity 프로젝트 루트. 에디터로 열어 패키지 테스트 및 샘플 실행.
- `GridMapGenerator/Packages/com.arandra.gridmapgenerator/` : 패키지 원본 코드와 README.
- `Document/request1.md` : 파이프라인 설계 참고 문서.

## 사용자 관점 사용 단계
1) 설치  
   - Unity Package Manager에서 Git URL 또는 로컬 경로로 `Packages/com.arandra.gridmapgenerator`를 추가합니다.  
   - 동일 레포 내 테스트 시 `manifest.json`에 `com.arandra.gridmapgenerator`를 로컬 경로로 참조합니다.

2) 기본 설정  
   - `GridMeta`로 크기, 셀 크기, 좌표 평면 등 그리드 틀을 정의합니다.  
   - `Seeds`로 전역/로컬 시드를 정해 재현성을 확보합니다.  
   - 필요하면 `ConstraintsInfo`로 진입/탈출 지점, 경로 연결 요구 등을 지정합니다.

3) 파이프라인 구성  
   - 1단계 Shape 모듈 1개 이상을 등록해 빈 그리드를 만듭니다.  
   - 2단계 Generation 모듈로 지형, 용도, 디테일 레이어를 채웁니다. 여러 개 연결 가능.  
   - 3단계 Constraint 모듈로 경로 보정 같은 후처리를 적용합니다.
   - UI로 구성하고 싶다면 `Create > Grid Map Generator > Pipeline Profile` 에셋을 만들어 인스펙터에서 단계별 모듈 enum(Mask)을 설정하세요. FlatTerrain을 고르면 Scale도 입력합니다.

4) 실행 예시
```csharp
using GridMapGenerator;
using GridMapGenerator.Modules;

var meta = new GridMeta { Width = 32, Height = 16, CellSize = 1f, CoordinatePlane = CoordinatePlane.XZ };
var seeds = new Seeds { GlobalSeed = 1234, LocalSeed = 0 };
var constraints = new ConstraintsInfo { RequireConnectivity = true, EntryPoint = new(0, 0), ExitPoint = new(31, 15) };

var pipeline = new GridPipeline();
pipeline.RegisterModule(new OneDirectionalStripGridModule(meta, seeds, constraints));
pipeline.RegisterModule(new FlatTerrainModule());
pipeline.RegisterModule(new BasicUsageModule());
pipeline.RegisterModule(new SimpleVariantModule());
pipeline.RegisterModule(new ConnectivityConstraintModule());

GridContext grid = pipeline.Run(); // grid가 채워진 뒤 셀을 읽어 사용
```

5) 결과 활용  
- `grid`에서 `EnumerateCells()`로 셀 정보를 순회하거나, 좌표 인덱서로 직접 접근해 렌더링/게임플레이 데이터로 변환합니다.  
- 필요 시 Stage에 맞춘 커스텀 모듈을 구현해 등록합니다.

## Unity 프로젝트 열기
- `GridMapGenerator` 폴더를 Unity Hub에서 Open Project로 열면 샘플 씬과 설정을 바로 확인할 수 있습니다.

## 추가 문서
- 패키지 상세: `GridMapGenerator/Packages/com.arandra.gridmapgenerator/README.md`
