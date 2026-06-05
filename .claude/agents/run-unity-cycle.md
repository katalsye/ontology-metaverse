# Unity 연동 사이클 실행 스킬

## 사용법
이 스킬을 실행하면 unity-liaison 에이전트가 1회 사이클을 수행한다.

## 실행 단계
1. unity→ontology 오픈 이슈 확인
2. 이슈 있으면 unity-liaison 에이전트 호출
3. 이슈 없으면 대기 상태 보고

## 실행 명령

OPEN_ISSUE=$(gh issue list --repo katalsye/ontology-metaverse --label "unity→ontology" --state open --json number --jq '.[0].number // empty')

if [ -z "$OPEN_ISSUE" ]; then
  echo "unity→ontology 오픈 이슈 없음. 대기 중."
  echo ""
  echo "현재 ontology→unity 오픈 이슈:"
  gh issue list --repo katalsye/ontology-metaverse --label "ontology→unity" --state open
else
  echo "처리할 이슈: #$OPEN_ISSUE"
  gh issue view $OPEN_ISSUE --repo katalsye/ontology-metaverse
fi

## 사이클 완료 후 출력할 것
- 처리한 이슈 번호와 제목
- 변경 파일 목록
- 검증 결과 요약
- 후속 이슈 생성 여부
- 현재 열린 이슈 목록 (양방향)