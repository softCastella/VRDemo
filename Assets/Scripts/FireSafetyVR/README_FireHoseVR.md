# Fire Hose VR Prototype (Step_10)

소화전 / 호스 / 물 분사 / 소화 판정 1차 프로토타입입니다.  
네임스페이스: `FireSafetyVR`

## 씬 구성

`Assets/Scenes/Step_10.unity` — Step_09 기반 XR Origin + `FireHydrantSystem` 계층

```
FireHydrantSystem
├── FireHydrantBox
│   ├── Cube_Back
│   ├── Cube_Door
│   ├── Cylinder_Reel
│   └── Valve          (XRSimpleInteractable + FireHydrantValveInteractable)
├── HoseStartPoint
├── HoseRenderer       (LineRenderer + SimpleFireHose)
├── Nozzle             (XRGrabInteractable + FireHoseNozzle + FireHoseNozzleTriggerBridge)
│   ├── Cylinder_Body
│   ├── Cylinder_Grip
│   ├── Cone_Tip
│   ├── ShootPoint
│   └── WaterParticle
└── (FireHydrantSystem 컴포넌트)

FireTarget             (불 + FireTarget + ParticleSystem)
```

## 스크립트 위치

`Assets/Scripts/FireSafetyVR/`

| 스크립트 | 역할 |
|---------|------|
| `SimpleFireHose.cs` | LineRenderer 호스 + 처짐 |
| `FireHoseNozzle.cs` | 물 분사 + Raycast 소화 |
| `FireTarget.cs` | 불 HP |
| `FireHydrantSystem.cs` | 밸브 / 트리거 통합 |
| `FireHoseNozzleTriggerBridge.cs` | 노즐 잡은 채 XR 트리거 → 분사 |
| `FireHydrantValveInteractable.cs` | 밸브 Select → ToggleValve |
| `HoseBuilder.cs` | (2차) 물리 세그먼트 체인 |
| `HosePointRenderer.cs` | (2차) 세그먼트 → LineRenderer |

## Inspector 연결

### FireHydrantSystem
- **Nozzle** → `Nozzle` 오브젝트의 `FireHoseNozzle`

### HoseRenderer (SimpleFireHose)
- **Hose Start** → `HoseStartPoint`
- **Nozzle** → `Nozzle`
- **Line Renderer** → 자기 자신
- Segment Count: 24, Sag Amount: 0.3

### Nozzle
- **Rigidbody**: Mass 0.5, Drag 0.2, Interpolate, Continuous Dynamic
- **XR Grab Interactable**: Velocity Tracking, Throw On Detach false
- **FireHoseNozzle**: Shoot Point, Water Particle, Range 8, Extinguish Power 10
- **FireHoseNozzleTriggerBridge**: System → FireHydrantSystem

### Valve
- **XRSimpleInteractable** + **FireHydrantValveInteractable**

### FireTarget
- Collider 필수 (Raycast 히트용)
- Fire Particle / Fire Visual Root 연결

## XR 이벤트 (수동 연결 시)

| 동작 | 호출 |
|------|------|
| 밸브 토글 | `FireHydrantSystem.ToggleValve()` |
| 트리거 누름 | `FireHydrantSystem.SetTriggerPressed(true)` |
| 트리거 뗌 | `FireHydrantSystem.SetTriggerPressed(false)` |

노즐은 `FireHoseNozzleTriggerBridge`가 자동으로 트리거를 연결합니다.

## 1차 테스트 순서

1. `Step_10` 씬 열기 → Play (VR 또는 Link)
2. 노즐 Grab → 당기면 LineRenderer 호스 따라옴
3. 밸브 닫힘: 트리거 눌러도 물 안 나옴
4. Valve Select → 밸브 열림
5. 노즐 잡고 트리거 → 물 Particle
6. FireTarget 조준 → HP 감소 → 소화 완료

## 2차 물리 호스

1. `HoseBuilder`를 `FireHydrantSystem`에 추가 (기본 비활성)
2. `Build On Start` 체크, Start Anchor / Nozzle Rigidbody 연결
3. `HosePointRenderer`에 세그먼트 Transform 리스트 연결
4. `SimpleFireHose`는 유지하거나 `HosePointRenderer`만 사용

## Quest 2 성능

- LineRenderer 우선 (물리 호스 20~30 세그먼트 이하)
- Raycast는 분사 중만 (`FireHoseNozzle.Update`)
- FixedUpdate 미사용
- 실시간 Tube Mesh / 유체 시뮬 금지
