# Unity 에디터 2개 동시 실행 — 빌드 백엔드 안정화

작성: 2026-09-08 · 작성 맥락: 땅굴 크루 비주얼 오버홀 배치 2 작업 중 발생
전제: **에디터 2개 동시 실행은 바꿀 수 없는 조건이다.** 두 프로젝트를 병렬로 진행하므로
"하나만 열어라" 는 해결책이 아니다. 이 문서는 그 전제 아래의 대책만 다룬다.

대상 환경:

- **두 프로젝트의 에디터 버전이 다르다. 절대 섞지 말 것.** (2026-09-08 실측)
  - `TunnelCrew` — Unity `6000.3.15f1`, URP 17.3.0 (정션 `C:\Users\Loadcomplete\TunnelCrew`)
  - `SlimeForge` — Unity `6000.0.69f1` (`C:\Users\Loadcomplete\SlimeForge`)
  - SlimeForge 를 `6000.3.15f1` 로 열면 상위 버전으로 강제 업그레이드되어 전체 재임포트와
    직렬화 변경이 일어난다. 되돌리기 어렵다.
- MCP for Unity `10.1.2` 브리지가 두 인스턴스에 각각 붙는다

---

## 1. 증상

에디터 콘솔에 다음이 뜨고 플레이 진입이 막힌다.

```
Internal build system error. Read the full binlog without getting a BuildFinishedMessage.
The backend process appears to still be running.

All compiler errors have to be fixed before you can enter playmode!
UnityEditor.SceneView:ShowCompileErrorNotification ()
```

## 2. 오진하기 쉬운 지점 — 실제로는 컴파일 오류가 아니다

2026-09-08 발생 시 실측한 값이다.

| 확인 항목 | 실측 |
|---|---|
| C# 컴파일 오류 | **0개** (콘솔 오류는 `Internal build system error` 하나뿐) |
| `UnityEditor.EditorUtility.scriptCompilationFailed` | **False** |
| `EditorApplication.isCompiling` / `isUpdating` | 둘 다 False |
| 어셈블리 로드 | 프로젝트 어셈블리 5개 전부 로드됨 |
| `Library/ScriptAssemblies/*.dll` | 정상 존재 |
| 디스크 여유 | 302GB (부족 아님) |
| `bee_backend.exe` | 실행 중인 것 **없음** (메시지와 달리 남아 있지 않았다) |

즉 **Bee 빌드 백엔드가 한 번 중단되면서 오류 하나를 남기고, 그때 SceneView 에 붙은
`ShowCompileErrorNotification` 알림이 지워지지 않은 채 계속 떠 있는 상태**다. 알림이 남아
있으면 실제 컴파일 상태와 무관하게 플레이가 막힌 것처럼 보인다.

### 2.1 오진하지 말 것 — DLL 타임스탬프 차이는 정상이다

`TunnelCrew.Presentation.dll`(14:12)과 `TunnelCrew.Editor.dll`(14:09)의 시각이 달라
"의존 어셈블리가 재빌드되지 못했다" 고 판단했는데 **틀린 추론이었다.** Unity/Bee 는 내용
해시로 판단해 결과가 같은 DLL 을 다시 쓰지 않는다. 어셈블리 참조도 이름으로 해석되므로
타임스탬프 차이 자체는 문제의 근거가 되지 않는다.

### 2.2 오진하지 말 것 — 크래시 리포트와 Editor.log 는 다른 프로젝트 것일 수 있다

`%LOCALAPPDATA%\Unity\Editor\Editor.log` 는 **에디터 인스턴스 하나만** 쓴다. 두 개를
띄우면 어느 쪽이 그 파일을 잡았는지 알 수 없다. 실제로 그 로그를 열었을 때
`Successfully changed project path to: C:\Users\Loadcomplete\SlimeForge` 였고,
같은 시각의 UIElements 렌더링 크래시도 SlimeForge 쪽이었다. TunnelCrew 문제를 진단하는데
SlimeForge 로그를 읽고 있던 것이다.

**이것이 이 문서의 1순위 대책이 로그 분리인 이유다.**

## 3. 즉시 복구 절차 (증상이 떴을 때)

파괴적 조작 없이 이 순서로 복구된 것을 확인했다.

1. **컴파일 상태를 먼저 확인한다** — 정말 컴파일 오류인지 가른다.
   ```csharp
   // Editor 스크립트 또는 MCP execute_code
   var p = typeof(UnityEditor.EditorUtility).GetProperty("scriptCompilationFailed",
       System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic
       | System.Reflection.BindingFlags.Public);
   Debug.Log($"scriptCompilationFailed = {p.GetValue(null)} · isCompiling = {EditorApplication.isCompiling}");
   ```
   `False` 면 컴파일 문제가 아니다 → 2번으로.

2. **전체 재컴파일을 강제해 백엔드가 완주하는지 본다.**
   ```csharp
   UnityEditor.Compilation.CompilationPipeline.RequestScriptCompilation();
   ```
   완주하면(콘솔에 같은 오류가 다시 안 뜨면) Bee 상태가 깨진 것이 아니라 일시 중단이었다.

3. **잔여 알림을 지운다.** 이것을 안 하면 상태가 정상이어도 계속 막힌 것처럼 보인다.
   ```csharp
   foreach (var sv in UnityEditor.SceneView.sceneViews)
       { var v = sv as UnityEditor.SceneView; v?.RemoveNotification(); v?.Repaint(); }
   var gv = System.Type.GetType("UnityEditor.GameView, UnityEditor");
   if (gv != null)
       foreach (var w in Resources.FindObjectsOfTypeAll(gv))
           { var ew = w as EditorWindow; ew?.RemoveNotification(); ew?.Repaint(); }
   ```

4. 그래도 재발하면 **에디터를 닫고** `<프로젝트>/Library/Bee` 를 삭제한 뒤 다시 연다.
   (에디터가 열려 있는 동안 삭제하지 말 것 — Unity 가 잡고 있다.)

## 4. 재발 방지 대책

우선순위 순. 위쪽이 효과 대비 비용이 좋다.

### 4.1 [1순위] 에디터 로그를 프로젝트별로 분리한다

**문제**: 두 인스턴스가 같은 `Editor.log` 를 노려 하나만 잡는다. 진단이 불가능해지고,
파일 잠금 경쟁도 생긴다.

**대책**: Unity Hub 대신 `Unity.exe` 를 직접 실행하고 `-logFile` 로 로그 경로를 분리한다.
프로젝트마다 바로 가기(.lnk) 또는 .bat 를 만들어 그것으로만 연다.

```bat
:: open-tunnelcrew.bat
"C:\Program Files\Unity\Hub\Editor\6000.3.15f1\Editor\Unity.exe" ^
  -projectPath "C:\Users\Loadcomplete\TunnelCrew" ^
  -logFile "C:\Users\Loadcomplete\TunnelCrew\Logs\Editor-TunnelCrew.log"
```

```bat
:: open-slimeforge.bat
:: 주의: SlimeForge 는 6000.0.69f1 이다. 6000.3.15f1 로 열면 프로젝트가 업그레이드된다.
"C:\Program Files\Unity\Hub\Editor\6000.0.69f1\Editor\Unity.exe" ^
  -projectPath "C:\Users\Loadcomplete\SlimeForge" ^
  -logFile "C:\Users\Loadcomplete\SlimeForge\Logs\Editor-SlimeForge.log"
```

**상태**: 위 두 파일은 2026-09-08 에 `C:\Users\Loadcomplete\Desktop\` 에 생성했다
(`open-slimeforge.bat`, `open-tunnelcrew.bat`). 에디터 실행 파일 경로와 프로젝트 경로는
실재 확인했다. 앞으로 Hub 대신 이 두 파일로만 연다.

주의: TunnelCrew 는 한글 경로 cp949 문제 때문에 **정션 경로로 열어야 한다**
(`C:\Users\Loadcomplete\TunnelCrew`). 실제 경로로 열면 MCP 가 인식하지 못한다.

**검증**: 두 에디터를 띄운 뒤 각 로그 파일 첫 100줄에서 `changed project path to` 가
서로 다른 프로젝트를 가리키는지 확인한다.

### 4.2 [1순위] 자동 복구 에디터 스크립트를 넣는다

**문제**: 근본 원인을 없애기 어렵다면(동시 실행이 전제) 최소한 사람이 원인을 헤매지
않아야 한다. 3장의 절차는 전부 자동화할 수 있다.

**구현 스펙** — 각 프로젝트에 `Assets/_Project/Editor/BuildBackendWatchdog.cs` 로 넣는다.

동작:

1. `[InitializeOnLoadMethod]` 로 `Application.logMessageReceived` 를 구독한다.
2. 메시지에 `Internal build system error` 가 포함되면:
   - `scriptCompilationFailed` 를 읽어 **실제 컴파일 오류가 아닌 것을 확인**하고,
   - SceneView/GameView 의 알림을 지우고,
   - `CompilationPipeline.RequestScriptCompilation()` 을 **한 번만** 요청한다
     (재시도 루프를 만들면 무한 재컴파일이 된다 — 세션당 1회로 제한할 것).
   - 콘솔에 `[빌드백엔드] 일시 중단을 감지해 재컴파일을 요청했다` 를 남긴다.
3. `EditorApplication.playModeStateChanged` 에서 진입이 거부됐는데
   `scriptCompilationFailed == false` 면 같은 복구를 수행한다.
4. 재시도 횟수와 마지막 발생 시각을 `SessionState` 에 기록해 로그로 남긴다
   (빈도를 알아야 4.3~4.5 중 무엇이 효과 있었는지 판단할 수 있다).

주의할 점:

- **실제 컴파일 오류일 때는 절대 알림을 지우지 말 것.** 그러면 진짜 오류가 숨는다.
  `scriptCompilationFailed == true` 면 아무것도 하지 않고 그대로 둔다.
- 재컴파일 요청은 도메인 리로드를 유발한다. 플레이 중에는 하지 말 것.
- `#if UNITY_EDITOR` 안에, Editor 어셈블리에 둔다.

### 4.3 [2순위] 임포트 워커 수를 프로젝트별로 절반씩 나눈다

**문제**: 에디터마다 `AssetImportWorker` 를 CPU 코어 수 기준으로 띄운다. 관측 시
TunnelCrew 만으로도 워커 5개(`AssetImportWorker0~4`)가 있었다. 두 프로젝트가 동시에
임포트하면 CPU·IO 가 포화되고, 그 상태에서 Bee 백엔드가 응답 시간을 넘겨 중단된다.

**대책**: 각 프로젝트의 워커 수를 코어 수의 절반 이하로 내린다.

- Project Settings → **Editor** → Asset Pipeline 항목에서 워커 수(또는 비율) 설정
- 또는 실행 인자 `-importWorkerCount <n>`

> **확인 필요**: 6000.3 에서 이 설정의 정확한 이름과 위치, 그리고
> `-importWorkerCount` 인자가 유효한지 확인할 것. 버전에 따라
> Preferences 쪽에 있거나 "Import Worker Count Percentage" 로 표기된다.

**검증**: 설정 후 `tasklist | findstr AssetImportWorker` 로 프로젝트당 워커 수를 센다.
두 프로젝트 합계가 논리 코어 수를 넘지 않게 맞춘다.

### 4.4 [2순위] Windows Defender 실시간 검사에서 빌드 폴더를 제외한다

**문제**: Bee 는 `Library/Bee` 아래에 수천 개의 중간 파일을 쓴다. 실시간 검사가 그 파일을
잠그면 백엔드가 쓰기에 실패하고 중단된다. 에디터가 2개면 그 빈도가 2배가 된다.

**대책**: 다음을 제외 경로로 추가한다(Windows 보안 → 바이러스 및 위협 방지 → 제외).

```
C:\Users\Loadcomplete\TunnelCrew\Library
C:\Users\Loadcomplete\TunnelCrew\Temp
C:\Users\Loadcomplete\SlimeForge\Library
C:\Users\Loadcomplete\SlimeForge\Temp
C:\Users\Loadcomplete\AppData\Local\Unity
C:\Program Files\Unity\Hub\Editor
```

프로세스 제외도 함께: `Unity.exe`, `bee_backend.exe`, `Unity.ILPP.Runner.exe`,
`UnityShaderCompiler.exe`, `AssetImportWorker.exe`.

> 정션으로 여는 경우 **정션 경로와 실제 경로를 모두** 넣어야 한다.
> 실제 경로: `C:\Users\Loadcomplete\Documents\ChatGPT\땅굴 크루 만들기\unity\TunnelCrew\Library`

**검증**: 제외 적용 전후로 4.2 의 watchdog 가 기록한 발생 빈도를 비교한다.

### 4.5 [3순위] 라이선스 클라이언트 오류를 확인한다

`Logs/AssetImportWorker*.log` 에 다음이 반복된다.

```
[Licensing::Client] Error: Code 404 while processing request
(status: Found 0 entitlement groups and 0 free entitlements matching requested entitlement ids)
```

에디터 2개가 같은 라이선스 클라이언트(`Unity.Licensing.Client.exe`)를 동시에 두드리면서
나는 것으로 보인다. 이 자체가 빌드 중단의 직접 원인인지는 확정하지 못했다.

> **확인 필요**: 이 404 가 정상 동작인지(엔타이틀먼트 조회 실패 후 폴백) 아니면 실제
> 라이선스 경쟁인지 판단할 것. Unity Hub 로그와 `Unity.Licensing.Client` 로그를 함께 볼 것.

### 4.6 [3순위] MCP 인스턴스를 항상 명시적으로 고정한다

**문제**: 브리지가 두 인스턴스에 붙으면 도구 호출이 **엉뚱한 프로젝트로 간다.** 실제로
`TunnelCrew@5795f232` 가 일시적으로 목록에서 사라지고 `SlimeForge@3fece294` 만 남은 구간이
있었고, 그 사이 콘솔 조회가 어느 쪽 것인지 알 수 없었다. 포트도 6400 ↔ 6402 로 바뀌었다.

**대책**: 세션 시작 시 반드시 다음을 먼저 호출한다.

```
set_active_instance("TunnelCrew@<hash>")
```

해시는 `mcpforunity://instances` 리소스로 확인한다. 도구 호출마다 `unity_instance` 인자로
넘기는 방법도 있다. 이것은 빌드 중단을 막지는 못하지만 **진단이 엉뚱한 프로젝트를 보는
것을 막는다** — 이번 사건에서 실제로 시간을 잃은 지점이다.

또한 MCP 릴레이 프로세스가 죽은 기록이 있다:
`Relay process exited (exit code -1073740791)` = `0xC0000409`
(STATUS_STACK_BUFFER_OVERRUN). 재현되면 별도로 다룰 것.

> **확인 필요**: 두 프로젝트가 MCP 브리지 포트를 각각 고정으로 쓰게 설정할 수 있는지
> (`mcp-for-unity` 설정에 포트 지정 옵션이 있는지) 확인할 것. 고정되면 인스턴스 혼동이 없어진다.

### 4.7 [선택] Unity Accelerator / 로컬 캐시 서버

두 프로젝트가 임포트 산출물을 공유 캐시에서 받으면 재임포트 비용이 줄고, 그만큼 IO 경쟁이
줄어든다. 다만 이번 증상의 직접 원인이 캐시라는 근거는 없으므로 후순위다.

> **확인 필요**: 두 프로젝트가 에셋을 얼마나 공유하는지. 공유가 적으면 효과가 없다.

---

## 5. 구현 체크리스트

- [ ] 4.1 프로젝트별 `-logFile` 바로 가기/배치 파일 2개 작성, 이후 그것으로만 실행
- [ ] 4.2 `BuildBackendWatchdog.cs` 를 두 프로젝트에 각각 추가 (세션당 1회 복구 제한 필수)
- [ ] 4.3 임포트 워커 수를 프로젝트당 코어의 절반 이하로 설정 (설정 위치 먼저 확인)
- [ ] 4.4 Defender 제외 경로·프로세스 등록 (정션·실제 경로 모두)
- [ ] 4.5 라이선스 404 원인 확인
- [ ] 4.6 MCP 세션 시작 시 `set_active_instance` 를 규약으로 굳히기, 포트 고정 가능 여부 확인
- [ ] 발생 빈도를 watchdog 로그로 2~3일 관측해 어떤 대책이 실제로 효과 있었는지 판정

## 6. 판정 기준

대책이 효과 있었다고 말할 수 있는 조건:

1. 두 에디터를 동시에 띄우고 양쪽에서 스크립트를 수정·재컴파일하는 작업을
   각 20회 이상 반복했을 때 `Internal build system error` 가 0회.
2. 로그 파일이 프로젝트별로 분리되어, 오류가 나면 어느 프로젝트인지 즉시 판별 가능.
3. watchdog 가 자동 복구한 기록이 남아, 사람이 개입하지 않고도 플레이 진입이 가능.

## 7. 이 문서를 만든 사건의 요약

- 발생: 2026-09-08 14:26 무렵, 비주얼 오버홀 배치 2 작업 직후 플레이 진입 시도
- 같은 시각 SlimeForge 에디터가 UIElements 렌더링 크래시로 죽었다
  (`UnityCrashHandler64.exe` 2개, `UnityBugReporter.log` 14:26 기록)
- TunnelCrew 쪽은 C# 오류 0, 어셈블리 5개 정상 로드, `scriptCompilationFailed = False`
- 3장의 절차(상태 확인 → 강제 재컴파일 → 알림 제거)로 복구, 플레이 진입 정상 확인
  (`VisualLab` 씬에서 윤곽 4 · 캐스터 4 · 접촉 AO 1 · 앵커 1 정상 초기화)
- 코드 수정은 필요하지 않았다 — 배치 2 산출물에 결함이 있어서 생긴 문제가 아니다
