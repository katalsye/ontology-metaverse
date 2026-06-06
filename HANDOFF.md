# 📦 ontology-metaverse — Claude Code 인계 문서

> 이 파일 통째로 Claude Code(Desktop App, Code 탭) 첫 메시지로 던지면 컨텍스트 잡힘.
> 작성일: 2026-06-05, Google 로그인 E2E 성공 직후.

---

## 1. 프로젝트 개요 및 현재 스택

### 개요
- **프로젝트명**: ontology-metaverse — 온톨로지 기반 안드로이드 생산성 앱
- **소속**: 경북대학교 전자/컴퓨터공학 종합설계프로젝트 7조 (담당자: 김준석, 학번 2021111675)
- **본인 역할**: 데이터 전처리 + 온디바이스 AI 통합 (7인 팀)
- **방침**: "완벽한 앱" — mock / Anonymous 우회 금지, 실데이터 + 진짜 온디바이스 Gemma

### 마감
| 일정 | 날짜 |
|---|---|
| 발표 | **2026-06-20** |
| SW 등록 (한국저작권위) | **2026-06-30** |

### 핵심 스택
| 분류 | 도구/버전 |
|---|---|
| 게임 엔진 | Unity **6000.3.10f1** (IL2CPP, arm64-v8a, Release) |
| 타겟 OS | Android Min API 25 / Target API 36 |
| IDE | VS Code |
| 인증 | Firebase Auth (Google Sign-In) — ✅ 작동 검증 완료 |
| DB | Firestore + 로컬 SQLite (sqlite-net-pcl, NuGetForUnity) |
| 메타데이터 | MetadataExtractor 2.9.3 (NuGet) |
| 온디바이스 AI | MediaPipe `tasks-genai 0.10.27` + Gemma 3n E2B int4 (2.91GB) |
| 서버 함수 | Cloud Functions (asia-northeast3) — `on_new_triple_written` |
| Firebase Unity SDK | 13.9.0 |
| 로그인 플러그인 | google-signin-unity (`libnative-googlesignin.so`) |
| 테스트 폰 | Samsung SM-S926N (Galaxy S24+, Android 16), adb id `R3CX80K07EP` |

### Firebase / OAuth 자격증명
| 항목 | 값 |
|---|---|
| 프로젝트 ID | `ontology-metaverse` (Blaze plan) |
| 프로젝트 번호 | `616295277126` |
| 패키지명 | `com.ontology.metaverse` |
| App ID (Android) | `1:616295277126:android:5d4addf6ffdff3374d2047` |
| **Web Client ID (현재 유효)** | `616295277126-qdlqp2bpf0k21pu4surr3lit1g2pobr9.apps.googleusercontent.com` |
| Android OAuth Client | `616295277126-fjbt4c0n23hlr3basomd0g6jj3i8pm3q...` (auto created) |
| **Release SHA-1** | `D6:D3:14:30:FF:CF:5E:79:8D:06:05:36:CD:25:81:8E:F4:A9:A7:36` |
| Release SHA-256 | `35:20:92:61:5C:00:A6:C2:1D:5E:CC:BB:F2:D3:D8:E4:5E:C0:7E:59:BF:04:29:00:94:64:85:2C:10:CA:03:18` |
| Cloud Functions 리전 | `asia-northeast3` |
| 검증된 테스트 UID | `aQxeWJOmJOTwNcxzmIQSAt5Yy8m2` (junseoggim349@gmail.com) |

### GitHub
- **메인 레포**: `katalsye/ontology-metaverse` (owner: 이서윤)
- **본인 계정**: `OII01` (또는 `Oll01`)
- **브랜치**: `main`, `integration/ontology-data` (무성님 작업 브랜치)
- **보조 레포 (포트폴리오용)**: `Oll01/iot-anomaly-agent`

### 팀 분담
| 이름 | 역할 |
|---|---|
| **김준석** (본인) | Android 데이터 수집, 온디바이스 Gemma, SQLite, Firestore 업로드 |
| 김무성 | Cloud Functions, 온톨로지 추론 엔진 |
| 이서윤 | repo owner, Unity 3D 씬 |
| 노성민 | Unity 3D 씬, room object 렌더링 |
| 나머지 3명 | 기획/디자인/문서 |

---

## 2. 이미 구현 완료된 것

### ✅ 빌드 환경 (검증 완료)
- Release Keystore 발급 + Google Drive 백업
- Unity Player Settings에 Custom Keystore 등록
- AndroidManifest 권한 설정 (`PACKAGE_USAGE_STATS`, `ACCESS_FINE_LOCATION`, `INTERNET` 등) — PR #92
- `google-services.json` 통합, FirebaseApp 자동 초기화 정상
- IL2CPP/arm64/Release 빌드 성공

### ✅ Google 로그인 — E2E 성공 (이번 세션에서 완성)
- `google-signin-unity` 플러그인 설치
- `libnative-googlesignin.so` 정상 로드
- `AuthConfig.asset`에 새 Web Client ID 등록
- Firebase Auth ID Token 교환 성공
- `GoogleFirebaseLogin` 컴포넌트 SignIn/SignOut 동작
- UI에 UserName/UserEmail 표시 ⚠️ 한글 폰트 미적용 (□ 깨짐)

### ✅ 데이터 수집 코드
- `GalleryEXIFCollector.cs` — MetadataExtractor로 갤러리 이미지 GPS/EXIF 추출
- `UsageStatsCollector.cs` — AndroidJavaObject로 앱 사용 통계 수집

### ✅ SQLite 4-테이블 스키마
| 테이블 | 역할 |
|---|---|
| `RawData` | 수집한 원본 데이터 |
| `Triple` | 추출된 트리플 (Subject/Predicate/Object) |
| `ImageCache` | 이미지 캐시 |
| `InferenceQueueItem` | 추론 대기 큐 |

### ✅ Cloud Functions (무성님)
- `on_new_triple_written` 트리거 배포 확인 (asia-northeast3)
- `temp_triples/{uid}/items/{auto_id}` 새 문서 → 자동 추론 → `quests`/`room_objects` 생성

### ✅ 트리플 명세 v3
- **Subject/Object**: full URI (`http://7team.dev/ontology#...`)
- **Predicate**: `prod:` 약식 허용
- **placeType → room object 매핑**:
  - `cafe` → `coffee_cup`
  - `gym` → `dumbbell`
  - `library` → `bookshelf`
  - `park` → `tree_pot`
- **PR #97** (User 타입 선언 + URI 정규화, `PlaceHabitTestUploader.cs`) — **merge 대기**

### ✅ 보조 작업물 (포트폴리오)
- IoT 이상감지 에이전트 (`Oll01/iot-anomaly-agent`) — Python, Gemini 2.5 Flash, Streamlit
- Computer Vision 과제 #1-3 (Pinhole / OpenCV calibration / ArUco solvePnP)
- Web Services Programming Flask 리팩토링 과제

---

## 3. Next Steps — 우선순위 순

### 🔥 P1. CheckDependencies init 순서 수정 (긴급, 1시간)
**증상** (logcat):
```
InvalidOperationException: Don't call Firebase functions before CheckDependencies has finished
   at Firebase.Auth.FirebaseAuth.get_DefaultInstance ()
   at UserManager.Start ()
   at TempTripleManager.Start ()
```
**원인**: `UserManager`, `TempTripleManager`가 Firebase 초기화 끝나기 전 `FirebaseAuth.DefaultInstance` 호출.

**수정 방안**:
```csharp
// GoogleFirebaseLogin 또는 메인 부트스트랩에서
FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
    if (task.Result == DependencyStatus.Available) {
        // 여기서 UserManager.Init(), TempTripleManager.Init() 호출
        // 두 컴포넌트의 Start() 안에서는 Firebase 직접 호출 제거
    }
});
```

### 🔥 P2. 한글 폰트 깨짐 (1~2시간)
**증상**: UserName "김준석"이 □□□로 표시 — `LiberationSans SDF`에 한글 글리프 없음.
**해결**:
1. Noto Sans KR (또는 Pretendard) TTF 다운로드
2. Unity → Window → TextMeshPro → Font Asset Creator → 한글 SDF 생성 (Custom Character List에 한글 범위 `0xAC00-0xD7A3`)
3. 기본 폰트 또는 Fallback에 등록
4. 모든 TMP_Text 컴포넌트에 적용

### P3. 데이터 수집 → Firestore 업로드 E2E (3~5시간)
- `GalleryEXIFCollector`, `UsageStatsCollector` 실기기에서 동작 확인 (권한 런타임 요청 포함)
- 수집 → SQLite `RawData` 저장
- Gemma 추론 (P4 완료 후) → SQLite `Triple`
- Firestore `temp_triples/{uid}/items`에 batch upload
- 무성님 Cloud Function 자동 트리거 → `quests`/`room_objects` 생성 확인 (Firestore Console에서 검증)

### P4. Gemma 3n 온디바이스 네이티브 브릿지 (옵션 B, 1~2일)
- 모델 파일 `gemma-3n-E2B-it-int4.task` (2.91GB)을 Firebase Storage에 업로드
- 앱 첫 실행 시 다운로드 (APK 번들링 X — 50MB 초과 정책 회피)
- `com.google.mediapipe:tasks-genai 0.10.27` Gradle 의존성 추가
- `AndroidJavaObject`로 C# ↔ Java 브릿지 작성
- 입력 텍스트 → 출력 Triple JSON 추출 로직

### P5. PR #97 merge (30분, 무성님 협조)
- ⚠️ **보안 이슈**: 무성님이 `serviceAccountKey.json`을 레포에 커밋한 상태. git history에서 제거 + 새 service account 키 발급 필요. 무성님에게 알리고 처리 요청.
- 위 이슈 해결 후 PR #97 merge

### P6. 발표 씬 + 3D room object 렌더링 (성민/서윤 담당, 본인은 데이터 인터페이스만)
- `room_objects` 컬렉션 스키마 확정
- Unity Firestore listener → Prefab Instantiate

### P7. 발표 자료 (6/20, 2~3일)
- 한글 5p 보고서
- PPT 슬라이드
- 데모 영상 (실기기 시연)

### P8. SW 등록 서류 (6/30, 1일)
- 한국저작권위원회 SW 저작권 등록
- 소스코드 인쇄본 + 등록 신청서

### ⭐ 보너스 (시간 남으면)
- Health Connect API 통합 (걸음수, 수면)
- 외부 API (날씨, 캘린더)
- Firebase Cloud Messaging 푸시 알림 (init은 이미 됨)

---

## 4. 통합 데이터 흐름 (전체 그림)

```
┌─────────────────────────────────────────────────────────────────┐
│  사용자 폰 (Android, Unity 앱)                                  │
│                                                                 │
│  ① CheckAndFixDependenciesAsync → Firebase init                 │
│  ② GoogleFirebaseLogin → Firebase Auth UID                      │
│  ③ Collectors:                                                  │
│     ├ GalleryEXIFCollector  (GPS/EXIF from 사진)                │
│     ├ UsageStatsCollector   (앱 사용 시간)                      │
│     └ Health Connect (P+)   (걸음/수면)                         │
│              ↓                                                  │
│  ④ SQLite RawData 저장                                          │
│              ↓                                                  │
│  ⑤ Gemma 3n (on-device, MediaPipe)                              │
│     "강남역 스타벅스에 오후 2시 방문" → Triple {S,P,O}          │
│              ↓                                                  │
│  ⑥ SQLite Triple → URI 정규화                                   │
│              ↓                                                  │
│  ⑦ Firestore upload:                                            │
│     temp_triples/{uid}/items/{auto_id}                          │
│        { subject, predicate, object, timestamp }                │
└─────────────────────────────────────────────────────────────────┘
              ↓ onWrite trigger
┌─────────────────────────────────────────────────────────────────┐
│  Cloud Function `on_new_triple_written` (asia-northeast3, 무성) │
│  - 트리플 모음 → 추론 (장소 → 적합한 room object 매핑)          │
│  - quests/{uid}/{quest_id}     생성                             │
│  - room_objects/{uid}/{obj_id} 생성                             │
└─────────────────────────────────────────────────────────────────┘
              ↓ Firestore listener
┌─────────────────────────────────────────────────────────────────┐
│  Unity 3D 씬 (성민/서윤)                                        │
│  - quests/room_objects subscribe                                │
│  - 3D Prefab Instantiate (coffee_cup, dumbbell, …)              │
│  - 사용자 방 시각화                                             │
└─────────────────────────────────────────────────────────────────┘
```

---

## 5. 파일 경로 인덱스

### 확실히 알고 있는 경로
| 항목 | 경로 |
|---|---|
| **프로젝트 루트** | `C:\Users\USER\Desktop\ontology-metaverse\` |
| AuthConfig | `Assets/02_Scripts/Config/AuthConfig.asset` |
| Release Keystore | `C:\Users\USER\Desktop\keystore\ontology-release.keystore` (alias `ontology`) |
| 빌드 APK 출력 | `C:\Users\USER\Desktop\build\ontology.apk` |
| Gemma 3n 모델 | `C:\Users\USER\Desktop\gemma-model\gemma-3n-E2B-it-int4.task` (2.91GB) |
| google-services.json | `Assets/google-services.json` (또는 `Assets/StreamingAssets/`) |
| AndroidManifest | `Assets/Plugins/Android/AndroidManifest.xml` |

### 작성된 줄 알지만 정확한 경로 확인 필요 (Claude Code가 직접 탐색)
- `GoogleFirebaseLogin.cs`
- `UserManager.cs`
- `TempTripleManager.cs`
- `GalleryEXIFCollector.cs`
- `UsageStatsCollector.cs`
- `PlaceHabitTestUploader.cs` (PR #97)
- SQLite 모델 클래스 (`RawData`, `Triple`, `ImageCache`, `InferenceQueueItem`)

### Claude Code 시작 명령 (권장)
```bash
cd C:\Users\USER\Desktop\ontology-metaverse

# 현재 상태 파악
git status
git log --oneline -10
git branch -a

# 파일 구조 탐색
ls -R Assets/02_Scripts
find Assets -name "*.cs" | findstr /I "login user triple collector sqlite raw inference"

# PR / Issue 현황
gh pr list
gh issue list
```

### 외부 도구 경로
| 도구 | 경로 |
|---|---|
| Unity Hub Editor | `C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\` |
| Android SDK build-tools | `C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\build-tools\36.0.0\` |
| apksigner | 위 경로 + `apksigner.bat` (서명 검증용) |

---

## 6. 사용자 작업 스타일 / 선호

- **빠른 진행**: "빨리빨리" 선호, 사이드 트랙 최소화
- **단계별 명시적 가이드**: 추측 X, 구체적 단계 ✓
- **복붙 가능한 전체 코드**: 부분 스니펫 X, 전체 함수/파일 ✓
- **한국어 OK** (변수명/주석은 영어 권장)
- **본질 짚는 질문 잘함**: "이거 진짜 원인이 맞아?" 같은 검증 환영
- **결정한 건 못 박기**: 한 번 정한 아키텍처는 다시 안 흔들기 (Android 플랫폼, SW 등록 academic deliverable, Option B Gemma 런타임 다운로드 등)

---

## 7. 알려진 비-blocking 이슈 (지금 안 막힘, 나중에 정리)

- `R/E ashmem Pinning is deprecated since Android Q` — 시스템 경고, 무시 가능
- `ClassLoaderContext classpath mismatch` — Unity Gradle 빌드 잔재, 무시 가능
- `LocalRequestInterceptor: No AppCheckProvider installed` — Firebase App Check 미설정, 프로덕션 가기 전엔 설정 권장하지만 지금은 무시
- `userfaultfd: MOVE ioctl unsupported` — 폰 OS 한정, 무시 가능

---

**이 문서를 Claude Code에 던지고, P1부터 순서대로 진행하면 됨. 화이팅 🚀**