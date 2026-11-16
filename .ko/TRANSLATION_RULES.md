# RHI Korean translation rules

Goal: translate every string a user can SEE in the RHI app UI (WinUI 3, C#) into natural, concise Korean.
Repo: C:\Users\Revaptor_FX\Documents\RHI-ko (project folder RenoDXCommander). Edit files in place with the Edit tool.

## Translate (user-visible)
- XAML attribute values: Text, Content, Header, PlaceholderText, ToolTipService.ToolTip, ToolTip, Title, Description,
  Label, OnContent, OffContent, PrimaryButtonText, SecondaryButtonText, CloseButtonText, Message, and literal text
  inside <TextBlock>/<Run>/<ComboBoxItem>/<x:String> elements.
- C# string literals that end up on screen: assignments to .Text / .Content / .Header / .Title / .Message /
  .PlaceholderText / .Description / ToolTipService.SetToolTip(..), ContentDialog Title/Content/PrimaryButtonText/
  SecondaryButtonText/CloseButtonText, InfoBar messages, status texts, StatusText / label properties on view models,
  ComboBox item display strings, menu item Text, MessageBox text, tooltip text, FAQ/help text, notification text.
- Interpolated strings: translate the text around the holes, keep {expressions} and format specifiers exactly.

## DO NOT translate
- Log / diagnostics: CrashReporter.Log(...), _crashReporter.Log, Debug.WriteLine, Console.WriteLine, Trace, logger
  calls, exception messages that only go to logs.
- Any string used in logic: ==, !=, Equals, switch/case labels, Contains, StartsWith, EndsWith, IndexOf, Replace,
  Split, Regex, dictionary keys / lookups, string comparison of SelectedItem/Content/Tag, JSON property names,
  settings keys, ini keys/sections/values, registry paths, command-line arguments, process names, file names,
  extensions, paths, URLs, GitHub repo names, version strings, enum names, nameof(), x:Name, x:Uid, Tag="...",
  AutomationProperties.AutomationId, style/resource keys, glyph codes.
- BEFORE translating a C# literal, grep the whole RenoDXCommander folder for that exact literal. If it is also used in
  any comparison/lookup/key/serialization, leave it in English. If a displayed value is ALSO stored to settings/json
  (e.g. a ComboBox item string saved by its text), leave it in English.
- Proper/product names stay in English: RHI, ReShade, RenoDX, Luma, OptiScaler, DLSS, DLAA, DLSS 5, Neural Rendering
  method names (GITC Uplift, ShortFuse DLSS Tool, DLSS5 Tool, DLSS5 Feeder, DX11 Bridge, NR Cost Scaler), Streamline,
  NVIDIA, AMD, Intel, XeSS, FSR, DXVK, dgVoodoo2, ReLimiter, Display Commander, Special K, ENB, ASI Loader,
  RTX Encore, MFG, Smooth Motion, G-Sync, VSync, ReBAR, DLDSR, HDR, SDR, RTX HDR, Steam, Epic, GOG, EA App,
  Ubisoft Connect, Xbox, Game Pass, Battle.net, Rockstar, Nexus Mods, PCGamingWiki, Ultra+, Unreal Engine, Unity,
  RE Framework, Vulkan, DirectX, DX9/DX11/DX12, OpenGL, GitHub, Discord, Windows. Keyboard keys (Ctrl, Home, F5).
- Code comments (leave them).

## Glossary (use consistently)
Install 설치 · Uninstall 제거 · Reinstall 재설치 · Update 업데이트 · Update All 모두 업데이트 · Check for updates 업데이트 확인
Settings 설정 · Options 옵션 · Advanced 고급 · General 일반 · Default 기본값 · Custom 사용자 지정 · Reset 초기화
Restore 복원 · Restore Defaults 기본값 복원 · Backup 백업 · Apply 적용 · Quick Apply 빠른 적용 · Save 저장 · Cancel 취소
OK 확인 · Close 닫기 · Yes 예 · No 아니요 · Enable 사용 · Disable 사용 안 함 · Enabled 켜짐 · Disabled 꺼짐 · On 켜짐 · Off 꺼짐
Browse 찾아보기 · Open 열기 · Open folder 폴더 열기 · Launch 실행 · Refresh 새로 고침 · Rescan 다시 검색 · Scan 검색
Search 검색 · Filter 필터 · Sort 정렬 · Hide 숨기기 · Hidden 숨김 · Show 표시 · Favourites/Favorites 즐겨찾기
Game 게임 · Games 게임 · Library 라이브러리 · Collection(s) 컬렉션 · Component(s) 구성 요소 · Addon(s) 애드온
Shader(s) 셰이더 · Shader pack 셰이더 팩 · Preset(s) 프리셋 · Profile 프로필 · Mod 모드 · Extras 추가 기능
Version 버전 · Latest 최신 · Installed 설치됨 · Not installed 설치 안 됨 · Update available 업데이트 있음 · Up to date 최신 상태
Download 다운로드 · Downloading 다운로드 중 · Installing 설치 중 · Uninstalling 제거 중 · Failed 실패 · Error 오류 · Warning 경고
Recommended 권장 · Experimental 실험적 · Stable 안정 · Nightly 나이틀리 · Beta 베타 · Legacy 레거시 · Channel 채널
Frame Generation 프레임 생성 · Super Resolution 슈퍼 해상도 · Ray Reconstruction 레이 재구성 · Upscaler 업스케일러
Render scale 렌더 배율 · Frame limiter 프레임 제한기 · FPS Limit FPS 제한 · Low Latency 저지연 · Power Mode 전원 모드
Driver 드라이버 · Driver settings 드라이버 설정 · Display 디스플레이 · Monitor 모니터 · Resolution 해상도 · Refresh rate 주사율
Peak brightness 최대 밝기 · Tone mapping 톤 매핑 · Hotkey 단축키 · Screenshot 스크린샷 · Overlay 오버레이 · Layout 레이아웃
Theme 테마 · Language 언어 · Achievements 도전 과제 · Patch notes 패치 노트 · Single-player only 싱글플레이 전용
Anti-cheat 안티치트 · Multiplayer 멀티플레이 · Bitness 비트 · 32-bit 32비트 · 64-bit 64비트 · Store 스토어
Folder 폴더 · File 파일 · Path 경로 · Drag and drop 끌어서 놓기 · Detected 감지됨 · Unknown 알 수 없음 · None 없음

Style: short UI Korean (명사형/해요체 혼용 금지 — buttons/labels as nouns or short verbs like "설치", messages in polite
"~합니다/~하세요" style). Keep punctuation like "…" and emoji/glyphs. Keep string length reasonable (buttons short).

## Safety
- Never change identifiers, logic, XAML structure, bindings ({Binding}, {x:Bind}, {StaticResource}), or escape
  sequences. Keep \" and \n intact. In XAML keep &amp; &lt; &#x...; entities valid.
- Do NOT run dotnet build (another process builds later). Do not touch files outside your assigned list.
- If unsure whether a string is shown or used in logic, leave it in English.
- Final report: number of files changed, and any risky strings you deliberately left in English (short list).
