// MainWindow.FaqBuilder.cs — Builds the FAQ/Quick Start guide content dynamically.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace RenoDXCommander;

public sealed partial class MainWindow
{
    private bool _faqBuilt;

    /// <summary>
    /// Builds the FAQ panel content. Called once when FAQ is first opened.
    /// </summary>
    private void BuildFaqContent()
    {
        if (_faqBuilt) return;
        _faqBuilt = true;

        var panel = FaqContentPanel;
        panel.Children.Clear();

        // Welcome section
        panel.Children.Add(BuildFaqSection(
            null, "RHI에 오신 것을 환영합니다", "AccentTealBrush",
            "RHI는 PC에 있는 게임을 찾아 HDR 모드, ReShade, 셰이더, 프레임 제한기, DLSS 파일 등을 설치·업데이트·제거하고, NVIDIA 드라이버 설정까지 한곳에서 관리합니다. 시작하는 방법은 다음과 같습니다.",
            "권장 첫 단계: 설정 → 계정 및 데이터 → GitHub 토큰에서 무료 GitHub 토큰을 추가하세요. 토큰이 없으면 GitHub는 시간당 60회 요청만 허용하므로 다운로드와 업데이트 확인이 실패할 수 있습니다. github.com/settings/tokens에서 아무 항목도 선택하지 않은 클래식 토큰을 만들어 붙여 넣고 저장을 클릭하세요."));

        // Step 1: Select a Game
        panel.Children.Add(BuildFaqStep(1,
            "게임 선택",
            "게임은 왼쪽에 나열됩니다. 게임을 클릭하면 상세 패널이 열립니다. 검색 상자와 필터 칩(모든 게임, 설치됨, 즐겨찾기, Unreal, Unity, RenoDX, Luma…)으로 게임을 찾을 수 있습니다.\n\n상세 패널은 구성 요소, 게임 재정의, Neural Rendering, DLSS / Streamline, 드라이버 설정, 관리, 추가 기능 섹션으로 나뉩니다. 섹션 제목을 클릭하면 접히고, ≡ 핸들을 끌면 순서를 바꿀 수 있습니다.",
            "게임을 두 번 클릭하면 실행됩니다. 게임을 마우스 오른쪽 버튼으로 클릭하면 폴더 열기, 즐겨찾기, 숨기기, 바탕 화면 바로 가기 만들기를 할 수 있습니다.\n실행 옆의 HDR 및 RES 버튼은 게임 실행 중 Windows HDR을 켜고 해상도를 전환합니다.\n검색어를 입력한 뒤 나타나는 +를 클릭하면 필터 칩으로 저장됩니다."));

        // Step 2: Install ReShade
        panel.Children.Add(BuildFaqStep(2,
            "ReShade 설치",
            "RenoDX, Luma, 프레임 제한기, DOF Fix에는 ReShade가 필요합니다. 구성 요소 섹션에서 'ReShade 설치'를 클릭하세요. RHI가 게임의 그래픽 API에 맞는 파일 이름을 고르고 애드온을 지원하는 버전을 설치합니다.",
            "게임별 ReShade 버전은 게임 재정의 → ReShade 채널에서 변경합니다: 안정, 나이틀리, 사용자 지정(직접 준비한 DLL), 애드온 없음, 레거시.\nReShade ⚙에서는 reshade.ini 또는 ReShadePreset.ini를 배포하고, 게임별 단축키를 설정하며, RHI가 이 게임의 reshade.ini를 변경하지 않도록 할 수 있습니다.\nReShade 프리셋(.ini)을 RHI로 끌어다 놓으면 필요한 셰이더와 함께 설치됩니다."));

        // Step 3a: RenoDX
        panel.Children.Add(BuildFaqSection(
            "3a", "RenoDX", "AccentTealBrush",
            "RenoDX 모드는 게임에 진정한 HDR 출력을 제공합니다. RenoDX HDR 행에 해당 게임에서 사용할 수 있는 항목이 표시됩니다:\n\n• 전용 모드: 커뮤니티가 만든 게임별 HDR 모드.\n• UE-Extended: 전용 모드가 없는 Unreal Engine 게임용.\n• Generic Unity: 전용 모드가 없는 Unity 게임용.\n\n설치된 모드 옆의 ✓는 완성된 모드, 망치는 아직 작업 중인 모드를 뜻합니다. 도구 모음에서 '사용 가능한 모드'를 클릭하면 RenoDX, Luma, Ultra+ 모드가 있는 모든 게임을 볼 수 있습니다.",
            "RenoDX ⚙에는 게임별 최대 밝기, UE-Extended 및 Engine.ini HDR 옵션, 호환성 설정, 프리셋 내보내기/가져오기가 있습니다.\n게임용 모드가 없나요? 같은 ⚙에서 RTX HDR(NVIDIA) 또는 Auto HDR(Windows 11, 모든 GPU)을 켜세요. 둘 중 하나가 해당 게임의 RenoDX를 대신합니다.\nRenoDX Discord에서 .addon64를 받았나요? RHI로 끌어다 놓고 게임을 선택하세요."));

        // Step 3b: Luma
        panel.Children.Add(BuildFaqSection(
            "3b", "Luma", "AccentTealBrush",
            "Luma는 Pumbo가 만든 모드 프레임워크입니다. 게임에 따라 Luma 모드는 HDR, DLSS, DLAA 또는 렌더링 수정을 추가합니다. 각 모드의 기능은 정보 버튼에서 확인하세요.\n\n• 전용 모드: 지원되는 게임용으로 Luma 행에 표시됩니다. Nexus Mods에만 있는 모드는 Nexus 페이지가 열립니다.\n• Generic Luma: DX11 Unreal Engine 게임용. RHI가 Luma 위키에 나온 Engine.ini 조정과 실행 인수를 적용합니다.\n\nGeneric Luma는 RenoDX와 함께 쓸 수 있습니다. 전용 Luma 모드는 보통 RenoDX를 대체합니다.",
            "Luma에는 ReShade가 필요합니다. ReShade가 설치될 때까지 설치 버튼에 'ReShade required'가 표시됩니다.\nLuma ⚙(Unreal 게임)에는 첫 부팅 시 HDR 및 TAA 설정이 있습니다.\n-dx11 같은 실행 인수는 설치 시 추가되고 제거 시 삭제됩니다. RHI를 통해 게임을 실행할 때만 적용됩니다.\nDX9 Luma 모드에는 dgVoodoo2가 자동으로 설치됩니다."));

        // Step 4: Choose Shaders
        panel.Children.Add(BuildFaqStep(4,
            "셰이더 선택 (선택 사항)",
            "도구 모음에서 '셰이더/애드온'을 클릭한 다음 '전역 셰이더'를 클릭하세요. Lilium의 HDR 셰이더가 기본으로 선택되어 있습니다. 선택한 셰이더는 ReShade가 설치된 모든 게임에 배포됩니다.\n\n팩을 펼쳐 개별 셰이더를 고르거나 검색할 수 있습니다. 프로필 패널에서 선택을 이름 붙여 저장하고, 내보내기로 Discord에 붙여 넣을 수 있는 zip으로 복사할 수 있습니다.",
            "게임별 셰이더: 게임 재정의 → 셰이더 → 선택.\n직접 준비한 셰이더: 선택기의 프로필 패널에서 사용자 지정 폴더 열기를 클릭하고 그곳에 넣으세요. 사용자 지정 셰이더 아래에 표시됩니다.\n같은 도구 모음 메뉴의 전역 애드온에서 모든 게임의 ReShade 애드온을 관리합니다."));

        // Step 5: DOF Fix
        panel.Children.Add(BuildFaqStep(5,
            "DOF Fix (UE5 권장)",
            "DOF Fix는 Unreal Engine 5.7의 피사계 심도 수정을 UE 5.0–5.6 게임에 백포트합니다. 지원되는 게임에서는 구성 요소 섹션의 권장 항목에 표시됩니다. 최상의 결과를 위해 RenoDX와 함께 설치하세요.",
            "DOF Fix는 64비트 UE 5.0–5.6 게임에만 표시됩니다.\n게임의 엔진 배지에 'Unreal Engine'만 표시되면 배지를 클릭해 Unreal Engine 5로 지정하세요."));

        // Step 6: Frame Limiters
        panel.Children.Add(BuildFaqStep(6,
            "프레임 제한기 (선택 사항)",
            "ReLimiter와 Display Commander는 프레임 속도를 정밀하게 제한하는 ReShade 애드온으로, VRR 디스플레이에 이상적입니다. 구성 요소 섹션에서 둘 중 하나만 설치하세요(둘 다 설치하지 마세요). ReLimiter를 권장합니다. 목표 FPS는 설정 → 구성 요소, 게임별로는 ReLimiter ⚙, 또는 게임 안에서 설정합니다.",
            "어느 제한기든 설치하면 해당 게임의 NVIDIA 드라이버 프레임 제한이 꺼집니다.\n주사율별 VRR 제한 프리셋(최대치보다 약간 낮게 두면 VRR이 유지됨):\n• 60Hz → 59 FPS\n• 120Hz → 116 FPS\n• 144Hz → 138 FPS\n• 165Hz → 157 FPS\n• 240Hz → 224 FPS\n• 360Hz → 324 FPS\n이 값들은 RHI의 FPS 드롭다운에 미리 설정되어 있습니다."));

        // Step 7: DLSS/Streamline
        panel.Children.Add(BuildFaqStep(7,
            "DLSS / Streamline (선택 사항)",
            "DLSS 또는 Streamline 파일이 있는 게임에는 DLSS / Streamline 섹션이 있습니다. 슈퍼 해상도, 레이 재구성, 프레임 생성, Neural Rendering, Streamline의 버전을 고르면 RHI가 파일을 교체합니다. 원본은 백업되며, '기본값'을 고르거나 DLSS/SL 복원을 클릭하면 되돌립니다.\n\n게임별로 프리셋과 렌더 배율도 설정할 수 있습니다. 이 설정은 NVIDIA 드라이버에 기록되므로 NVIDIA Profile Inspector가 필요 없습니다.",
            "버전 목록의 NVIDIA Override는 드라이버가 대신 최신 DLSS 파일을 사용하도록 합니다.\n설정 → DLSS / Streamline → 기본값 구성에서 선호하는 버전과 프리셋을 한 번 설정한 뒤, 어떤 게임에서든 빠른 적용을 사용하거나 일괄 배포로 여러 게임에 한꺼번에 적용하세요.\nDLSS 섹션이 없나요? 설정 → 도구 및 유지 관리에서 전체 새로 고침을 사용하세요."));

        // Neural Rendering
        panel.Children.Add(BuildFaqSpecialSection("N", "AccentGreenBrush",
            "Neural Rendering (DLSS 5, 선택 사항)",
            "Neural Rendering 섹션은 DLSS 5 뉴럴 렌더링 도구를 설치합니다. RHI가 게임에 맞는 방식을 고르고, 필요하면 ReShade도 설치합니다:\n• ShortFuse DLSS Tool: 네이티브 DLSS가 있는 대부분의 게임.\n• DLSS5 Tool: 네이티브 DLSS가 있는 DX12 게임.\n• DLSS5 Tool + DX11 Bridge: 네이티브 DLSS가 있는 DX11 및 Vulkan 게임.\n• DLSS5 Feeder: OpenGL, DX9, 32비트 게임을 포함한 DLSS가 없는 게임.",
            "각 방식의 버전과 NR DLL 버전은 고정하거나 최신으로 둘 수 있습니다.\nNR Cost Scaler는 뉴럴 렌더링을 더 적은 비용으로 실행합니다. 설치 전에 켜세요. ShortFuse에는 이미 포함되어 있습니다."));

        // OptiScaler
        panel.Children.Add(BuildFaqSpecialSection("⚙", "AccentAmberBrush",
            "OptiScaler (선택 사항)",
            "OptiScaler는 게임의 DLSS, FSR, XeSS 입력으로 다른 업스케일러(FSR, XeSS, DLSS)를 구동하고, 어떤 GPU에서든 프레임 생성을 추가하거나 대체할 수 있습니다. 추가 기능 섹션에 있습니다.\n\n설치 전에 설정 → 구성 요소 → OptiScaler에서 GPU 종류(AMD/Intel은 DLSS 입력도)를 설정하세요. 그런 다음 OptiScaler ⚙에서 버전을 고르세요:\n• 안정: 공식 릴리스.\n• 나이틀리: 최신 빌드, 또는 특정 빌드 고정.\n• DLSS NR 및 F5 DLSSNR: DLSS Neural Rendering이 포함된 포크.\n\n⚙에서는 업스케일러, 프레임 생성, Reflex, DLSS 프리셋, 렌더 배율, Engine.ini 수정, Neural Rendering 옵션도 설정하고, 다른 게임에 적용할 수 있는 프리셋을 저장할 수 있습니다.",
            "OptiScaler와 ReShade는 함께 실행할 수 있습니다. 둘을 함께 쓸 때 게임이 충돌하면 게임 재정의 → DLL 이름 재정의에서 ReShade에 다른 이름을 지정하세요.\n⚙의 '새 복사본으로 교체...'는 게임의 OptiScaler.ini를 템플릿으로 초기화합니다."));

        // Extras
        panel.Children.Add(BuildFaqSpecialSection("X", "AccentAmberBrush",
            "추가 기능 (선택 사항)",
            "추가 기능 섹션에는 선택형 도구가 있습니다:\n• 프레임 생성 잠금 해제: Universal MFG Unlocker(RTX 40/30), MFG Unlock(RTX 40, RTX 20/30 실험적), 20/30 FG Unlock(RTX 20/30), RTX Encore, DLSS Enabler.\n• ASI Loader: .asi 플러그인을 불러옵니다.\n• DXVK: DX8–DX11 게임을 Vulkan으로 실행합니다. Lilium HDR 버전은 오래된 게임에 HDR을 제공합니다.\n• dgVoodoo2: DX9 게임을 DX11로 실행합니다.",
            "대부분 설치할 때 사용할 파일 이름을 묻습니다. RHI의 다른 구성 요소가 이미 사용 중인 이름은 비활성화되며, 교체되는 게임 파일은 백업됩니다."));

        // NVIDIA Driver Settings
        panel.Children.Add(BuildFaqInfoSection("NVIDIA 드라이버 설정",
            "드라이버 설정 섹션은 게임의 NVIDIA 프로필을 직접 변경합니다. 관리자 모드가 필요합니다(설정 → 일반 → 관리자 모드).",
            new[]
            {
                "VSync: 모드, 티어링 제어, 저지연(꺼짐, 켜짐, 울트라)",
                "Smooth Motion(RTX 40+): 켜져 있는 동안 저지연을 울트라로 설정",
                "전원 모드 및 G-Sync",
                "ReBAR: 사용, 모드, 크기 제한",
                "멀티 프레임 생성(RTX 50): DLSS / Streamline 섹션의 Multi Frame Gen 버튼"
            },
            "전역 기본값(셰이더 캐시, G-Sync, FPS 제한, VSync, 전원 모드, ReBAR)과 프로필 백업/복원은 설정 → 드라이버 설정에 있습니다. 디지털 바이브런스는 설정 → 디스플레이 및 HDR에 있습니다."));

        // Settings Overview
        panel.Children.Add(BuildFaqInfoSection("설정",
            "모든 게임에 적용되는 옵션은 도구 모음에서 '설정'을 클릭하세요:",
            new[]
            {
                "일반: 업데이트 확인, 자동 업데이트(구성 요소, DLSS, Streamline), 시스템 트레이, 관리자 모드",
                "계정 및 데이터: GitHub 토큰, 모드 데이터 소스, Nexus Mods",
                "ReShade 및 RenoDX: 단축키, 스크린샷, 최대 밝기(nits), 셰이더, 애드온 감시 폴더",
                "디스플레이 및 HDR: HDR 및 해상도 자동 전환, 출력 색상, DLDSR",
                "DLSS / Streamline: 빠른 적용 기본값, DLSS 표시기, 멀티 프레임 생성, 일괄 배포",
                "드라이버 설정: 전역 NVIDIA 드라이버 기본값 및 프로필",
                "구성 요소: ReLimiter 및 OptiScaler",
                "도구 및 유지 관리: INI 일괄 배포, 폴더 및 로그, 전체 새로 고침, 스테이징 파일 정리"
            }));

        // Vulkan Games
        panel.Children.Add(BuildFaqSpecialSection("V", "AccentPurpleBrush",
            "Vulkan 게임",
            "Vulkan 게임(VLK 배지로 표시)은 C:\\ProgramData\\ReShade에 설치되는 전역 ReShade 레이어를 사용합니다. 관리자 권한이 필요하므로 RHI가 처음에 권한을 요청합니다.",
            "모든 Vulkan 게임은 같은 ReShade를 공유하므로, 한 게임에서 ReShade 채널을 바꾸면 모든 게임에 적용됩니다.\nRenoDX 애드온과 셰이더는 여전히 각 게임 폴더에 설치됩니다.\nDirectX와 Vulkan을 모두 지원하는 게임: 게임 재정의 → 그래픽 API에서 하나를 선택하세요."));

        // Adding Games Manually
        panel.Children.Add(BuildFaqSpecialSection("+", "AccentAmberBrush",
            "게임 수동 추가",
            "게임이 감지되지 않으면 검색 상자 옆의 녹색 +를 클릭해 .exe를 선택하거나, .exe를 RHI 창으로 끌어다 놓으세요. RHI가 라이브러리에 추가하고 엔진을 감지합니다.",
            "폴더가 잘못되었나요? 관리 → 설치 폴더 변경을 사용하세요.\n수동으로 추가한 게임은 관리 → 초기화 / 게임 제거로 제거할 수 있습니다."));

        // Updating Everything
        panel.Children.Add(BuildFaqSpecialSection("↑", "AccentGreenBrush",
            "모두 업데이트하기",
            "도구 모음에서 '모두 업데이트'를 클릭하면 모든 게임의 ReShade, RenoDX, ReLimiter, Display Commander, OptiScaler, RE Framework, DXVK, Luma, DOF Fix를 업데이트합니다.",
            "업데이트가 있는 게임은 사이드바에 녹색 점이 표시됩니다.\n모두 업데이트에 포함할 항목은 설정 → 일반 → 확인할 구성 요소에서, 게임별로는 게임 재정의 → 업데이트 포함에서 선택하세요.\n설정 → 일반에서 자동 업데이트를 켜면 백그라운드에서 업데이트가 설치됩니다.\n오른쪽 아래의 업데이트 버튼에서 업데이트된 항목을 모두 볼 수 있습니다."));

        // Troubleshooting
        panel.Children.Add(BuildFaqSpecialSection("↻", "AccentBlueBrush",
            "문제 해결",
            "게임이 누락되었거나, 게임 위치가 바뀌었거나, DLSS 파일이 추가 또는 제거되었다면 설정 → 도구 및 유지 관리의 '전체 새로 고침'으로 처음부터 모두 다시 검색하세요.",
            "다운로드나 업데이트 확인이 실패하나요? GitHub 토큰을 추가하세요(설정 → 계정 및 데이터).\n문제를 보고하려면 설정 → 도구 및 유지 관리의 로그 복사 및/또는 게임 관리 섹션의 보고서 복사를 클릭한 뒤 Discord에 붙여 넣으세요."));

        // System Tray
        panel.Children.Add(BuildFaqSpecialSection("◰", "AccentPurpleBrush",
            "시스템 트레이",
            "창을 닫아도 RHI가 시스템 트레이에서 계속 실행되도록 할 수 있습니다. 트레이 아이콘을 마우스 오른쪽 버튼으로 클릭하면 RHI를 열지 않고 최근 게임을 실행할 수 있습니다.",
            "설정 → 일반 → Windows에서 트레이로 닫기, 최근 게임, Windows 시작 시 실행을 켜세요.\n최근 게임은 작업 표시줄 점프 목록에도 표시됩니다.\n실행 중에는 GitHub 토큰이 있으면(설정 → 계정 및 데이터) 1시간마다, 없으면 4시간마다 업데이트를 확인합니다."));

        // Need More Help
        panel.Children.Add(BuildFaqLinksSection());
    }


    private Border BuildFaqSection(string? badge, string title, string titleBrush, string description, string? tip)
    {
        var stack = new StackPanel { Spacing = 10 };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 10 };
        if (badge != null)
        {
            var badgeBorder = new Border
            {
                Background = (Brush)Application.Current.Resources[titleBrush],
                CornerRadius = new CornerRadius(12),
                Width = 24,
                Height = 24
            };
            badgeBorder.Child = new TextBlock
            {
                Text = badge,
                FontSize = 12,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                Foreground = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 10, 30, 50)),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Children.Add(badgeBorder);
        }
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(header);

        stack.Children.Add(new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            LineHeight = 20
        });

        if (tip != null)
        {
            var tipBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["SurfaceToolbarBrush"],
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                BorderBrush = (Brush)Application.Current.Resources["BorderSubtleBrush"],
                BorderThickness = new Thickness(1)
            };
            tipBorder.Child = new TextBlock
            {
                Text = tip,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                LineHeight = 18
            };
            stack.Children.Add(tipBorder);
        }

        return new Border
        {
            Background = (Brush)Application.Current.Resources["SurfaceRaisedBrush"],
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20, 16, 20, 16),
            BorderBrush = (Brush)Application.Current.Resources["BorderSubtleBrush"],
            BorderThickness = new Thickness(1),
            Child = stack
        };
    }


    private Border BuildFaqStep(int step, string title, string description, string? tip)
    {
        var stack = new StackPanel { Spacing = 8 };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var badgeBorder = new Border
        {
            Background = (Brush)Application.Current.Resources["AccentTealBrush"],
            CornerRadius = new CornerRadius(12),
            Width = 24,
            Height = 24
        };
        badgeBorder.Child = new TextBlock
        {
            Text = step.ToString(),
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 10, 30, 50)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(badgeBorder);
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(header);

        stack.Children.Add(new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            LineHeight = 20,
            Margin = new Thickness(32, 0, 0, 0)
        });

        if (tip != null)
        {
            var tipBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["SurfaceToolbarBrush"],
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(32, 4, 0, 0),
                BorderBrush = (Brush)Application.Current.Resources["BorderSubtleBrush"],
                BorderThickness = new Thickness(1)
            };
            tipBorder.Child = new TextBlock
            {
                Text = tip,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                LineHeight = 18
            };
            stack.Children.Add(tipBorder);
        }

        return new Border
        {
            Background = (Brush)Application.Current.Resources["SurfaceRaisedBrush"],
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20, 16, 20, 16),
            BorderBrush = (Brush)Application.Current.Resources["BorderSubtleBrush"],
            BorderThickness = new Thickness(1),
            Child = stack
        };
    }


    private Border BuildFaqInfoSection(string title, string description, string[] bullets, string? tip = null)
    {
        var stack = new StackPanel { Spacing = 8 };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var badgeBorder = new Border
        {
            Background = (Brush)Application.Current.Resources["AccentBlueBrush"],
            CornerRadius = new CornerRadius(12),
            Width = 24,
            Height = 24
        };
        badgeBorder.Child = new TextBlock
        {
            Text = "?",
            FontSize = 12,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 10, 30, 50)),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(badgeBorder);
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(header);

        stack.Children.Add(new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            LineHeight = 20,
            Margin = new Thickness(32, 0, 0, 0)
        });

        var bulletStack = new StackPanel { Spacing = 6, Margin = new Thickness(32, 4, 0, 0) };
        foreach (var bullet in bullets)
        {
            bulletStack.Children.Add(new TextBlock
            {
                Text = $"• {bullet}",
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                LineHeight = 18
            });
        }
        stack.Children.Add(bulletStack);

        if (tip != null)
        {
            var tipBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["SurfaceToolbarBrush"],
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(32, 4, 0, 0),
                BorderBrush = (Brush)Application.Current.Resources["BorderSubtleBrush"],
                BorderThickness = new Thickness(1)
            };
            tipBorder.Child = new TextBlock
            {
                Text = tip,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                LineHeight = 18
            };
            stack.Children.Add(tipBorder);
        }

        return new Border
        {
            Background = (Brush)Application.Current.Resources["SurfaceRaisedBrush"],
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20, 16, 20, 16),
            BorderBrush = (Brush)Application.Current.Resources["BorderSubtleBrush"],
            BorderThickness = new Thickness(1),
            Child = stack
        };
    }


    private Border BuildFaqSpecialSection(string badge, string badgeBrush, string title, string description, string? tip)
    {
        var stack = new StackPanel { Spacing = 8 };

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        var badgeBorder = new Border
        {
            Background = (Brush)Application.Current.Resources[badgeBrush],
            CornerRadius = new CornerRadius(12),
            Width = 24,
            Height = 24
        };
        var badgeFg = UIFactory.ThemedBrush(Windows.UI.Color.FromArgb(255, 10, 30, 50));
        badgeBorder.Child = new TextBlock
        {
            Text = badge,
            FontSize = badge.Length > 1 ? 11 : 14,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            Foreground = badgeFg,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        header.Children.Add(badgeBorder);
        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["TextPrimaryBrush"],
            VerticalAlignment = VerticalAlignment.Center
        });
        stack.Children.Add(header);

        stack.Children.Add(new TextBlock
        {
            Text = description,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            LineHeight = 20,
            Margin = new Thickness(32, 0, 0, 0)
        });

        if (tip != null)
        {
            var tipBorder = new Border
            {
                Background = (Brush)Application.Current.Resources["SurfaceToolbarBrush"],
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(32, 4, 0, 0),
                BorderBrush = (Brush)Application.Current.Resources["BorderSubtleBrush"],
                BorderThickness = new Thickness(1)
            };
            tipBorder.Child = new TextBlock
            {
                Text = tip,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
                LineHeight = 18
            };
            stack.Children.Add(tipBorder);
        }

        return new Border
        {
            Background = (Brush)Application.Current.Resources["SurfaceRaisedBrush"],
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20, 16, 20, 16),
            BorderBrush = (Brush)Application.Current.Resources["BorderSubtleBrush"],
            BorderThickness = new Thickness(1),
            Child = stack
        };
    }


    private Border BuildFaqLinksSection()
    {
        var stack = new StackPanel { Spacing = 10 };

        stack.Children.Add(new TextBlock
        {
            Text = "도움이 더 필요하신가요?",
            FontSize = 14,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            Foreground = (Brush)Application.Current.Resources["AccentTealBrush"]
        });

        stack.Children.Add(new TextBlock
        {
            Text = "Discord에서 지원을 받을 수 있습니다. 커뮤니티에 참여해 도움, 모드 업데이트, 토론을 확인하세요.",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = (Brush)Application.Current.Resources["TextSecondaryBrush"],
            LineHeight = 20
        });

        var linksStack = new StackPanel { Spacing = 6 };

        var discordLink = new HyperlinkButton
        {
            NavigateUri = new Uri("https://discord.gg/ultraplus"),
            Padding = new Thickness(0)
        };
        discordLink.Content = new TextBlock
        {
            Text = "Ultra+ Discord 참여 (메인 커뮤니티)",
            Foreground = (Brush)Application.Current.Resources["AccentBlueBrush"],
            FontSize = 12
        };
        linksStack.Children.Add(discordLink);

        var renodxDiscordLink = new HyperlinkButton
        {
            NavigateUri = new Uri("https://discord.gg/renodx"),
            Padding = new Thickness(0)
        };
        renodxDiscordLink.Content = new TextBlock
        {
            Text = "RenoDX Discord (모드 개발)",
            Foreground = (Brush)Application.Current.Resources["AccentBlueBrush"],
            FontSize = 12
        };
        linksStack.Children.Add(renodxDiscordLink);

        var wikiLink = new HyperlinkButton
        {
            NavigateUri = new Uri("https://github.com/clshortfuse/renodx/wiki/Mods"),
            Padding = new Thickness(0)
        };
        wikiLink.Content = new TextBlock
        {
            Text = "RenoDX 모드 위키 둘러보기",
            Foreground = (Brush)Application.Current.Resources["AccentBlueBrush"],
            FontSize = 12
        };
        linksStack.Children.Add(wikiLink);

        var githubLink = new HyperlinkButton
        {
            NavigateUri = new Uri("https://github.com/RankFTW/RHI"),
            Padding = new Thickness(0)
        };
        githubLink.Content = new TextBlock
        {
            Text = "RHI GitHub — 문제 보고 또는 기능 요청",
            Foreground = (Brush)Application.Current.Resources["AccentBlueBrush"],
            FontSize = 12
        };
        linksStack.Children.Add(githubLink);

        stack.Children.Add(linksStack);

        return new Border
        {
            Background = (Brush)Application.Current.Resources["SurfaceRaisedBrush"],
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(20, 16, 20, 16),
            BorderBrush = (Brush)Application.Current.Resources["AccentTealBorderBrush"],
            BorderThickness = new Thickness(1),
            Child = stack
        };
    }
}
