# scripts/ — 통합 테스트용 디버깅 도구

## check_inference.py

김준석-김무성 1차 통합 테스트용 Firestore 상태 확인 + HTTP infer 호출 도구.

### 사전 조건

```bash
# ADC 인증 (로컬 실행 시)
firebase login
# 또는
export GOOGLE_APPLICATION_CREDENTIALS=/path/to/serviceAccountKey.json
```

### 사용 예시

**1. temp_triples 상태만 확인** — 김준석 "데이터 올렸어요" 알림 직후

```bash
cd Functions
python scripts/check_inference.py user_001
```

출력 예시:
```
==================================================
  temp_triples 상태
==================================================
✓ uid=user_001 — temp_triples 트리플 수: 12

  [처음 5개 미리보기]
  1. 'http://7team.dev/ontology#user_001'  —[slept]→  '6.5'
  2. 'http://7team.dev/ontology#user_001'  —[walked]→  '2800'
  ...
✓ 모든 subject가 절대 URI 형식
✓ 모든 predicate가 core.ttl에 정의됨
```

**2. 전체 흐름 실행** — temp_triples 확인 → infer 호출 → 결과 조회

```bash
python scripts/check_inference.py user_001 https://REGION-PROJECT.cloudfunctions.net/infer
```

**3. 결과만 조회** — 이미 추론 완료 후 quests/room_objects/persona 확인

```bash
python scripts/check_inference.py user_001 --results-only
```

---

### 김준석 1차 통합 테스트 흐름

| 단계 | 담당 | 명령 |
|------|------|------|
| 1. 트리플 업로드 | 김준석 | Android → Firestore |
| 2. 업로드 확인 | 김무성 | `python scripts/check_inference.py <uid>` |
| 3. 추론 + 결과 확인 | 김무성 | `python scripts/check_inference.py <uid> <endpoint>` |
| 4. 결과 공유 | 김무성 | quests/persona 내용 슬랙 공유 |

### 주의사항

- predicate 경고(`⚠ 미정의 predicate`)가 있으면 해당 트리플은 추론에서 제외됨
- subject URI 경고(`⚠ subject가 절대 URI 아님`)는 Gemma 3n 트리플 생성 로직 확인 필요
- `--results-only` 모드는 Firebase 인증만 필요하며 추론을 재실행하지 않음
