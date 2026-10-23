# 봇 설정 v2 회귀 검사

저장소 루트에서 실행합니다.

```powershell
dotnet run --project tests/BotSettingsRegression -c Release
```

- `Program.cs`: 저장 기준 분리·난이도 왕복(225m → 112.5m → 225m → 112.5m), 원격 갱신·삭제, 저장 예외, 바닐라 역할 판정과 탈출 복구를 검사합니다.
- `BoundaryStubs.cs`: 게임 엔진·BigBrain 레이어 저장소·서버 통신을 대신하는 검사 전용 경계입니다. 게임/서버를 실행하거나 API를 호출하지 않습니다.
- 프로젝트는 실제 `PresetSync`(서버 저장본 관리), `SAINEnableClass`(봇에 SAIN을 붙일지 판정), `BigBrainHandler`(행동 레이어 적용), 역할 목록과 공용 계산 코드를 직접 연결합니다.
- 14개 시나리오입니다. 전역 바닐라 검사는 10개 묶음을 모두 돌며 PMC와 모드 역할이 잘못 잠기지 않는지도 확인합니다.
- 엔진 경계를 대체하므로 실제 BigBrain 내부 동작, F6 GUI 이벤트 순서·배치, 웹 화면, 실제 봇 전투·탈출은 게임에서 별도 확인해야 합니다.
- 기존 오프라인 호스트 전체 검사는 별도 사본에서 실행했습니다. 142개 통과, 수정 전부터 있던 실패 `Action_unlinked_holds`, `Doors_suite_runs_and_is_deterministic` 2개는 유지됩니다.
