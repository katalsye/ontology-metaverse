# 📦 ontology-metaverse — Claude Code 인계 문서

> 이 파일 통째로 Claude Code 새 채팅 첫 메시지로 던지면 컨텍스트 잡힘.
> **최신 작성일**: 2026-06-07, PR #109 (Weather Collector) 직후.
> **이전 인계**: 2026-06-05 (Google 로그인 E2E 성공 직후) — 그 후 진행된 거 P1~P3 다 끝남.

---

## 1. 프로젝트 개요

### 개요
- **프로젝트명**: ontology-metaverse — 온톨로지 기반 안드로이드 생산성 앱
- **소속**: 경북대학교 전자/컴퓨터공학 종합설계프로젝트 7조 (담당자: 김준석, 학번 2021111675)
- **본인 역할**: 데이터 전처리 + 온디바이스 AI 통합 (7인 팀)
- **방침**: "완벽한 앱" — mock / Anonymous 우회 금지, 실데이터 + 진짜 온디바이스 Gemma

### 마감
| 일정 | 날짜 | D-day (오늘 2026-06-07 기준) |
|---|---|---|
| 발표 | **2026-06-20** | D-13 |
| SW 등록 (한국저작권위) | **2026-06-30** | D-23 |

### 핵심 스택
| 분류 | 도구/버전 |
|---|---|
| 게임 엔진 | Unity **6000.3.10f1** (IL2CPP, arm64-v8a, Release) |
| 타겟 OS | Android Min API 26 / Target API 36 |
| IDE | VS Code |
| 인증 | Firebase Auth (Google Sign-In) — ✅ 작동 검증 완료 |
| DB | Firestore + 로컬 SQLite (sqlite-net-pcl, NuGetForUnity) |
| 메타데이터 | MetadataExtractor 2.9.3 (NuGet) |
| 온디바이스 AI | MediaPipe `tasks-genai 0.10.27` + Gemma 3n E2B int4 (2.91GB) |
| 서버 함수 | Cloud Functions (asia-northeast3, Python) — `on_new_triple_written` |
| Firebase Unity SDK | 13.9.0 |
| 외부 API | 기상청 단기예보 (getUltraSrtNcst) |
| 테스트 폰 | Samsung SM-S926N (Galaxy S24+, Android 16) |

### Firebase / OAuth 자격증명
| 항목 | 값 |
|---|---|
| 프로젝트 ID | `ontology-metaverse` (Blaze plan) |
| 프로젝트 번호 | `616295277126` |
| 패키지명 | `com.ontology.metaverse` |
| App ID (Android) | `1:616295277126:android:5d4addf6ffdff3374d2047` |
| Web Client ID | `616295277126-qdlqp2bpf0k21pu4surr3lit1g2pobr9.apps.googleusercontent.com` |
| Release SHA-1 | `D6:D3:14:30:FF:CF:5E:79:8D:06:05:36:CD:25:81:8E:F4:A9:A7:36` |
| Cloud Functions 리전 | `asia-northeast3` |
| **검증된 본인 테스트 UID** | `aQxeWJOmJOTwNcxzmIQSAt5Yy8m2` (junseoggim349@gmail.com) |

### GitHub
- **메인 레포**: `katalsye/ontology-metaverse` (owner: 이서윤)
- **본인 계정**: `Oll01`
- **활성 브랜치**:
  - `main` — 안정 (PR #107, #108 머지됨)
  - `feature/weather-collector` — **PR #109 OPEN** (Weather Collector, 머지 대기)
  - `integration/ontology-unity` — 서윤님 통합 브랜치 (3D 씬/UI 통합 중, 절대 손대지 말 것)
  - `integration/ontology-data` — 무성님 Cloud Functions 브랜치

### 팀 분담
| 이름 | 역할 |
|---|---|
| **김준석** (본인) | Android 데이터 수집, 온디바이스 Gemma, SQLite, Firestore 업로드 |
| 김무성 | Cloud Functions Python, 온톨로지 추론 엔진 (core.ttl + SPARQL Rules) |
| 이서윤 | repo owner, Unity 3D 씬, UI, FCM |
| 노성민 | Unity 3D 씬, room object 렌더링 |
| 나머지 3명 | 기획/디자인/문서 |

---

## 2. ✅ 이미 완료된 것 (전부)

### 빌드 환경
- Release Keystore + Google Drive 백업
- Custom Keystore, IL2CPP/arm64/Release 빌드 검증
- AndroidManifest 권한 전체 (`PACKAGE_USAGE_STATS`, `ACCESS_FINE_LOCATION`, `INTERNET`, `RECEIVE_BOOT_COMPLETED`, `POST_NOTIFICATIONS` 등)
- `google-services.json` 통합, FirebaseApp 자동 초기화

### Firebase Auth & 초기화 순서
- `FirebaseBootstrap` 컴포넌트 — `CheckAndFixDependenciesAsync` 한 번 + `RunWhenReady(callback)` 패턴
- 모든 Manager가 `FirebaseBootstrap.RunWhenReady(Init)` 사용 → init race 해결
- Google Sign-In 플러그인, AuthConfig.asset, ID Token 교환 전부 정상
- 자동 로그인 (재실행 시 즉시 UID 복원)

### 데이터 수집 자동화 (P3 시리즈, PR #107 머지 완료)
- `BatchScheduler` — 60s 주기 수집, 60s 주기 batch (이번 PR #109에서 튜닝)
- 수집기:
  - `LocationCollector` — GPS (Unity LocationService)
  - `StepCollector` — Android Step Counter 센서
  - `AppUsageCollector` — PACKAGE_USAGE_STATS (UsageStatsManager)
  - `GalleryEXIFCollector` — MetadataExtractor로 갤러리 EXIF (GPS/시각)
  - **`WeatherCollector`** — 기상청 단기예보 API (이번 PR #109 신규)
- `RawDataToTripleConverter` — type별 raw → triples (gps/step/app_usage/exif/weather/sleep)
- `TempTripleManager` — Firestore `temp_triples/{uid}/items`로 업로드 + User 타입 선언

### SQLite 4-테이블 스키마
| 테이블 | 역할 |
|---|---|
| `RawData` | 수집한 원본 (Type, Content, Timestamp, Processed) |
| `Triple` | 추출된 트리플 (Subject/Predicate/Object/Datatype/Source/Synced) |
| `ImageCache` | 이미지 캐시 |
| `InferenceQueueItem` | 추론 대기 큐 |

### 트리플 명세 v3 (PR #97 머지됨)
- Subject/Object: full URI (`http://7team.dev/ontology#...`)
- Predicate: `prod:` 약식 허용
- `TripleValidator` 통과한 것만 SQLite 저장
- URI 정규화 (LLM 노이즈 흡수)

### Weather Collector (PR #109, 이번 작업) ⭐
- 기상청 공공데이터포털 API 키 발급 완료
- `WeatherCollector.cs`, `GridConverter.cs` (LCC 투영), `KmaApiClient.cs`, `WeatherCondition.cs`
- `RawDataToTripleConverter.ConvertWeather` 추가 — 트리플 4종:
  - `(user, prod:hasWeather, weather_X)`
  - `(weather_X, prod:temperature, ...^^xsd:float)`
  - `(weather_X, prod:condition, ...^^xsd:string)`
  - `(weather_X, prod:recordedAt, ...^^xsd:dateTime)`
- BatchScheduler 통합, 60분 주기 수집
- **E2E 검증 완료**: 실기기 → 기상청 API → SQLite → batch → Firestore에서 4개 트리플 확인

### 배치 파이프라인 튜닝 (PR #109)
- `batchSize`: 10 → **20** (RawDataToTripleConverter)
- `batchIntervalSeconds`: 120s → **60s** (BatchScheduler)
- 이유: 수집 12 raws/min vs 기존 처리 5/min → backlog 누적. 새 설정 20/min 처리로 -8/min catchup

### Manager Singleton 패턴 (PR #109, 임시)
- UserManager / DiaryManager / FollowManager / QuestManager / RewardManager
- `public static Instance` + DontDestroyOnLoad + FirebaseBootstrap.RunWhenReady
- Stub 메서드: `FollowManager.CheckIsFollowing`, `UserManager.GetUserProfileForFollow`
- 서윤님 UI 코드(PR #99)가 `XxxManager.Instance` 패턴 가정 → 빌드 위해 필요
- 추후 서윤님이 integration/ontology-unity 머지 시 정식 구현으로 대체

### UsageStatsCollector 중복 제거 (PR #108 머지됨)
- AppUsageCollector와 중복이라 삭제 (서윤님과 카톡 합의 후)

### 한글 폰트 (P2 완료)
- Pretendard-Regular SDF 생성 + TMP 폰트 등록

### Cloud Functions (무성님, 운영 중)
- `on_new_triple_written` 트리거 (asia-northeast3) — temp_triples 쓰면 자동 추론
- `quests`, `room_objects`, `persona` 컬렉션 생성
- ⚠️ 무성님 본인 uid에선 잘 됨, 본인(aQxe...) uid에선 onWrite 미발동 이슈 있음 — 무성님께 fix 요청 보냄, 대기 중

---

## 3. 🔥 당장 진행해야 할 것 (우선순위 순)

### 🔥 P0. PR #109 상태 확인 + 머지 대기 (지금)
- **URL**: https://github.com/katalsye/ontology-metaverse/pull/109
- **상태**: OPEN, 본인 푸시 직후, 리뷰어 미지정
- **할 일**:
  1. 카톡으로 서윤님(repo owner)에게 PR 머지 요청
  2. 충돌 없는지 확인 (지금 시점에선 main 위에 깔끔히 올라감)
  3. 머지되면 `git checkout main && git pull && git branch -d feature/weather-collector` (로컬 정리)

### 🔥 P1. 서윤님께 firestore.rules 추가 요청 (카톡 1통, 5분)
**이슈**: FCM 토큰 저장 시 `PERMISSION_DENIED`
```
Firestore: Write failed at users/{uid}/fcmTokens/{tokenId}: PERMISSION_DENIED
```

**원인**: integration/ontology-unity 브랜치의 `firestore.rules`에 `fcmTokens` 서브컬렉션 매치가 없음. 서윤님 FCM 토큰 저장 코드는 있는데 rules 빠뜨림. main도 동일 상태.

**카톡 문구**:
```
서윤님 자동 수집 돌리는데 FCM 토큰 저장에서
PERMISSION_DENIED 떠요.

users/{uid}/fcmTokens/{tokenId} 쓰는 코드는 있는데
firestore.rules에 그 서브컬렉션 매치가 없어서요.

rules에 이거 한 줄 추가하면 될 것 같은데
시간 되실 때 봐주실 수 있나요?

match /users/{uid}/fcmTokens/{tokenId} {
  allow read, write: if request.auth != null
                     && request.auth.uid == uid;
}

지금은 자동 수집/추론에는 영향 없는데
나중에 퀘스트 푸시 알림 보내려면 필요할 것 같아요.
```

**현재 영향**: 자동 수집/추론/Firestore 업로드 전부 정상. **FCM 푸시 알림 수신만 안 됨**. 발표 데모엔 무관.

### 🔥 P2. 무성님 onWrite Rule 7 (IndoorDayPattern) 검증 (의존성: 무성님)
- Weather 트리플 4개가 Firestore에 들어간 거 검증 완료 (오늘 10:38 기준)
- 무성님이 본인 uid(`aQxeWJOmJOTwNcxzmIQSAt5Yy8m2`)에서 onWrite 미발동 fix 했는지 확인 필요
- fix 완료되면 Rule 7 (rain or 겨울 + 외출 2곳 미만 → IndoorDayPattern + 실내 활동 퀘스트) 발동 확인
- Firestore Console에서 `quests/{uid}/userQuests` 또는 `room_objects/{uid}/objects`에 결과 떴는지 검증

---

## 4. 📋 앞으로 해야 할 것 (마감 D-day 기준)

### 🟡 발표 전 (~D-13, 6/20)

#### A. 발표 데모 시나리오 준비 (1~2일)
- 실기기에서 자동 수집 → 추론 → 3D 방 오브젝트 배치 시연 동영상
- 발표용 5p 한글 보고서
- PPT 슬라이드
- 라이브 시연 / 녹화 결정 (네트워크 문제 대비)

#### B. (선택) 추가 데이터 수집기
- **Health Connect API 통합** — Galaxy Watch가 있으면 정확한 수면/걸음 데이터
- **Reverse Geocoding** — GPS lat/lng → 장소명 (카페/공원/도서관) 매핑. 무성님 Rule 4 (PlaceHabit), Rule 5 (인과 추론) 활성화에 중요
  - 옵션 A: Google Places API (유료 가능성)
  - 옵션 B: 카카오 로컬 REST API (무료)
  - 옵션 C: OpenStreetMap Nominatim (무료, rate limit)

#### C. (선택) 한글 LLM Triple 추출 (옵션 B, Gemma 3n)
- 일기 텍스트 → Gemma 3n on-device → Triple JSON
- MediaPipe tasks-genai 통합 (already in stack)
- 모델 파일 Firebase Storage 다운로드
- `TextTripleExtractor.cs` (이미 일부 작성됨, integration 브랜치)
- ⚠️ 발표 데모엔 필수 아님. 시간 남으면.

### 🔴 SW 등록 (~D-23, 6/30)

#### D. 한국저작권위원회 SW 저작권 등록
- 소스코드 인쇄본 + 등록 신청서
- 김무성/김준석 공동 저작권
- 1일 작업

### ⭐ 보너스 (시간 매우 남으면)
- Firebase Cloud Messaging 푸시 알림 실제 발송 (P1 fcmTokens rules 해결 후)
- 외부 API (캘린더 통합)
- 다른 도시 좌표 자동 (현재는 대구 35.83, 128.54 하드코딩 fallback)

---

## 5. 📁 파일 경로 인덱스

### 확실히 알고 있는 경로
| 항목 | 경로 |
|---|---|
| **프로젝트 루트** | `C:\Users\USER\Desktop\ontology-metaverse\` |
| Unity 프로젝트 | `Unity/` |
| AuthConfig | `Unity/Assets/02_Scripts/Config/AuthConfig.asset` |
| Release Keystore | `C:\Users\USER\Desktop\keystore\ontology-release.keystore` (alias `ontology`) |
| 빌드 APK 출력 | `C:\Users\USER\Desktop\build\ontology.apk` |
| Gemma 3n 모델 | `C:\Users\USER\Desktop\gemma-model\gemma-3n-E2B-it-int4.task` (2.91GB) |
| google-services.json | `Unity/Assets/google-services.json` |
| AndroidManifest | `Unity/Assets/Plugins/Android/AndroidManifest.xml` |

### 데이터 수집 (이번 작업 핵심)
| 파일 | 경로 |
|---|---|
| BatchScheduler | `Unity/Assets/02_Scripts/DataCollection/BatchScheduler.cs` |
| RawDataToTripleConverter | `Unity/Assets/02_Scripts/DataCollection/RawDataToTripleConverter.cs` |
| LocationCollector | `Unity/Assets/02_Scripts/DataCollection/Location/LocationCollector.cs` |
| StepCollector | `Unity/Assets/02_Scripts/DataCollection/Step/StepCollector.cs` |
| AppUsageCollector | `Unity/Assets/02_Scripts/DataCollection/AppUsage/AppUsageCollector.cs` |
| GalleryEXIFCollector | `Unity/Assets/02_Scripts/DataCollection/Gallery/GalleryEXIFCollector.cs` |
| **WeatherCollector** | `Unity/Assets/02_Scripts/DataCollection/Weather/WeatherCollector.cs` |
| GridConverter (LCC) | `Unity/Assets/02_Scripts/DataCollection/Weather/GridConverter.cs` |
| KmaApiClient | `Unity/Assets/02_Scripts/DataCollection/Weather/KmaApiClient.cs` |
| WeatherCondition | `Unity/Assets/02_Scripts/DataCollection/Weather/WeatherCondition.cs` |
| SQLiteManager | `Unity/Assets/02_Scripts/DataCollection/SQLite/SQLiteManager.cs` |

### Firebase 매니저
| 파일 | 경로 |
|---|---|
| FirebaseBootstrap | `Unity/Assets/02_Scripts/Firebase/FirebaseBootstrap.cs` |
| UserManager | `Unity/Assets/02_Scripts/Firebase/Auth/UserManager.cs` |
| TempTripleManager | (어딘가에 있음, grep으로 찾기) |
| FcmManager | `Unity/Assets/02_Scripts/Firebase/` 어딘가 |
| DiaryManager | `Unity/Assets/02_Scripts/Firebase/Manager/DiaryManager.cs` |
| FollowManager | `Unity/Assets/02_Scripts/Firebase/Manager/FollowManager.cs` |
| QuestManager | `Unity/Assets/02_Scripts/Firebase/Manager/QuestManager.cs` |
| RewardManager | `Unity/Assets/02_Scripts/Firebase/Manager/RewardManager.cs` |

### Scene
| 파일 | 경로 |
|---|---|
| 현재 사용 중 Scene | `Unity/Assets/_Recovery/0.unity` (Build Settings index 0) |
| BootScene | `Unity/Assets/Scenes/BootScene.unity` (서윤님 영역, 안 씀) |
| MainScene | `Unity/Assets/Scenes/MainScene.unity` (서윤님 영역, 안 씀) |

### Scene 내 핵심 GameObject 구성 (`_Recovery/0.unity`)
- `_AutoCollect` — BatchScheduler + 모든 Collector 컴포넌트
- `_Managers` — UserManager, FcmManager, QuestManager, RewardManager, DiaryManager, FollowManager
- `TempTripleManager` — 별도 GameObject (TempTripleManager 컴포넌트)
- `FirebaseBootstrap` 컴포넌트는 어딘가의 GameObject에 부착

### Claude Code 시작 시 추천 명령
```bash
cd C:\Users\USER\Desktop\ontology-metaverse

# 현재 상태 파악
git status
git log --oneline -10
git branch -a

# PR 현황
gh pr list --repo katalsye/ontology-metaverse

# 무성님 영역
ls Functions/
```

---

## 6. 🚧 알려진 비-blocking 이슈

| 이슈 | 영향 | 대응 |
|---|---|---|
| FCM 토큰 PERMISSION_DENIED | 푸시 알림만 못 받음 | 서윤님 rules 추가 대기 (P1) |
| 무성님 onWrite 미발동 (본인 uid) | Rule 추론 결과 안 옴 | 무성님 fix 대기 (P2) |
| AuthConfig 경고 | 무영향 | 무시 |
| `ashmem Pinning deprecated` | 시스템 경고 | 무시 |
| `ClassLoaderContext mismatch` | Unity Gradle 잔재 | 무시 |
| `No AppCheckProvider installed` | 프로덕션 권장 | 무시 (학교 프로젝트) |
| `userfaultfd: MOVE ioctl unsupported` | 폰 OS 한정 | 무시 |
| `GoogleApiManager DEVELOPER_ERROR` | gms providerinstaller | 무시 |
| Inspector 한글 로그 깨짐 (logcat) | 인코딩만 깨짐 | 영문 grep으로 우회 |

---

## 7. 👤 사용자(김준석) 작업 스타일

- **빨리빨리**: 사이드 트랙 최소화, 결정 못 박기
- **단계별 명시적 가이드**: 추측 X, 구체적 단계 ✓
- **복붙 가능한 전체 코드**: 부분 스니펫 X, 전체 함수/파일 ✓
- **한국어 OK** (변수명/주석은 영어 권장)
- **본질 짚는 질문 잘함**: "이거 진짜 원인이 맞아?" 같은 검증 환영
- **결정한 건 못 박기**: 한 번 정한 아키텍처는 다시 안 흔들기
- **commit에 Co-Authored-By Claude 라인 안 넣음** (졸업과제 평가 의식)
- **commit/push 직전 확인 요청** (hard-to-reverse 작업 신중)

---

## 8. 🎯 이번 세션 (2026-06-07) 끝낸 일 요약

1. ✅ Weather Collector 4파일 작성 + 통합 (어제 시작)
2. ✅ 기상청 API 키 발급 + Inspector 입력
3. ✅ Scene GameObject 구성 (_AutoCollect, _Managers, TempTripleManager)
4. ✅ Manager 5개 singleton 패턴 추가 (서윤님 UI 컴파일 지원)
5. ✅ batchSize 10→20, interval 120s→60s 튜닝 (backlog 누적 방지)
6. ✅ 실기기 E2E 검증 — 어제 끝나고 자고 일어나서 오늘 10:38 raw[232] type=weather 처리 확인
7. ✅ Firebase Console에서 weather 트리플 4개 (hasWeather/temperature/condition/recordedAt) 직접 확인
8. ✅ commit + push + PR #109 생성 (main 타겟)

## 9. ⏭️ 다음 세션 첫 액션 (1줄 요약)

1. **카톡 2통 보내기** — 서윤님(firestore.rules fcmTokens), 무성님(PR #109 알림 + onWrite 검증 요청)
2. **PR #109 머지 확인** — 서윤님 머지 → 로컬 main pull
3. **무성님 Rule 7 발동 검증** — Firestore Console에서 quests/room_objects 결과 확인
4. **(시간 되면) Reverse Geocoding 시작** — 무성님 Rule 4/5 활성화 필요

---

**이 문서를 새 채팅 첫 메시지로 던지면 컨텍스트 잡힘. 화이팅 🚀**
