# Mania Timing Bar

**made by 0naire**

osu!mania에서 판정 타이밍을 콤보 아래의 작은 삼각형과 눈금으로 확인하는 Windows용 수동 실행 도구입니다. 긴 색상 막대 대신 흰색 판정 눈금, 분홍색 기준선과 삼각형을 표시해 노트가 덜 가려지게 했습니다.

## 주요 기능

- 실제 플레이의 빠른 입력·늦은 입력을 실시간으로 표시합니다.
- 최근 눈금만 짧게 남기고, 눈금이 과도하게 쌓이지 않도록 개수를 제한합니다.
- 가로 위치, 세로 위치, 눈금 범위를 메뉴에서 조절합니다.
- 현재 4K 스킨의 `ColumnStart`, `ColumnWidth`, `ComboPosition`을 읽어 위치를 맞춥니다.
- 분홍색 포인트의 어두운 메뉴와 위치 미리보기를 제공합니다.
- **자동 시작을 등록하지 않습니다.** 직접 열고 ‘판정 바 시작’을 누른 동안만 동작합니다.
- ‘중지’ 또는 창 닫기로 연결 프로그램과 오버레이를 함께 종료합니다. osu! 자체는 종료하지 않습니다.

## 사용 방법

1. 이 저장소의 **Code → Download ZIP**으로 내려받고 압축을 완전히 풉니다.
2. `Setup.cmd`를 한 번 실행합니다. 공식 tosu 구성 요소 두 개를 내려받아 SHA-256을 확인한 후 준비합니다. 약 185MiB를 다운로드하며, 압축 해제 공간이 추가로 필요합니다.
3. osu!를 켜고 사용할 mania 스킨을 선택합니다.
4. `ManiaTimingBar.exe`를 직접 실행합니다.
5. **스킨에 맞추기 → 판정 바 시작**을 누릅니다.
6. 필요하면 가로·세로 위치와 눈금 범위를 조절합니다. ‘메뉴에서도 위치 미리보기’를 켜면 플레이 전에도 위치를 볼 수 있습니다.

메뉴를 닫으면 도구도 종료됩니다. 최소화하면 계속 동작합니다. 게임 실행이나 Windows 로그인만으로 이 도구가 자동 실행되지는 않습니다.

## 요구 사항과 범위

- Windows 10/11 64비트, .NET Framework 4.8.
- osu!stable의 mania를 대상으로 제작했으며, 현재 4K 스킨에 맞춰 기본 위치를 설정합니다. 다른 키 수와 특수 배치는 직접 조절하세요.
- 판정 데이터를 읽는 연결 부분과 전체화면 표시는 [tosu](https://github.com/tosuapp/tosu)를 사용합니다. 이 프로젝트는 연결 제어 메뉴와 작은 눈금 형태의 오버레이를 제공합니다.
- Windows 환경 및 다른 오버레이와의 조합에 따라 호환성이 달라질 수 있습니다. 게임과 도구의 실행 권한을 동일하게 맞추세요.
- 기존 tosu가 24050 포트를 사용 중이면 먼저 기존 tosu를 종료해야 합니다. 다른 실행 중인 tosu를 이 도구가 임의로 종료하지 않습니다.
- 기존 스킨과 게임 설정 파일을 수정하지 않습니다. 사용자별 설정은 도구 폴더의 `runtime/static/ZeroTimingBar/layout.json`에 저장됩니다.

## 직접 빌드

소스는 `source/ManiaTimingBar.cs`에 있습니다. Windows PowerShell에서 `powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1`을 실행하면 같은 폴더에 실행 파일을 만듭니다. `Setup.cmd`는 구성 요소만 준비하며 프로그램을 자동 실행하지 않습니다. PowerShell 실행 정책은 해당 실행에만 적용합니다.

## 구성 요소와 제작자

- 메뉴·연결 제어·컴팩트 표시: **0naire**
- 데이터 연결 및 전체화면 오버레이: **[tosuapp/tosu](https://github.com/tosuapp/tosu)**, LGPL-3.0. 공식 릴리스 `v4.26.3`을 사용합니다.
- 판정 계산과 눈금 엔진: **[KapiWilq/HitErrorMeter](https://github.com/KapiWilq/HitErrorMeter)**, AGPL-3.0. 관련 원본 소스와 라이선스를 `runtime/static/ZeroTimingBar/vendor/`에 포함했습니다.

웹 오버레이는 위 AGPL 구성 요소를 사용하는 파생 작업으로 해당 조건을 따릅니다. 원저작자 표시는 `made by 0naire`와 별도로 유지합니다. `THIRD-PARTY-NOTICES.md`를 참고하세요.
