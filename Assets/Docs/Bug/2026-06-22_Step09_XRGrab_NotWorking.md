# Bug Report: Step_09 Cube XR Grab 미동작

| 항목 | 내용 |
|------|------|
| **날짜** | 2026-06-22 |
| **씬** | `Assets/Scenes/Step_09.unity` |
| **대상** | `Cube` (XR Grab Interactable) |
| **증상** | Rigidbody, BoxCollider, XR Grab Interactable을 넣었는데 그립해도 잡히지 않음 |
| **상태** | 수정 완료 (Play Mode 검증 필요) |

---

## 요약

Cube에 Grab 관련 컴포넌트는 대부분 있었으나, **씬/오브젝트 단위 상호작용 파이프라인이 끊겨 있었음**.  
AttachPoint 부재는 원인이 아님. **Interaction Manager, Direct Interactor, Grab Collider 등록 오류**가 핵심 원인.

---

## 재현 조건

1. `Step_09` 씬 열기
2. Play Mode 진입 (Quest 또는 XR Device Simulator)
3. 컨트롤러를 Cube `(0.10, 1.04, 0.50)` 근처로 이동
4. 그립(Select) 입력

### 기대 결과

Cube가 손/컨트롤러에 붙어 이동함.

### 실제 결과 (수정 전)

- Hover는 간헐적으로 되나 **Select(잡기) 안 됨**
- 또는 아예 상호작용 대상으로 인식되지 않음

---

## Cube 계층 (수정 당시)

```
Cube
├─ XR Grab Interactable
├─ Rigidbody (kinematic, useGravity=false)
├─ XR General Grab Transformer
├─ AttachPoint (+ AttachPointGizmo)   ← 잡기 필수 아님
└─ COL (+ BoxCollider)
```

---

## 원인 분석

### 1. 씬에 XR Interaction Manager 없음 (초기)

- XRI는 Manager가 없으면 Interactor ↔ Interactable 연결 불가
- 이후 씬에 `XR Interaction Manager` 추가됨

### 2. 컨트롤러에 Grab용 Interactor 없음 (초기)

- `Left Controller` / `Right Controller`에 `TrackedPoseDriver`만 존재
- 이후 `Left_NearFarInteractor` / `Right_NearFarInteractor` 추가

### 3. Direct Interactor 없음 (핵심)

- NearFar만 있으면 **레이/원거리 + 근거리 Near 영역** 방식이라 물리적 “손 닿아서 잡기”에 불안정
- Play Mode 프로브: Hover는 되나 `hasSelection: false` — **감지 vs Select 입력** 문제
- **Direct Interactor** (SphereCollider 트리거) 추가 후 NearFar 비활성화가 올바른 패턴

### 4. XR Grab Interactable Collider 목록에 `null` 항목

Play Mode 진단 결과:

```
grabColliders: ["NULL", "COL enabled=True"]
```

씬 YAML (수정 전):

```yaml
m_Colliders:
- {fileID: 0}      # ← null 참조
- {fileID: 687180063}  # COL
```

- `COL` 자식 BoxCollider는 **존재했음**
- Grab 컴포넌트에 **빈 슬롯이 같이 등록**되어 상호작용 검증 실패 가능

### 5. Grab ↔ Interaction Manager 미연결

- Cube `XR Grab Interactable`의 `m_InteractionManager`가 `{fileID: 0}` (비어 있음)
- 런타임 자동 연결되는 경우도 있으나, 씬 저장 상태 기준으로는 미연결

### 6. 오해: AttachPoint 없어서 못 잡는다?

- **아님.** AttachPoint는 잡힌 뒤 **attach transform** (손에 붙는 위치)만 지정
- 수동 `SelectEnter` 테스트 시 grab 메커니즘 자체는 정상 동작 확인됨

### 7. 부가 요인: Cube scale 0.1

- 월드 크기 약 10cm — Direct Interactor 반경·손 위치에 따라 잡기 어려움
- Direct Interactor Sphere `radius`를 **0.15**로 확대해 완화

### 8. 개발 중 부수 이슈: 스크립트 순환 참조

- `XrGrabInteractionUtility` ↔ `XrGrabRuntimeBootstrap` 상호 참조 → `CS0246` 컴파일 에러
- 수동 생성 `.meta` guid 불일치 가능
- **해결:** 순환 참조 제거, Unity가 `.meta` 재생성, 씬 guid 갱신

---

## 수정 내용

### 씬 / 런타임

| 조치 | 설명 |
|------|------|
| XR Interaction Manager | 씬에 추가 및 Interactor/Interactable 연결 |
| Direct Interactor | 좌/우 컨트롤러에 추가, NearFar Select 입력 복사 |
| NearFar Interactor | 비활성화 (Direct 우선) |
| Grab Collider | null 항목 제거, `COL`만 등록 |
| Interaction Manager 참조 | Cube Grab에 Manager 연결 |
| `XrGrabRuntimeBootstrap` | `XR Origin (VR)`에 부착 — Play 시 자동 보정 |

### 스크립트 / 에디터

| 파일 | 역할 |
|------|------|
| `Assets/Scripts/HandPose/XrGrabInteractionUtility.cs` | 씬 Grab 일괄 설정 (Manager, Direct, Collider) |
| `Assets/Scripts/HandPose/XrGrabRuntimeBootstrap.cs` | Play 시작 시 collider/interactor 자동 보정 |
| `Assets/Scripts/HandPose/GrabbableObjectUtility.cs` | Cube `COL`·AttachPoint·Grab 참조 설정 |
| `Assets/Editor/XrGrabInteractionSetup.cs` | 메뉴: `Tools > Hand Pose > Setup Scene For XR Grab` |
| `Assets/Editor/GrabbableObjectSetup.cs` | 메뉴: `Tools > Hand Pose > Setup Grabbable Attach Point` |

### 에디터 메뉴 (재발 시)

1. `Tools > Hand Pose > Setup Scene For XR Grab`
2. Cube 선택 후 `Tools > Hand Pose > Setup Grabbable Attach Point`
3. 씬 저장

---

## 검증 방법

1. Play Mode **종료** 후 스크립트 컴파일 에러 없는지 확인
2. Play 진입
3. Hierarchy 확인:
   - `Left/Right Controller/Direct Interactor` **활성**
   - `Left_NearFarInteractor` / `Right_NearFarInteractor` **비활성**
4. Cube Inspector:
   - `XR Grab Interactable > Colliders`에 **COL만** (null 없음)
5. 손을 Cube에 가까이 대고 **그립(Select)**
6. (선택) Play Mode에서 `interactionManager.SelectEnter(direct, grab)` 수동 호출 시 잡히면 → 입력/거리 문제, 안 잡히면 → Collider/레이어 문제

---

## 체크리스트 (새 Grabbable 오브젝트 추가 시)

- [ ] 씬에 `XR Interaction Manager` 존재
- [ ] 컨트롤러에 `Direct Interactor` (+ SphereCollider trigger)
- [ ] `XR Grab Interactable` Colliders에 **유효한 Collider만** (null 없음)
- [ ] Collider가 Grab 오브젝트 또는 `COL` 자식에 있음
- [ ] Interaction Layer 양쪽 일치 (기본 bit 1)
- [ ] `InputActionManager` + XRI Default Input Actions 활성
- [ ] AttachPoint — **선택** (잡기 필수 아님)

---

## 관련 메모

- 프로젝트 손 Attach Point (`HandAttachPoint` Wrist / IndexNear)는 **손 pose·시각화**용이며, XRI Grab 필수 요소 아님
- 오브젝트 AttachPoint (`AttachPointGizmo`)는 잡힌 뒤 정렬 위치용

---

## 참고 파일

- `Assets/Scenes/Step_09.unity`
- `VRHandPoseSystemNotes.md` (Hand Pose / Attach Point 설계 메모)
