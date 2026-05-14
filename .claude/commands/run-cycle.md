## 시작 전 미완료 이슈 확인 (최우선)
사이클 시작 시 반드시 아래 순서로 확인:

1. 미완료 이슈 확인
gh issue list --repo katalsye/ontology-metaverse \
  --label ontology --state open

2. open 상태 이슈가 있으면
   → 새 Task 도출하지 말고 기존 미완료 이슈부터 처리
   → 가장 오래된 이슈(번호가 낮은 것)부터 처리

3. open 상태 이슈가 없으면
   → 아키텍트가 새 Task 도출

4. 현재 작업 중이던 파일 확인
git status
git diff --stat
   → 수정된 파일이 있으면 해당 작업 먼저 완료 후 커밋

---

아래 순서대로 서브에이전트를 호출해서 한 사이클을 완주해줘. 중간에 멈추지 마.

## 중복 작업 방지
사이클 시작 전 반드시 확인:
gh issue list --repo katalsye/ontology-metaverse --state closed --label ontology

이미 완료된 이슈와 동일한 작업은 절대 다시 하지 말 것.

1. ontology-architect 에이전트를 써서
   현재 Functions/ontology/ 상태를 분석하고
   다음 Task 1개를 GitHub 이슈로 등록해줘.
   이슈 번호를 기억해.

2. ontology-developer 에이전트를 써서
   방금 등록한 이슈를 구현하고
   validate_ontology.py와 test_rules.py 검증까지 완료해줘.

3. ontology-reviewer 에이전트를 써서
   구현된 코드를 리뷰해줘.

4. ontology-architect 에이전트를 써서
   리뷰 결과를 비평하고 다음 사이클 우선순위를 정해줘.

5. 아래 명령어를 실행해줘:
   git add .
   git commit -m "ontology: [자동] {이슈 제목} (closes #{이슈 번호})"
   git push origin feature/ontology

사이클 완료 후 아래 요약을 출력해줘:
- 구현한 내용 한 줄 요약
- 리뷰에서 발견된 문제점 수
- 다음 사이클 예정 작업
- 트리플 수 변화 (이전 → 현재)

## 커밋 메시지 형식
- 스키마 변경: "ontology: [자동] core.ttl {내용} (closes #{번호})"
- 규칙 추가: "ontology: [자동] Rule {번호} {규칙명} 추가 (closes #{번호})"
- 버그 수정: "fix: [자동] {내용} 수정 (closes #{번호})"
- 테스트 추가: "test: [자동] {내용} (closes #{번호})"

## 작업 로그 기록
각 사이클 완료 후 아래 명령어로 로그를 기록해줘:

echo "## 사이클 완료: $(date)" >> Docs/auto_work_log.md
echo "- 이슈: #{이슈번호} {이슈제목}" >> Docs/auto_work_log.md
echo "- 트리플: {이전} → {현재}" >> Docs/auto_work_log.md
echo "- 문제점: {리뷰어 발견 문제점 수}개" >> Docs/auto_work_log.md
echo "---" >> Docs/auto_work_log.md

## 사이클 연속 실행
요약 출력 후 즉시 다음 사이클을 시작할 것.
멈추지 말고 위의 전체 흐름(미완료 이슈 확인 → 아키텍트 → 개발자 → 리뷰어 → 아키텍트 비평 → 커밋 → 로그)을 반복한다.
