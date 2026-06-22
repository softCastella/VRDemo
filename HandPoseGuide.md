# VR 손 포즈 / 고스트 핸드 시스템 정리

이 문서는 VR 화학물질 안전 교육 프로젝트에서 사용하는 **손 포즈**, **고스트 핸드**, **AttachPoint**, **애니메이션 클립**, **ScriptableObject** 개념을 정리한 문서이다.

목표는 다음과 같다.

- 소화기, 화학물질 병, 버튼 같은 물체를 자연스럽게 잡거나 누르게 만들기
- 물체마다 미리 정해둔 손 모양을 적용하기
- 잡을 때는 물체 전용 손 포즈가 나오고, 놓으면 기본 손 애니메이션으로 돌아오게 하기

---

## 1. 전체 개념

선생님이 말한 흐름은 다음과 같다.

```text
1. 핸드 비주얼라이저 포징
2. 클립으로 저장
3. 프리팹화
4. 물체에 고스트 핸드와 AttachPoint를 붙임
5. 잡으면 실제 손이 저장된 포즈로 바뀜
6. 놓으면 기본 손 애니메이션으로 복귀
```

쉽게 말하면:

> 물체마다 "이 물체는 이렇게 잡아야 한다"는 손 모양을 미리 저장해두고, VR에서 실제로 잡을 때 그 손 모양을 적용하는 시스템이다.

예를 들어 소화기를 잡을 때 기본 주먹 애니메이션만 쓰면 손가락이 소화기 손잡이에 정확히 맞지 않을 수 있다.

그래서 소화기 전용 손 포즈를 미리 만들고, 소화기를 잡는 순간 실제 손 모델이 그 포즈로 바뀌게 한다.

---

## 2. 고스트 핸드란?

고스트 핸드는 실제 플레이어 손이 아니라, 물체에 미리 붙여두는 **손 모양 미리보기 모델**이다.

예시:

```text
FireExtinguisher
├─ Mesh
├─ Collider
├─ XR Grab Interactable
├─ AttachPoint
└─ GhostHand
```

`GhostHand`는 "이 물체를 잡으면 손이 이런 위치와 모양이 되어야 한다"는 기준 역할을 한다.

중요한 점:

```text
고스트 핸드 자체 = 애니메이션 클립
```

은 아니다.

더 정확히는:

```text
고스트 핸드 = 손 모델 / 프리팹
클립 = 그 손 모델의 손가락 포즈 데이터
```

즉, 고스트 핸드는 손 모델이고, 그 손 모델에 적용된 자세 정보가 애니메이션 클립 또는 포즈 데이터라고 이해하면 된다.

---

## 3. 애니메이션 클립이란?

애니메이션 클립은 손가락 뼈들이 어떤 각도로 굽어 있는지를 저장한 데이터이다.

예를 들어:

```text
HandPose_FireExtinguisher.anim
HandPose_ChemicalBottle.anim
HandPose_ButtonPress.anim
```

같은 클립을 만들 수 있다.

각 클립에는 다음과 같은 정보가 들어갈 수 있다.

```text
엄지 뼈 회전값
검지 뼈 회전값
중지 뼈 회전값
약지 뼈 회전값
새끼손가락 뼈 회전값
```

즉, 클립은 "손 모델"이 아니라 "손 모델을 어떤 자세로 만들지 저장한 파일"이다.

---

## 4. AttachPoint란?

`AttachPoint`는 물체를 잡았을 때 손이 붙을 위치이다.

예를 들어 소화기를 잡는다면 손이 소화기 몸통 아무 곳에나 붙으면 안 된다. 손잡이에 붙어야 자연스럽다.

그래서 물체 안에 빈 GameObject를 만들고 그 위치를 손목 기준점으로 사용한다.

예시:

```text
FireExtinguisher
└─ AttachPoint
```

AttachPoint는 보통 다음 정보를 가진다.

```text
손목 위치
손목 회전
```

즉:

```text
AttachPoint = 손목이 붙을 위치와 방향
GhostHand = 손이 어떻게 보일지 미리 보여주는 모델
HandPose = 손가락이 어떻게 굽을지 저장한 데이터
```

---

## 5. 손목 위치와 손가락 변화를 함께 저장해야 하는 이유

선생님이 말한 "손목 위치 맞추고 손의 구체적 변화를 저장해야 한다"는 말은 매우 중요하다.

손 포즈를 만들 때는 두 가지를 모두 저장해야 한다.

```text
1. 손목 위치 / 회전
2. 손가락 뼈들의 구체적인 회전값
```

손가락 모양만 저장하면 문제가 생길 수 있다.

```text
손가락 모양만 저장
→ 손가락은 잘 굽어 있음
→ 하지만 손 전체 위치가 물체와 어긋날 수 있음
```

그래서 손목 위치까지 맞춰야 한다.

```text
손목 위치 + 손가락 포즈 저장
→ 손 전체가 물체의 잡는 위치에 맞음
→ 손가락도 물체를 자연스럽게 감쌈
```

예를 들어 소화기 전용 HandPose 데이터는 이런 느낌이다.

```text
HandPose_FireExtinguisher
├─ Wrist position offset
├─ Wrist rotation offset
├─ Thumb bone rotations
├─ Index bone rotations
├─ Middle bone rotations
├─ Ring bone rotations
└─ Pinky bone rotations
```

---

## 6. ScriptableObject를 사용하는 이유

ScriptableObject는 Unity에서 데이터를 파일처럼 저장하는 방식이다.

손 포즈를 ScriptableObject로 만들면 물체마다 손 포즈를 쉽게 연결할 수 있다.

예시:

```text
HandPose_FireExtinguisher.asset
HandPose_ChemicalBottle.asset
HandPose_Beaker.asset
HandPose_ButtonPress.asset
```

각 파일에는 다음 정보가 들어갈 수 있다.

```text
손목 위치 보정값
손목 회전 보정값
각 손가락 뼈의 회전값
고스트 핸드 프리팹 참조
AttachPoint 관련 정보
```

그러면 물체 프리팹에는 이런 식으로 연결할 수 있다.

```text
FireExtinguisher
├─ XR Grab Interactable
├─ AttachPoint
├─ GhostHand
└─ HandPose_FireExtinguisher
```

---

## 7. 플레이 중 동작 흐름

### 평상시

플레이어의 실제 VR 손은 컨트롤러를 따라다닌다.

```text
컨트롤러 이동
→ 손 모델 이동

그립 버튼 누름
→ 기본 손 그립 애니메이션 재생
```

이때는 기본 손 애니메이션이 작동한다.

---

### 물체를 잡을 때

예를 들어 플레이어가 소화기를 잡으면:

```text
소화기 Grab
→ 소화기에 연결된 HandPose_FireExtinguisher 찾기
→ 손목을 AttachPoint 위치와 회전에 맞춤
→ 기본 그립 애니메이션을 잠시 끔
→ 손가락을 저장된 소화기 전용 포즈로 변경
```

여기서 "기존 애니메이션을 없앤다"는 말은 보통 완전히 삭제한다는 뜻이 아니다.

정확히는:

```text
잡고 있는 동안 기본 손 애니메이션을 잠시 비활성화하고,
물체 전용 손 포즈를 우선 적용한다.
```

---

### 물체를 놓을 때

물체를 놓으면:

```text
Release
→ 물체 전용 손 포즈 해제
→ 손목이 다시 컨트롤러를 따라감
→ 손가락이 다시 기본 손 애니메이션을 따라감
```

즉, 놓으면 원래 손 애니메이션으로 돌아와야 한다.

---

## 8. 핵심 정리

```text
고스트 핸드
= 물체에 붙어 있는 손 모양 미리보기 모델

애니메이션 클립
= 손가락 뼈들이 어떤 각도로 굽어 있는지 저장한 데이터

AttachPoint
= 물체를 잡았을 때 손목이 붙을 위치와 방향

HandPose ScriptableObject
= 손목 위치, 손목 회전, 손가락 포즈를 저장하는 데이터 파일

기본 손 애니메이션
= 평소 컨트롤러 입력에 따라 손이 움직이는 애니메이션

물체 전용 손 포즈
= 특정 물체를 잡을 때만 적용되는 고정 손 모양
```

---

## 9. 최종 목표 구조

잡을 수 있는 물체는 대략 이런 구조를 가지면 된다.

```text
GrabObject
├─ Mesh
├─ Collider
├─ XR Grab Interactable
├─ AttachPoint
├─ GhostHand
└─ HandPose 데이터
```

그리고 손 시스템은 이렇게 동작해야 한다.

```text
평상시
→ 기본 VR 손 애니메이션

물체 잡음
→ AttachPoint에 손목 위치 맞춤
→ 물체 전용 HandPose 적용
→ 기본 손 애니메이션 잠시 끔

물체 놓음
→ HandPose 해제
→ 기본 VR 손 애니메이션 복귀
```

---

## 10. 한 문장으로 요약

> 물체마다 손목 위치와 손가락 포즈를 미리 저장해두고, 플레이어가 그 물체를 잡으면 실제 손이 그 저장된 포즈로 바뀌며, 놓으면 다시 기본 손 애니메이션으로 돌아오는 시스템이다.
