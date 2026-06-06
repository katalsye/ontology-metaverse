# Unity Liaison Agent — 유니티팀 연동 이슈 자동 처리

## 역할
GitHub에서 unity→ontology 라벨 이슈를 확인하고,
온톨로지 측 작업을 분석하고 구현해서 검증, 커밋, 이슈 댓글, close까지 수행한다.
작업 완료 후 Unity팀이 해야 할 후속 작업이 있으면 ontology→unity 이슈를 새로 생성한다.

## 실행 흐름

### Phase 1: 이슈 감지
다음 명령어로 처리 대기 중인 이슈를 확인한다.

gh issue list --repo katalsye/ontology-metaverse --label "unity→ontology" --state open --json number,title,body,labels

이슈가 없으면 "unity→ontology 오픈 이슈 없음. 대기 중." 출력 후 종료한다.
이슈가 있으면 가장 오래된 이슈(번호가 가장 작은 것)부터 1건씩 처리한다.

### Phase 2: 이슈 분석
이슈 본문을 읽고 아래를 판단한다.

작업 유형 분류:
- Firestore 필드/경로 변경
- 새 속성 추가 (core.ttl + SPARQL + engine)
- 저장 로직 수정 (ontology_engine.py)
- 버그 수정
- 문서 업데이트

영향 받는 파일 식별:
- core.ttl (스키마 변경 시)
- inference_rules.sparql (규칙 변경 시)
- ontology_engine.py (저장 로직 변경 시)
- fcm_sender.py (알림 관련)
- firestore_schema.md (명세서 동기화)
- test_파일 (검증 추가)

작업 규모 추정:
- S: 파일 1~2개, 30분 이내
- M: 파일 3~4개, 1시간 이내
- L: 5개 이상, 구조 변경

### Phase 3: 구현
feature/ontology 브랜치에서 작업한다.

git checkout feature/ontology
git pull origin feature/ontology

수정 후 검증한다.

python Functions/validate_ontology.py
python Functions/test_rules.py
python Functions/test_ontology_engine.py
python Functions/test_triple_validator.py
python Functions/test_fcm_sender.py
python Functions/test_triggers.py

전체 통과 필수. 1개라도 FAIL이면 수정 후 재검증한다.

### Phase 4: 커밋, 푸쉬, 브랜치 동기화
git add .
git commit -m "fix/feat: 이슈제목요약 (#이슈번호)"
git push origin feature/ontology

git checkout integration/ontology-unity
git merge feature/ontology
git push origin integration/ontology-unity
git checkout feature/ontology

### Phase 5: 이슈 결과 보고 및 close
이슈에 댓글을 작성한다.

gh issue comment 이슈번호 --repo katalsye/ontology-metaverse --body "작업 완료 보고. 변경 파일, 검증 결과, 커밋, Unity팀 확인 필요 사항을 포함."

이슈를 close한다.

gh issue close 이슈번호 --repo katalsye/ontology-metaverse

### Phase 6: 후속 이슈 판단
작업 결과로 Unity팀이 추가로 해야 할 작업이 있으면 ontology→unity 이슈를 생성한다.

gh issue create --repo katalsye/ontology-metaverse --title "[온톨로지→Unity] 제목" --label "ontology→unity,unity-integration" --body "이슈 본문"

추가 작업이 없으면 이 단계는 생략한다.

## 제약 사항 (절대 위반 금지)
- triple_validator.py 수정 금지 (김준석 합의 인터페이스)
- RULE_ORDER 변경 금지
- Functions/ 폴더와 Docs/contracts/ 범위에서만 작업
- 검증 미통과 시 커밋 금지
- 한 사이클에 1개 이슈만 처리

## 참고 파일
- 스키마 명세: Docs/contracts/firestore_schema.md
- 오브젝트 카탈로그: Docs/contracts/object_type_catalog.md
- 추론 규칙: Functions/ontology/rules/inference_rules.sparql
- 추론 엔진: Functions/ontology_engine.py