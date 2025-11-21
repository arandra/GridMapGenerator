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
