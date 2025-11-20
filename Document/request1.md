# Procedural Grid Terrain System – Core & Module Architecture  
### Codex 구현을 위한 참조 문서

---

# 1. 시스템 개요

이 시스템은 **그리드 기반 절차적 지형 생성기**이며, 생성 과정을 **3단계 파이프라인**으로 구성한다.  
각 단계는 하나 이상의 **모듈(Module)**이 수행한다.

- **Core**
  - `GridContext`라는 공통 데이터 구조를 관리한다.
  - 모듈을 순서대로 호출하여 파이프라인을 실행한다.
  - 자체적으로 지형을 생성하지 않는다.
  - 최소 1개의 모듈이 없으면 동작할 수 없다.

- **Module**
  - 특정 단계(1~3)에서 `GridContext`를 읽고 수정하는 기능 단위.
  - 모듈은 “자신의 단계”에서만 수행된다.
  - 여러 모듈을 조합하여 다양한 프로젝트 요구에 대응할 수 있다.

---

# 2. 핵심 데이터 구조 – `GridContext`

모든 모듈이 입출력으로 사용하는 공통 데이터 객체이다.

## 2.1 GridMeta
- `width`, `height`
- `cellSize`
- `chunkSize` (선택)
- `origin`
- `coordinatePlane` (XY, XZ 등)

## 2.2 CellData
각 셀은 아래 레이어를 포함한다.
- **Terrain Layer:** `terrainType`, terrainNoise  
- **Usage Layer:** `usageMask` 또는 channel 방식  
- **Detail Layer:** `variantIndex`, `detailNoise`, `tags`

## 2.3 Seeds
- `globalSeed`
- `localSeed`

## 2.4 ConstraintsInfo  
3단계 모듈에서 사용할 제약 조건 정보.

---

# 3. 파이프라인 단계

```
1단계: Grid Shape Module  
→ 2단계: Generation Modules  
→ 3단계: Constraint Modules
```

---

# 4. 모듈 정의

## 4.1 1단계 – Grid Shape Module
### 역할
- 빈 그리드를 어떻게 구성할지 결정한다.
- GridMeta 설정 + 비어 있는 CellData 배열 생성.

### 출력
- 초기화된 `GridContext` (CellData는 비어 있음)

### 예시 모듈
- `OneDirectionalStripGridModule`
- `ChunkedGridModule`

---

## 4.2 2단계 – Generation Modules
여러 개 존재할 수 있으며, GridContext의 특정 레이어를 채운다.

### 4.2.1 Terrain Generation Module
- terrainType 채우기  
- 예: `PerlinTerrainModule`, `BiomeTerrainModule`

### 4.2.2 Usage/Traversal Generation Module
- usageMask / usageChannel 채우기  
- 예: `BasicTerrainToUsageModule`, `RacingTrackUsageModule`

### 4.2.3 Detail/Variant Module
- variantIndex, detailNoise 채우기  
- 예: `SimpleVariantModule`, `ClusteredDecorationModule`

---

## 4.3 3단계 – Constraint Modules
2단계 결과를 보정하거나 조건을 충족하도록 수정한다.

### 예시 모듈
- `EnsurePathBetweenPointsModule`
- `CheckpointIntervalModule`
- `ConnectivityCleanupModule`

---

# 5. Core 역할

- GridContext 생성 및 파이프라인 실행  
- 모듈 간 순서 보장  
- 모듈 단계 호환성 검사  
- 사용자가 선택한 모듈 조합대로 파이프라인 구성

---

# 6. 모듈 개발 규칙

1. 모듈은 반드시 특정 단계(1,2,3)에 속해야 한다.  
2. 입력은 항상 `GridContext` 하나이다.  
3. 출력도 동일한 `GridContext`여야 한다.  
4. 각 모듈은 자신이 담당하는 레이어만 수정한다.  
5. 3단계 모듈만 여러 레이어 수정 가능.  
6. 모듈은 순수 로직이어야 하며 GameObject 생성 금지.  

---

# 7. 요약

Core는 “빈 그리드를 만들고 모듈을 실행시키는 조정자”이며,  
모듈은 GridContext를 읽고 쓰는 형태로 특정 기능을 수행한다.

이 문서 기반으로 Codex는:
- GridContext 구조 정의  
- Core 파이프라인 구현  
- 단계별 모듈 인터페이스 설계  
- 기본 모듈 및 확장 모듈 구현  

을 순차적으로 진행할 수 있다.
