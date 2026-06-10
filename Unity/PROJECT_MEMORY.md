# 종합설계프로젝트 7조 — 프로젝트 메모리

> 사용자(R, smroh0509@gmail.com) = **노성민**, Unity 프론트엔드 담당
> 출처: `7조_중간 보고서.pdf` (중간보고서 요약 + 본문)

---

## 1. 과제 개요

- **과제명**: 온톨로지 및 논리 추론 기반 개인 생산성 향상 도구 개발 및 연구
- **기간**: 2026.03.01 ~ 2026.06.30
- **타겟**: Android 단일 앱 (Unity 6.3 기반)
- **핵심 목표**: 분산된 개인 생활 데이터를 온톨로지 지식 그래프로 통합 → 인과 추론 → 3D 메타버스(My Room)에 시각화 → 퀘스트-보상 루프로 행동 변화 유도
- **운영비**: 월 0원 (서버리스 + 온디바이스 AI)

## 2. 팀 구성

| 학번 | 이름 | 역할 |
|---|---|---|
| 2023012083 | 김무성 | 온톨로지 설계 및 클라우드 추론 |
| 2021111675 | 김준석 | 데이터 전처리 및 온디바이스 AI 연동 |
| **2023014973** | **노성민 (= 사용자)** | **Unity 개발 (프론트엔드)** |
| 2024005961 | 이서윤 | Unity 개발 (백엔드) |
| 2024016926 | 천이퉁 | UI/UX 설계 및 디자인 |

## 3. 시스템 아키텍처 (3계층)

### ① On-device 계층
- **Unity 6.3 (C#)**: UI 전체(피드/마이룸/퀘스트/상점), 3D 렌더링, Firebase SDK 연동, 로컬 데이터 수집
- **Gemma 3n E2B (MediaPipe)**: 2B 파라미터, RAM ~2GB, raw 데이터 → RDF 트리플 변환만 담당 (추론 X)
- **SQLite (로컬 전용, 미전송)**:
  - `raw_data`: EXIF, GPS, 걸음수, 수면, 앱 사용시간
  - `triples`: (S, P, O) 구조화 데이터 (batch 시 신규분만 sync)
  - `image_cache`: 이미지 분석 캐싱
  - `inference_queue`: batch 대기 작업

### ② Firebase 계층
- **Auth**: 소셜 로그인
- **Firestore (공유 전용)**: `room_objects`, `quests`, `rewards`, `follows`, `users/{uid}`, `temp_triples`
- **Storage**: 아바타 이미지, 직렬화된 온톨로지 그래프
- **FCM**: 방 업데이트/퀘스트 알림

### ③ Cloud Functions 계층 (Python)
- Cloud Scheduler가 **1일 6회 batch** 실행
- 흐름: temp_triples 읽기 → DuckDB 전처리/집계 → Storage에서 기존 그래프 로드 → RDFLib + SPARQL 추론 → room_objects/quests/persona Firestore 기록 → 그래프 직렬화 후 Storage 저장 → temp_triples 삭제 → FCM 발송

## 4. 핵심 기능

### 데이터 파이프라인 (7단계)
1. (Unity) 갤러리/GPS/Health Connect/UsageStats 수집 → SQLite `raw_data`
2. (Gemma 3n) 트리플 추출 → SQLite `triples`
3. SQLite에 누적, raw는 절대 미전송
4. (Sync) 신규 트리플만 Firestore `temp_triples` 업로드
5. (Cloud) DuckDB 집계/패턴 분석
6. (Cloud) SPARQL 추론 → 분기 A(결과 생성) or 분기 B(퀘스트 생성)
7. (Unity) 실시간 리스너로 방 즉시 갱신, 퀘스트 표시

### 내 방 vs 남의 방
- **내 방**: Firestore 실시간 리스너 → 즉시 Unity 3D 반영
- **남의 방**: `room_objects` JSON fetch → 동일 렌더링 코드로 표시 (Storage 스냅샷 X)

### 퀘스트 2종
- **데이터 보완형**: 온톨로지 빈 노드 감지 (예: "오늘 카페 누구랑 갔어?")
- **삶 개선형**: 패턴 분석 (예: 수면 불규칙 → "30분 산책하기")
- 완료 시 재화 지급 → 가구/스킨/펫 구매

### 복원 전략
- 페르소나: Firestore `users/{uid}` 백업 (1일 최대 6회 덮어쓰기, 수 KB)
- 온톨로지 그래프: Firebase Storage 직렬화 저장
- raw 데이터: 복원 불가 (재수집)

## 5. 화면 구성 (전체 24개 + 공통 컴포넌트 6개)

- **첫 실행**: 온보딩 → 로그인 → 권한 → 프로필 → 피드
- **재실행**: 스플래시 → 마이룸
- **피드**: 팔로우 사용자 방 카드 목록
- **마이룸 3D**: 책상(퀘스트), 일기, 옷장, 달력, 게시판, 침대, 문, AI 펫, 편집 버튼
- **남의 방 3D**: 게시판, NPC, 침대, 달력, 닉네임, 문/뒤로가기
- **상점**: 가구/스킨/펫 카테고리
- **설정**: 프로필, 알림, 권한, 사운드, 해상도, 프레임, 로그아웃
- **공통 UI**: 하단 네비(피드/마이룸/퀘스트/상점), 좌상단 설정, 3D 진입 시 하단 바 숨김, 보상 팝업/푸시 토스트/재화 잔액 바 오버레이

## 6. 기획서 대비 주요 변경점

| 원안 | 변경 | 사유 |
|---|---|---|
| AWS t3.micro + FastAPI | Cloud Functions 서버리스 | 비용 0원 |
| WebSocket | Firestore 리스너 + FCM | 서버 없이 실시간 |
| Claude 보조 AI | Gemma 3n E2B 온디바이스 | 비용 0원, 프라이버시 |
| Firebase Storage + Cloudflare | Firebase Storage 단일 | 단순화 |
| Android + Unity 분리 | Unity 단일 앱 | 규모 축소 |
| 남의 방 실시간 3D | room_objects JSON 렌더링 | Storage 부하↓ |
| Firestore 단일 | SQLite + Firestore 이중 | 프라이버시 |

## 7. 지금까지 수행 내용 (중간 시점 기준)

- **전원**: Unity 6 + Firebase SDK 세팅, 모노레포(unity/, functions/, docs/) + Git LFS, 아키텍처 재설계 확정
- **김무성**: 온톨로지 스키마 설계 (schema:Person 중심, 수집 5종 + 맥락 3종 + 추론 3종 + 시스템 2종), 트리플 JSON 포맷 합의, 클라우드 추론 파이프라인 설계
- **김준석**: SQLite 스키마 생성, Location/EXIF 수집 구현, MediaPipe + Gemma 3n 환경 세팅, 트리플 추출 프롬프트 설계, 비전 멀티모달 분석 검증
- **이서윤**: Google Sign-In + Firebase Auth 연동, Android 빌드 통과, 데이터 모델 8개 + Manager 7개 구현, Firestore CRUD + 실시간 리스너 테스트 통과
- **노성민 (나)**:
  - 마이룸 3D 뷰 구성 및 오브젝트 시스템 설계
  - 7개 가구 + 아바타 + AI 펫 배치 구조 확정
  - 각 가구 탭 인터랙션 흐름 확정
  - 첫 실행/재실행 분기 처리 구조 설계
  - 피드/마이룸/퀘스트/상점/설정 전체 인터랙션 흐름 설계
  - 3D 뷰 진입 시 하단 네비 숨김 등 공통 화면 전환 규칙 정의
- **천이퉁**: 24개 화면 + 6개 공통 컴포넌트 정의, Figma 와이어프레임 착수, 마이룸 가구별 인터랙션 디자인

## 8. 향후 추진할 내용

### 노성민 (내가 할 일) — Unity 프론트엔드 구현
- Unity 마이룸 3D 환경 구현
- 7개 가구 + 아바타 + AI 펫 오브젝트 씬 배치
- 각 가구 탭 인터랙션 연결
- Firestore 실시간 리스너로 room_objects 변경 감지 → 내 방 렌더링
- 타인 room_objects JSON fetch → 동일 코드로 남의 방 렌더링
- 방 커스텀 모드 (아이템 트레이 + 드래그 배치)
- 월간 캘린더 기반 과거 방 재구성

### 다른 팀원 요약
- 김무성: RDFLib + SPARQL 추론 엔진 구현, DuckDB 집계, room_objects/quests 기록, JSON-LD 직렬화 사이클
- 김준석: Health Connect/UsageStats Java 브릿지, WorkManager batch 스케줄러, Reverse Geocoding, OAuth(Calendar/Spotify/날씨) 연동, 퀘스트 트리거 → 자연어 변환
- 이서윤: 실시간 리스너 완성, FCM 수신 처리, 재화 트랜잭션, 팔로우 승인 백엔드, 페르소나 복원, Storage 연동
- 천이퉁: Figma 최종 디자인 확정, Unity 에셋 적용, AI 펫/상점 아이템 에셋, 보상 팝업/푸시 토스트 인터랙션 효과
- 전원: 퀘스트 시스템 + 보상 루프 연결, end-to-end 통합 테스트, 100명 부하 테스트, 중저가 기기 Gemma 3n 성능 테스트, 소프트웨어 등록

## 9. 해결한 난제 / 설계 결정

- 서버 비용 → Cloud Functions 서버리스
- AI API 비용 → 온디바이스 Gemma 3n E2B
- 프라이버시 → SQLite(로컬) + Firestore(공유) 이중 구조, raw 미전송
- Unity → Android 네이티브 API: Java 브릿지
- SPARQL 성능: DuckDB 전처리 → RDFLib 추론 2단계
- 남의 방 부하: 스냅샷 이미지 X, room_objects JSON 렌더링
- Cloud Functions 9분 타임아웃: 그래프 직렬화 후 Storage 로드/저장
- Firebase SDK 100MB+ 파일: Git LFS

## 10. 핵심 키워드

마이룸, 종프, 7조, 페르소나, 트리플, 온톨로지, SPARQL, RDFLib, DuckDB, Gemma 3n E2B, MediaPipe, Firebase, Firestore, Cloud Functions, room_objects, temp_triples, 퀘스트 보완형/개선형, 팔로우 승인제, AI 펫, 7개 가구, 24개 화면, batch 1일 6회

---

## 11. 종프 세션 현재 진행 상황 (Unity 프론트엔드 — 노성민 작업)

### 현재 진행 중
- **[수정 완료, Unity 측 테스트 필요] 댓글 DB → 포스트잇 프리팹 스폰**
  - **근본 원인**: `postit.prefab` 파일이 **YAML 레벨에서 깨져 있음** (78줄에서 `m_RemovedComponents: [` 뒤로 잘림, 닫는 `]` + `m_SourcePrefab` 라인 모두 없음)
  - 그래서 Unity가 fileID 100100000을 resolve 못해서 런타임에 `postItPrefab=False` 반환
  - 씬 파일 fileID(`100100000, guid: 7c6b2154...`)는 정상이었음 — fileID 수정 작업은 무관했음
  - 추가 발견: `Assets/Resources/postit.prefab`도 동일하게 깨진 복사본 (Resources.Load fallback도 같은 이유로 실패)
  - **적용한 수정**:
    1. `Assets/03_Prefabs/postit.prefab` 끝에 `m_RemovedComponents: []` `m_RemovedGameObjects: []` `m_AddedGameObjects: []` `m_AddedComponents: []` + `m_SourcePrefab: {fileID: 100100000, guid: f1838138604891e44b35c491fda1b44a, type: 3}` 추가 (model.dae를 source로)
    2. `Assets/Resources/postit.prefab` 동일하게 복구
    3. `PostItManager.cs`에 3단 fallback 추가:
       - 1차: Inspector 연결 (`postItPrefab` field)
       - 2차: `Resources.Load<GameObject>("postit")`
       - 3차: `GameObject.CreatePrimitive(PrimitiveType.Quad)` (테스트용 Quad)
    4. `ValidateSetup()`이 `_useFallbackPrimitive=true`일 때 prefab null이어도 통과하도록 수정
    5. `SpawnPostIt()`에서 fallback 모드 분기 추가
  - **테스트 방법**: Unity에서 씬 다시 열고 (또는 Reimport All) Play. 콘솔에 `①-A Resources.Load fallback → prefab=True` 뜨면 프리팹 복구 성공. `①-B` 경고 뜨면 Quad가 5개 board 앞에 스폰됨.
  - **2차 이슈**: `Instantiate(postItPrefab, ...)`에서 `InvalidCastException` 발생 (Resources.Load는 성공했으나 Unity 내부 캐스트 실패) — 프리팹 자체가 여전히 어딘가 깨져 있어서 GameObject로 cast 불가.
  - **최종 해결 (현재 코드)**:
    - Start()에서 1회 probe-Instantiate로 프리팹 사용 가능 여부 검증 (실패 시 `_useFallbackPrimitive=true` 영구 전환)
    - 디폴트 모드 시 `GameObject.CreatePrimitive(Cube)`로 40×40×5cm 얇은 판 생성 (모든 면 보임 → 방향 무관, backface culling 영향 없음)
    - `SetParent(board, true)` (worldPositionStays=true)로 board scale (6,10,1) 상속 회피
    - Resources.Load fallback 제거 (불필요한 복잡도)
    - SpawnPostIt 내부 try-catch 제거 (probe로 시작 시 한 번에 결정)
  - **디폴트 댓글 (운영자 3개)**:
    1. "오늘 방의 댓글을 추가해주세요"
    2. "오늘도 좋은 하루 되세요"
    3. "어떤 하루를 보내고 있나요?"
  - **디폴트 포스트잇 transform** (board 자식 기존 model.dae 포스트잇과 동일한 보이는 크기):
    - model.dae 메시 intrinsic 크기 = (12.6, 1.7, 12.6) 유닛 (분석 결과)
    - 기존 PrefabInstance LocalScale = (0.05, 0.3, 0.03)
    - Cube는 unit(1,1,1)이라 등가 LocalScale = 메시크기 × 기존 스케일 = **(0.63, 0.51, 0.378)**
    - LocalRotation: Euler (90, 0, zRot) — zRot은 ±randomRotationRange 랜덤
    - SetParent(board, false) 후 worldPosition 설정, localScale/localRotation 적용
  - **DB 연동 위치 명시**: `LoadComments()` 함수에 큰 배너 주석 추가 — DB(Firestore) 연동 코드를 여기에 작성, 응답 항목마다 `AddComment(username, comment)` 호출, 실패 시 더미로 fallback

### 다음 작업 (순서대로)
3. **가구 위치 배치 변경 + 플레이어 겹침 처리**
4. **프레임 설정 (낮음 / 보통 / 높음)** — 설정 화면 연동
5. **가구별 기능 포커스** — 7개 가구 각 탭 인터랙션 (책상=퀘스트, 일기, 옷장, 달력, 게시판, 침대, 문)
6. **감도 조절** — 카메라/조이스틱 감도 슬라이더 (설정에서 처리, #4와 묶어서) — 완료

### 포스트잇 관련 추가 작업 (완료)
- **board 가까이 스폰**: CacheBounds를 Renderer 우선으로 변경 + `surfaceOffset = 0.01f` Inspector 노출
- **TMP 텍스트 자식**: CreateDefaultPostIt에서 Cube의 -Y 면(플레이어 향함)에 TextMeshPro 자식 추가. PostItNote.Setup이 `<b>username</b>\n\ncomment` 형식으로 텍스트 세팅. 본체 머티리얼은 TMP renderer 제외하고 적용.
- **댓글 작성 UI** (`CommentInputUI.cs`):
  - Canvas GameObject에 컴포넌트로 부착하면 Play 시 우측 하단 "댓글" 버튼 + 입력 패널 자동 생성
  - InputField characterLimit=50, 글자수 카운터 표시
  - 확인 시 PostItManager.AddComment 호출 → 랜덤 위치/머티리얼 포스트잇 자동 추가
  - `isMyRoom` 플래그 — true면 버튼 비활성. **테스트 위해 디폴트 false(활성)**. 운영 시 자기 방이면 true로 세팅
- **백엔드(DB) 저장**: 프론트는 주석으로 위치만 표시. 실제 DB 통신은 백엔드 담당자가 별도 모듈(`BackendBridge.SaveComment` 같은 함수) 구현 후 호출. Firestore/그 외 기술 스택 결정은 백엔드 영역.

### 포스트잇 클릭 + 댓글 버튼 수정 (완료)
- **포스트잇 클릭 안 됨 원인**: BoardInteraction collider가 board 표면보다 카메라 쪽으로 두껍게 확장돼 있어서(이전 가장자리 클릭 수정 잔재), 표면에 붙은 포스트잇이 그 collider 뒤에 위치 → `Physics.Raycast`가 board를 먼저 잡음
  - **수정**: `CameraController.CheckBoardClick`을 `Physics.RaycastAll`로 변경, 우선순위 **AddCommentButton > PostIt > Board > 빈 공간**으로 선택. board collider가 앞을 막아도 포스트잇 잡힘.
- **댓글 버튼**: Canvas UI 버튼 → **3D HUD 버튼**으로 교체. `CommentInputUI.Build3DButton()`이 카메라 자식으로 Cube+TMP "댓글" 생성, 우측 하단 고정(`button3DLocalPos = (0.45, -0.32, 1)`). 태그 `AddCommentButton`.
  - 클릭은 CameraController가 raycast로 감지 → Board 줌인 상태에서만 → `CommentInputUI.Instance.OpenPanel()`
  - Canvas 입력 패널(InputField 50자 + 확인/취소)은 그대로 유지 (타이핑은 Canvas 필요)
  - `CommentInputUI.Instance` 싱글톤 추가
- **TagManager.asset**에 `AddCommentButton` 태그 추가됨
- **Unity 세팅**: `CommentInputUI`를 씬 Canvas에 부착 → Play 시 3D 버튼 + 패널 자동 생성

### 줌인/조이스틱/포스트잇 텍스트 수정 (완료)
- **줌인 시 조이스틱 숨김**: `BoardFocusController.LockPlayer`에서 `joystickObject.SetActive(!locked)`. 조이스틱 자동 검색(Awake에서 `FindObjectOfType<JoystickController>`). `CameraController.IsTouchOnJoystick` + `JoystickController.IsOnJoystickArea`에 `activeInHierarchy` 체크 추가 → 숨겨졌을 때 터치 안 가로챔 → 포스트잇 클릭 가능
- **postit.prefab 사용자가 수정함**: 이제 `m_SourcePrefab` 정상 → 프리팹 제대로 로드됨. Canvas+TMP Text 자식 추가됨 (단 RenderMode 0, scale 0,0,0이라 그대로면 안 보임 → 코드에서 처리)
- **SpawnPostIt 통일**: 프리팹 경로도 `Instantiate(prefab, board)` 후 `localRotation = Euler(90,0,zRot)` 적용. localScale은 프리팹 authored (0.05/0.3/0.03) 유지 → 기존 비활성 포스트잇과 동일
- **PostItNote 재작성**:
  - Setup에서 본체 머티리얼 적용(TMP/Canvas 렌더러 제외)
  - `TextMeshProUGUI`/`TextMeshPro` 둘 다 `<b>작성자</b>\n\n댓글` 텍스트 세팅
  - Canvas를 `RenderMode.WorldSpace`로 전환 + 본체 메시 bounds 기준으로 위치/스케일 코드 배치 (`PlaceCanvas`)
  - 줌인된 동안 `LateUpdate`에서 Canvas가 카메라 향하도록 빌보드 (`SetBillboard`) — 포커스된 1개만
- **BoardFocusController**: FocusPostIt → `note.SetBillboard(true)`, BackToBoard/BackToFree → `SetBillboard(false)`
- **PostitPlus 버튼 연결 완료**: 사용자가 board 자식으로 PostitPlus(postit 프리팹 인스턴스 + Canvas/Text) 생성. `CommentInputUI.WirePostitPlus()`가 `GameObject.Find("PostitPlus")`로 찾아서:
  - 태그 `AddCommentButton` 부여 (TagManager에 등록됨)
  - BoxCollider 보장 (raycast 클릭용)
  - 크기 `postitPlusScaleMultiplier`(기본 2.5x) 배율로 확대
  - Canvas 텍스트 `postitPlusLabel`(기본 "+ 댓글 추가") 세팅 + `PostItCanvasHelper.PlaceCanvas`로 World Space 배치
  - 클릭 → CameraController.CheckBoardClick이 Board 줌인 상태에서 감지 → `CommentInputUI.Instance.OpenPanel()`
- **CommentInputUI 변경**: 카메라 자식 Cube HUD 버튼(Build3DButton) 제거 → PostitPlus 사용으로 교체. Canvas 입력 패널(BuildPanel)은 유지.
- **PostItCanvasHelper.cs 신규**: Canvas를 본체 메시 Renderer bounds 기준으로 World Space 배치하는 공용 static 헬퍼. PostItNote와 CommentInputUI가 공유.
- **주의**: model.dae 메시 pivot이 (-62,151,-2.8)로 크게 어긋나 있어 Canvas는 Renderer.bounds 기준 런타임 계산. faceNormal은 `transform.up` 추정 — 방향 안 맞으면 PostItCanvasHelper.PlaceCanvas의 faceNormal 조정 필요. 줌인 시엔 PostItNote 빌보드가 보정.

### 콜라이더 정확한 fit (메시 꼭짓점 변환) (현재)
- **콜라이더 크기 틀어짐 원인**: 기존 `EnsureFittedCollider`가 월드 AABB(`rend.bounds`)를 lossyScale로 나눠서 로컬 size 계산. **회전(Euler 90,0,0)** 있으면 AABB가 mesh와 안 맞아서 collider가 실제 메시보다 크거나 작아짐. → 포스트잇/Sticky_note_yellow 위치/크기에 콜라이더가 안 맞아서 raycast 실패.
- **수정**: `mf.sharedMesh.bounds`의 **8 꼭짓점**을 mesh local → 월드 → 루트 로컬로 변환해서 정확한 axis-aligned bounds 재구성. 회전/스케일/계층 차이 모두 자동 보정. 모든 케이스(포스트잇, Sticky_note_yellow)에 정확.
- 자식 collider 무시하고 루트에만 강제 추가/refit. 진단 로그 `BoxCollider fit — center=... size=... tag=...`.

### 루트 콜라이더 강제 + Gizmo 양방향 cross (이전)
- **클릭 안 됨 진짜 원인**: `EnsureFittedCollider`가 자식에 collider 있으면 early-return하던 게 문제. Sticky_note_yellow는 자식(Canvas 등)에 collider가 있을 수 있어서 **루트(AddCommentButton 태그)에 콜라이더가 안 붙음** → raycast가 자식 collider 잡으면 태그 다름 → 인식 실패. 해결: 무조건 루트에 BoxCollider 추가/refit. 자식 collider 무시. 진단 로그 추가 (`BoxCollider fit — center=... size=... tag=...`).
- **Gizmo 빨강 안 보이던 문제**: 빨강선이 center→+hw 한쪽만 그려져서, area.right 방향이 시야축에 가까우면 거의 안 보였음. 해결: **양방향 전체 cross** (center-hw ↔ center+hw)로 빨강/초록 둘 다 그리고 양 끝에 구체 표시. 추가로 파랑 선으로 area.forward 방향도 표시.

### EventSystem 자동 생성 + Gizmo 시각화 + 버튼 겹침 방지 (이전)
- **UI Button 클릭 안 됨 진짜 원인 ★**: 씬에 EventSystem GameObject가 **없음** (`grep` 결과 EventSystem 0건). EventSystem 없으면 UI Button.onClick 절대 fire 안 됨. 해결: `CommentInputUI.Awake`에서 자동 생성. 새 Input System 패키지 있으면 `InputSystemUIInputModule`(reflection으로 type 찾음, Unity.InputSystem assembly), 없으면 `StandaloneInputModule` fallback. 콘솔 로그 `EventSystem 자동 생성 — UI 클릭 활성화됨`.
- **스폰 영역 시각화 (Gizmo)**: `PostItManager.OnDrawGizmos` — 에디터/Play에서 항상 보임. 시안색 사각형=스폰 영역, 빨강 선=spawnWidth 방향, 초록 선=spawnHeight 방향, 노랑 구체=중심점. 이걸로 영역이 어느 방향으로 어디 위치하는지 시각 확인 가능 (X 조정이 안 보이면 빨강 선이 실제 어느 축인지 보고 진단).
- **댓글 버튼 위 겹침 방지**: `PostItManager.MarkOccupied(worldPos)` 공개 메서드. `CommentInputUI.WireAddButton`이 Sticky_note_yellow의 body renderer 중심을 등록. minSpacing 회피 로직이 자동 처리해서 그 위에 포스트잇 안 생김.

### UI Button 직접 연결 + spawnOffset (이전)
- **Sticky_note_yellow 클릭 인식 안 됨**: 3D 콜라이더 raycast가 안 잡힘. 해결: `CommentInputUI.WireAddButton`이 Sticky_note_yellow 자식의 **모든 Button을 찾아 onClick에 `TogglePanel` 연결**. 사용자가 안에 넣은 UI Button을 직접 활용. (3D raycast 경로도 함께 유지)
- **패널 초기 hide**: `Awake()`에서 즉시 `postitPanel.SetActive(false)` + `backgroundPanel.SetActive(false)`. 사용자가 에디터에서 켜둬도 런타임 시작 시 닫힘.
- **TogglePanel 디버그 로그**: 호출 시 postitPanel 연결 여부, 현재 활성 상태, isMyRoom 콘솔에 찍어서 어디서 막히는지 진단 가능.
- **PostItManager.spawnOffset**: Vector3 추가 (X=우/좌, Y=위/아래, Z=앞/뒤 area 로컬). Inspector에서 숫자만 살짝 조정해서 board 면 위치 미세조정. `area.right*x + area.up*y + area.forward*z` 더해서 candidate 계산.

### 스폰 영역 = 직접 배치 Transform + 댓글 UI 패널 (이전)
- **스폰 위치 문제**: board 로컬 좌표 추측(board.TransformPoint)도 옷장 내부에 생성됨 — board의 로컬 축/피벗을 신뢰 불가.
- **최종 해결 — 사용자 직접 조정 방식**: `PostItManager`에 `spawnArea`(Transform) 필드 추가. 사용자가 board 면 위에 빈 GameObject를 놓고 연결하면 그 위치/방향 기준으로 생성. `spawnWidth`/`spawnHeight`(월드 단위)로 퍼짐 범위 조절. `GetRandomPosition`은 `area.position + area.right*rx + area.up*ry`. spawnArea 비우면 board 사용. 모든 board 로컬 좌표 추측 코드 제거.
- **댓글 입력 UI 흐름** (CommentInputUI): Sticky_note_yellow 클릭 → `postitPanel` + `backgroundPanel` 띄움 / `backgroundPanel` 클릭 → 둘 다 닫고 원래 화면 / `registerButton`(등록) 클릭 → 댓글 추가 + 닫기.
- **런타임 컴포넌트 추가 안 함**: 사용자가 이미 만든 버튼 사용. `backgroundButton` 미연결 시 `backgroundPanel.GetComponent<Button>()`로만 찾음(AddComponent 안 함). Inspector 필드: `postitPanel`, `backgroundPanel`, `backgroundButton`, `inputField`, `registerButton`.

### 포스트잇 안 생기는 문제 — 근본 해결 (완료)
- **진짜 원인**: `postit.prefab`이 **model.dae의 Prefab Variant**였는데, 이 변형 구조가 깨져서 런타임에 `Instantiate`가 **null 반환** (변형 체인 resolve 실패). fallback Cube가 보였던 것도 이 때문.
- **근본 해결**: `postit.prefab`을 **변형이 아닌 일반(regular) 프리팹으로 완전히 재작성**.
  - 루트 GameObject "postit" (fileID `3144538496394355800`, layer 7, tag PostIt) + Transform(scale 0.05/0.3/0.03, rot Euler 90,0,0) + MeshFilter(model.dae 메시 `-7843526606762953986`) + MeshRenderer(머티리얼 `e03c61940200...`)
  - 자식 "Text (TMP)" — R님이 배치한 3D TextMeshPro 그대로 (fileID·local값 전부 보존, m_Father만 새 루트로)
  - PrefabInstance/m_SourcePrefab 없음 = 순수 일반 프리팹
- **씬 참조 업데이트**: PostItManager `postItPrefab` → `{fileID: 3144538496394355800, guid: 7c6b2154...}` (일반 프리팹은 루트 GameObject 실제 fileID로 참조)
- `AddComment`: 깔끔한 `Instantiate(postItPrefab, board)` — 변형 아니므로 정상 작동. 위치/회전만 세팅.
- 참고: model.dae 메시 fileID `-7843526606762953986` (출처: `_Recovery/0 (2).unity`의 정상 동작하던 postit 인스턴스). PostItNote 스크립트 guid `da7233e9a88033546b80c61b51f8a953`.

### PostItManager 대폭 단순화 (이전)
- 사용자 요청: "프리팹의 TMP 그대로 써라, 위치·폰트 건드리지 마라, 쉽게쉽게"
- **제거**: `CreateDefaultPostIt`(하드코딩 TMP 위치/폰트 생성), `_useFallbackPrimitive`, probe-Instantiate 검증, Cube fallback 전체 → 프리팹 깨졌던 시절의 잔재였음
- **현재 PostItManager**: `Start` → `CacheBounds` + `LoadComments`. `AddComment` → `Instantiate(postItPrefab, board)` + 위치/회전만 세팅. 끝. 프리팹 그대로 신뢰.
- **PostItNote.Setup**: `<b>` 태그 제거 → `tmp.text = $"{username}\n{comment}"` 순수 텍스트만. TMP 위치/폰트/정렬 전부 프리팹 그대로.
- 디버그 로그 ①~⑨ 노이즈 제거.

### 프리팹 TMP / 줌인 / Sticky_note_yellow 토글 (완료)
- **postit 프리팹 변경**: 사용자가 Canvas 대신 **3D `TextMeshPro`** Text(TMP)를 정확한 위치(anchoredPos 62.9,151.3 — 메시 pivot에 맞춤)에 배치함. `PostItNote.Setup`은 이제 텍스트만 세팅(`<b>작성자</b>\n댓글`), 위치/Canvas/billboard 로직 전부 제거.
- **포스트잇 줌인 안 됨 원인**: model.dae 메시 pivot이 (-62,151,-2.8)로 어긋나 있어 `note.transform.position`이 실제 포스트잇 위치가 아니었음 → 카메라가 엉뚱한 곳으로 줌. **수정**: `PostItNote.GetVisualCenter()`(body Renderer bounds.center) 추가, `BoardFocusController.FocusPostIt`이 이걸 사용.
- **콜라이더**: `PostItCanvasHelper.EnsureFittedCollider()` — 본체 메시 world bounds를 루트 로컬로 변환해 BoxCollider를 **루트**에 추가 (루트에 붙어야 raycast 시 루트 태그 PostIt/AddCommentButton이 잡힘). PostItNote.Awake + CommentInputUI.WireAddButton에서 호출.
- **board 자식 댓글 버튼 = `Sticky_note_yellow`** (PostitPlus 아님). scale 7.5×22.5×4.5, "+" 텍스트. CommentInputUI가 `addButtonName`("Sticky_note_yellow")로 찾아 태그 부여 + 콜라이더. 크기/텍스트는 사용자가 이미 설정 → 안 건드림.
- **토글 동작**: CameraController가 AddCommentButton 클릭 감지 → `CommentInputUI.TogglePanel()` (열려있으면 닫고, 닫혀있으면 염).
- **PostItCanvasHelper**: PlaceCanvas 제거, FindBodyRenderer + EnsureFittedCollider만 남음.

### 겹침 방지 / 텍스트 위치 / 입력창 (이전)
- **#1 포스트잇 겹침 방지**: `PostItManager.GetRandomPosition`이 `_placedPositions` 리스트로 기존 위치 추적, `minSpacing`(기본 1.5) 이상 떨어진 위치를 `placementAttempts`(25회) 재시도. 못 찾으면 가장 여유로운 후보 사용. ClearAll/RemoveOldest에서 리스트 정리.
- **#2 텍스트 위치 어긋남 원인**: PostitPlus/postit 프리팹의 Canvas RectTransform `pivot`이 (0,0)이라 `crt.position` 설정 시 좌하단 기준이 되어 텍스트가 어긋남. `PostItCanvasHelper.PlaceCanvas`에서 pivot/anchor를 (0.5,0.5)로 정규화 + 자식 Text를 Canvas 꽉 채우도록 stretch. (한글 폰트는 사용자가 별도 처리 예정)
- **#3 댓글 입력 창**: 자동생성 Canvas 패널 제거. `CommentInputUI`에 Inspector 필드 `commentWindow`/`inputField`/`confirmButton`/`cancelButton` 추가 — 사용자가 직접 만든 "큰 포스트잇 모양 창"을 연결. OpenPanel은 commentWindow 활성화만, Confirm은 inputField 읽어서 AddComment. `inputField.characterLimit`은 maxCharacters(50)로 자동 설정.
  - **Unity 세팅 변경**: CommentInputUI는 이제 Canvas 부착 불필요(아무 GameObject 가능). 대신 Inspector에서 입력 창 UI 4개 필드 연결 필요.

### 최근 해결된 이슈
- 줌인 후 밖 클릭 시 이전 카메라 yaw/pitch 복원 (BoardFocusController `SaveState/RestoreState`, `Returning` 상태 추가)
- board 가운데 클릭이 wall로 인식되던 문제 → BoxCollider center.z를 충분히 앞으로 + size 확장 (로컬 8×2.8, 스케일 적용 후 world 48×28)
- 잠금 상태에서 플레이어 입력 무시 (`enabled=false` 시 SetJoystickInput 차단)
- **#6 감도 조절 완료** (CameraController):
  - `rotateSpeed` 0.1 → 0.15 (기본값 살짝 인상)
  - `touchSensitivityMultiplier` 신규 추가 (기본 2.5, 1~5 슬라이더) — 터치 입력에만 배율 적용
  - `dragDeadZonePx` 1.0 → 0.3 (작은 움직임도 반응)
  - 최종 효과: 모바일 터치 회전 감도 ≈ 기존의 3.75배. Inspector에서 미세 조정 가능

### 알려진 메모
- Board layer = 6, PostIt layer = 7
- 포스트잇은 `board`의 자식으로 Instantiate됨
- `[PostItManager] Start — prefab=False board=True mats=3` 로그 나오면 프리팹 연결 문제

> ⚠️ 출처: 종프 세션(local_ff285346) 트랜스크립트 — TaskList는 세션 격리로 직접 가져오기 불가, R님이 직접 알려주신 내용 기반
