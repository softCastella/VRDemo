# Unity Editor Hand Pose Tool 구현 요청 프롬프트

아래 프롬프트는 VR 화학물질 안전 교육 프로젝트에서 사용할 **Unity Editor용 손 포즈 저장 및 고스트 핸드 생성 툴**을 구현 요청할 때 사용한다.

---

## 구현 요청 프롬프트

다음 요구사항을 만족하는 Unity Editor 툴과 런타임 손 포즈 시스템을 구현해줘.

프로젝트는 VR 화학물질 안전 교육용 Unity 프로젝트이다. 플레이어는 VR 손으로 소화기, 화학물질 병, 비커, 버튼 등의 물체를 잡거나 누른다. 각 물체를 잡을 때 기본 그립 애니메이션만 사용하는 것이 아니라, 물체마다 미리 저장해둔 손 포즈가 실제 손에 적용되어야 한다.

핵심 목표는 다음과 같다.

```text
Unity Editor에서 손 모델을 원하는 모양으로 포징한다.
버튼을 누르면 현재 손목 위치와 손가락 포즈가 HandPose 데이터로 저장된다.
동시에 선택한 물체에 AttachPoint와 GhostHand가 자동으로 생성된다.
물체 프리팹에는 저장된 HandPose 데이터가 연결된다.
플레이 중 물체를 잡으면 실제 손이 저장된 포즈로 바뀐다.
물체를 놓으면 다시 기존 VR 손 애니메이션으로 돌아간다.
```

---

## 구현해야 할 주요 기능

### 1. HandPose ScriptableObject

손 포즈 데이터를 저장할 `ScriptableObject`를 만들어줘.

저장해야 할 정보:

```text
포즈 이름
손 종류: Left / Right
손목 위치 offset
손목 회전 offset
손가락 뼈들의 localRotation 값
뼈 이름 또는 Transform 경로
GhostHand 프리팹 참조 또는 생성된 GhostHand 정보
```

요구사항:

```text
손가락 뼈 데이터는 배열 또는 리스트로 저장한다.
각 뼈는 이름/path와 localRotation을 함께 저장한다.
나중에 실제 손 모델에 같은 뼈 이름/path를 찾아 회전값을 적용할 수 있어야 한다.
손목 위치와 회전은 AttachPoint 기준으로 사용할 수 있어야 한다.
```

예상 파일:

```text
Assets/Scripts/HandPose/HandPoseData.cs
```

---

### 2. 잡을 물체에 붙는 컴포넌트

잡을 수 있는 물체에 붙일 컴포넌트를 만들어줘.

예상 이름:

```text
HandPoseInteractable.cs
```

역할:

```text
이 물체가 사용할 HandPoseData 참조
AttachPoint Transform 참조
GhostHand GameObject 참조
왼손/오른손 포즈 구분
잡을 때 손 포즈 적용에 필요한 데이터 제공
```

예상 구조:

```text
GrabObject
├─ Mesh
├─ Collider
├─ XR Grab Interactable
├─ AttachPoint
├─ GhostHand
└─ HandPoseInteractable
```

---

### 3. 실제 손에 포즈를 적용하는 런타임 컴포넌트

실제 플레이어 손에 붙는 컴포넌트를 만들어줘.

예상 이름:

```text
HandPoseApplier.cs
```

역할:

```text
평소에는 기존 VR 손 애니메이션을 사용한다.
물체를 잡으면 기존 손 애니메이션을 잠시 비활성화한다.
잡은 물체의 HandPoseData를 읽는다.
손목을 AttachPoint 위치와 회전에 맞춘다.
손가락 뼈들의 localRotation을 저장된 값으로 변경한다.
물체를 놓으면 HandPose 적용을 해제한다.
기존 VR 손 애니메이션을 다시 활성화한다.
```

중요:

```text
"기존 애니메이션을 없앤다"는 뜻은 삭제가 아니다.
잡고 있는 동안 기본 그립 애니메이션보다 저장된 HandPose를 우선 적용한다는 뜻이다.
놓으면 반드시 원래 애니메이션으로 복귀해야 한다.
```

---

### 4. Unity EditorWindow 툴

Unity Editor에서 사용할 커스텀 툴 창을 만들어줘.

예상 이름:

```text
HandPoseEditorWindow.cs
```

메뉴 위치:

```text
Tools/VR Hand Pose Tool
```

툴 창에서 선택하거나 입력할 항목:

```text
포징된 손 모델 Transform
손목 기준 Transform
저장 대상 물체 GameObject
GhostHand로 복제할 손 모델
저장할 HandPose 이름
왼손/오른손 선택
저장 경로
```

툴 창에 필요한 버튼:

```text
[현재 손 포즈 저장]
[GhostHand 생성]
[AttachPoint 생성]
[포즈 저장 + GhostHand 생성 + 물체에 연결]
```

가장 중요한 버튼은 다음 하나이다.

```text
[포즈 저장 + GhostHand 생성 + 물체에 연결]
```

이 버튼을 누르면 아래 작업이 자동으로 실행되어야 한다.

```text
1. 현재 포징된 손 모델의 손목 위치와 회전을 읽는다.
2. 손가락 뼈들의 localRotation 값을 읽는다.
3. HandPoseData ScriptableObject를 생성하고 저장한다.
4. 선택된 물체 아래에 AttachPoint가 없으면 생성한다.
5. AttachPoint의 위치와 회전을 현재 손목 위치 기준으로 맞춘다.
6. 선택된 물체 아래에 GhostHand를 생성한다.
7. GhostHand에는 현재 포징된 손 모델의 복제본을 사용한다.
8. GhostHand는 기본적으로 반투명하거나 비활성화 상태로 둘 수 있다.
9. 선택된 물체에 HandPoseInteractable 컴포넌트를 추가한다.
10. HandPoseInteractable에 HandPoseData, AttachPoint, GhostHand 참조를 자동으로 연결한다.
11. 변경된 물체가 프리팹이면 프리팹 변경사항을 저장한다.
```

---

## GhostHand 생성 규칙

GhostHand는 클립 자체가 아니라, 저장된 포즈를 미리 보여주는 손 모델이다.

요구사항:

```text
GhostHand는 선택한 물체의 자식으로 생성한다.
이름은 GhostHand_Left 또는 GhostHand_Right 형태로 한다.
GhostHand의 위치와 회전은 AttachPoint 기준에 맞춘다.
GhostHand는 현재 포징된 손 모델과 같은 손가락 자세를 가져야 한다.
GhostHand는 플레이 중에는 필요에 따라 숨길 수 있어야 한다.
```

선택 사항:

```text
GhostHand의 Renderer 머티리얼을 반투명 머티리얼로 바꿀 수 있으면 좋다.
반투명 처리가 복잡하면 일단 GameObject를 비활성화 상태로 생성해도 된다.
```

---

## AttachPoint 생성 규칙

AttachPoint는 물체를 잡았을 때 손목이 붙을 위치이다.

요구사항:

```text
AttachPoint는 선택한 물체의 자식으로 생성한다.
이름은 AttachPoint_Left 또는 AttachPoint_Right 형태로 한다.
현재 포징된 손목 위치와 회전을 기준으로 생성한다.
런타임에서는 실제 손목 Transform이 AttachPoint 위치와 회전에 맞춰져야 한다.
```

---

## 런타임 동작 흐름

### 평상시

```text
실제 VR 손은 컨트롤러를 따라간다.
그립 버튼에 따라 기본 손 애니메이션이 재생된다.
```

### 물체를 잡을 때

```text
XR Grab Interactable 또는 잡기 이벤트 발생
잡은 물체의 HandPoseInteractable을 찾음
HandPoseData를 읽음
기존 손 Animator 또는 기본 손 애니메이션을 잠시 비활성화
손목을 AttachPoint 위치와 회전에 맞춤
손가락 뼈 localRotation을 HandPoseData 값으로 적용
```

### 물체를 놓을 때

```text
잡기 해제 이벤트 발생
HandPose 적용 해제
손목이 다시 컨트롤러를 따라가게 함
기존 손 Animator 또는 기본 손 애니메이션을 다시 활성화
```

---

## 코드 작성 시 주의사항

```text
기존 Hand.cs가 있다면 먼저 구조를 읽고 기존 방식을 최대한 유지한다.
XR Interaction Toolkit을 사용 중이면 XRGrabInteractable의 selectEntered/selectExited 이벤트와 연결한다.
기존 손 애니메이션 Animator가 있다면 삭제하지 말고 enable/disable 또는 weight 조절 방식으로 처리한다.
뼈를 찾을 때는 단순 이름 검색보다 Transform 경로 저장 방식을 우선 고려한다.
왼손/오른손을 구분할 수 있게 만든다.
에디터 스크립트는 Assets/Editor 또는 Editor 폴더 아래에 둔다.
런타임 스크립트와 에디터 스크립트를 분리한다.
Prefab 저장은 UnityEditor.PrefabUtility를 사용한다.
Asset 생성은 UnityEditor.AssetDatabase를 사용한다.
```

---

## 예상 파일 구조

```text
Assets/
├─ Scripts/
│  └─ HandPose/
│     ├─ HandPoseData.cs
│     ├─ HandPoseInteractable.cs
│     └─ HandPoseApplier.cs
│
├─ Editor/
│  └─ HandPoseEditorWindow.cs
│
└─ HandPoses/
   ├─ HandPose_FireExtinguisher.asset
   ├─ HandPose_ChemicalBottle.asset
   └─ HandPose_Button.asset
```

---

## 완료 기준

구현이 완료되었다고 판단하려면 다음이 가능해야 한다.

```text
1. Unity Editor에서 손 모델을 원하는 모양으로 포징할 수 있다.
2. Tools/VR Hand Pose Tool 메뉴에서 에디터 창을 열 수 있다.
3. 포징된 손과 대상 물체를 선택한 뒤 버튼을 누르면 HandPoseData가 생성된다.
4. 대상 물체 아래에 AttachPoint와 GhostHand가 자동 생성된다.
5. 대상 물체에 HandPoseInteractable이 자동으로 추가되고 참조가 연결된다.
6. 플레이 중 해당 물체를 잡으면 실제 손이 저장된 포즈로 바뀐다.
7. 물체를 놓으면 실제 손이 기존 애니메이션으로 돌아간다.
```

---

## 한 문장 요약

Unity Editor에서 손을 포징한 뒤 버튼 한 번으로 HandPose 데이터 저장, AttachPoint 생성, GhostHand 생성, 물체 컴포넌트 연결까지 자동화하고, 런타임에서는 물체를 잡을 때 저장된 손 포즈를 적용하고 놓으면 기본 손 애니메이션으로 복귀하는 시스템을 구현해줘.
