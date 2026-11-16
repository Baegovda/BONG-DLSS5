// PlainTips.cs -- BONG DLSS5: plain-language tooltips for the jargon on screen.
// After a page is built, Attach() walks its visual tree and gives every label or button that names a known
// term (and has no tooltip of its own yet) a short explanation a non-technical user can follow.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace RenoDXCommander.Services;

internal static class PlainTips
{
    /// <summary>Term as it appears on screen → what it means, in everyday words. Longer terms are matched first.</summary>
    private static readonly (string Term, string Tip)[] Glossary =
    {
        // DLSS 5 / Neural Rendering
        ("DLSS 5", "엔비디아의 최신 AI 화질 기술이에요. AI가 조명·그림자·재질을 실제처럼 다시 그려서, 게임 화면이 훨씬 사실적으로 보여요."),
        ("DLSS5", "엔비디아의 최신 AI 화질 기술이에요. AI가 조명·그림자·재질을 실제처럼 다시 그려서, 게임 화면이 훨씬 사실적으로 보여요."),
        ("신경망 렌더링", "DLSS 5의 다른 이름이에요. 그래픽카드의 AI가 게임 장면을 더 사실적으로 다시 그려 줘요."),
        ("Neural Rendering", "DLSS 5의 다른 이름이에요. 그래픽카드의 AI가 게임 장면을 더 사실적으로 다시 그려 줘요."),
        ("GITC Uplift", "DLSS 5를 게임에 연결하는 방법 중 하나예요. 보통은 추천된 방법을 그대로 쓰면 돼요."),
        ("DLSS5 Feeder", "DLSS 5를 게임에 연결하는 방법 중 하나예요. ReShade를 통해 게임 화면을 DLSS 5로 보내 줘요."),
        ("ShortFuse DLSS Tool", "DLSS 5를 게임에 연결하는 방법 중 하나예요(제작자: ShortFuse). 보통은 추천된 방법을 그대로 쓰면 돼요."),
        ("DLSS5 Tool", "DLSS 5를 게임에 연결하는 방법 중 하나예요. 보통은 추천된 방법을 그대로 쓰면 돼요."),
        ("DX11 Bridge", "DirectX 11로 만든 옛날 게임에서도 DLSS 5가 돌아가게 이어 주는 연결 장치예요."),
        ("NR Cost Scaler", "DLSS 5가 그래픽카드를 얼마나 많이 쓰게 할지 정해요. 낮추면 프레임이 올라가고, 올리면 화질이 좋아져요."),
        ("RTX Encore", "RTX 40 시리즈에서도 최신 프레임 생성 기능을 쓸 수 있게 해 주는 추가 기능이에요."),

        // DLSS family
        ("DLSS Frame Generation", "AI가 진짜 프레임 사이에 가짜 프레임을 끼워 넣어서, 화면이 더 부드럽게 보이게 해요. 반응 속도는 살짝 느려질 수 있어요."),
        ("Frame Generation", "AI가 진짜 프레임 사이에 가짜 프레임을 끼워 넣어서, 화면이 더 부드럽게 보이게 해요. 반응 속도는 살짝 느려질 수 있어요."),
        ("프레임 생성", "AI가 진짜 프레임 사이에 가짜 프레임을 끼워 넣어서, 화면이 더 부드럽게 보이게 해요. 반응 속도는 살짝 느려질 수 있어요."),
        ("Ray Reconstruction", "레이 트레이싱(빛 추적) 화면의 지글거림을 AI가 깨끗하게 정리해 줘요."),
        ("Streamline", "게임이 DLSS 같은 엔비디아 기능을 불러 쓰는 연결 통로예요. 보통은 건드리지 않아도 돼요."),
        ("DLSSG", "DLSS 프레임 생성 파일이에요. 화면을 더 부드럽게 해 주는 가짜 프레임을 만들어요."),
        ("DLSSD", "DLSS 레이 재구성 파일이에요. 빛 추적 화면의 잡티를 AI가 정리해 줘요."),
        ("MFG", "멀티 프레임 생성이에요. 진짜 프레임 하나당 가짜 프레임을 여러 장 넣어 화면을 훨씬 부드럽게 만들어요."),
        ("DLSS", "엔비디아의 AI 업스케일링이에요. 게임을 낮은 해상도로 돌린 뒤 AI가 선명하게 키워서, 화질은 비슷하게 두고 프레임을 올려 줘요."),
        ("DLAA", "해상도를 낮추지 않고 AI로 계단 현상만 매끄럽게 다듬는 모드예요. 화질은 가장 좋지만 프레임 이득은 없어요."),
        ("Reflex", "입력 지연을 줄여 주는 엔비디아 기능이에요. 마우스를 움직이면 화면이 더 빨리 반응해요."),
        ("업스케일링", "게임을 작은 해상도로 그린 다음 크게 늘려서 프레임을 올리는 방식이에요."),
        ("렌더 스케일", "게임이 실제로 그리는 해상도 비율이에요. 낮을수록 빠르지만 흐려져요."),
        ("프리셋", "미리 정해 둔 설정 묶음이에요. 하나만 고르면 여러 설정이 한 번에 바뀌어요."),

        // Mods and tools
        ("ReShade", "게임 화면 위에 색감·선명도·HDR 같은 효과를 덧입히는 프로그램이에요. DLSS 5 같은 기능도 이걸 통해 붙어요."),
        ("RenoDX", "SDR로만 나오던 게임을 진짜 HDR로 바꿔 주는 모드예요. HDR 모니터가 있어야 효과가 보여요."),
        ("Luma", "게임 화면을 HDR과 더 좋은 화질로 바꿔 주는 모드예요."),
        ("ReLimiter", "프레임(초당 화면 수)을 일정하게 묶어서 화면이 덜 끊기게 하는 도구예요."),
        ("Display Commander", "프레임 제한, 화면 모드 같은 디스플레이 설정을 게임마다 정해 주는 도구예요."),
        ("OptiScaler", "AMD FSR, 인텔 XeSS만 지원하는 게임에서도 DLSS를 쓸 수 있게 바꿔 주는 도구예요(반대도 가능)."),
        ("FSR", "AMD의 업스케일링 기술이에요. DLSS처럼 해상도를 낮춰 그린 뒤 키워서 프레임을 올려요."),
        ("XeSS", "인텔의 업스케일링 기술이에요. DLSS처럼 프레임을 올려 줘요."),
        ("DXVK", "옛날 DirectX 게임을 Vulkan 방식으로 바꿔서 돌려 주는 도구예요. 일부 게임에서 더 빠르고 안정적이에요."),
        ("dgVoodoo2", "아주 오래된 게임의 그래픽을 요즘 컴퓨터에서 잘 돌아가게 바꿔 주는 도구예요."),
        ("ASI Loader", "게임이 시작될 때 추가 모드 파일을 같이 불러오게 해 주는 작은 도구예요."),
        ("RE Framework", "캡콤 게임(바이오하자드, 몬스터헌터 등)용 모드를 불러오는 도구예요."),
        ("HSF", "프레임 생성 기능을 지원하지 않는 게임에도 프레임 생성을 붙여 주는 도구예요."),
        ("iMMERSE", "ReShade용 고급 효과 모음이에요. 일부 기능이 화면 움직임 정보를 얻는 데 써요."),
        ("Ultra+", "언리얼 엔진 게임의 숨은 그래픽 설정을 쉽게 바꾸게 해 주는 모드예요."),
        ("셰이더", "ReShade가 화면에 입히는 효과 하나하나예요(선명하게, 색 보정 등)."),
        ("애드온", "ReShade에 기능을 더해 주는 확장 파일이에요."),
        ("Addon", "ReShade에 기능을 더해 주는 확장 파일이에요."),

        // Display
        ("RTX HDR", "엔비디아 드라이버가 SDR 게임을 자동으로 HDR처럼 바꿔 주는 기능이에요."),
        ("HDR", "밝은 곳은 더 밝게, 어두운 곳은 더 깊게 보여 주는 화면 방식이에요. HDR 모니터가 필요해요."),
        ("DLDSR", "모니터보다 높은 해상도로 그린 뒤 줄여서 보여 줘요. 계단 현상이 줄고 더 선명해지지만 느려져요."),
        ("Smooth Motion", "엔비디아 드라이버가 게임 밖에서 프레임을 끼워 넣어 화면을 부드럽게 만드는 기능이에요."),
        ("ReBAR", "CPU가 그래픽카드 메모리를 한 번에 크게 쓰게 해서, 일부 게임이 조금 빨라지는 기능이에요."),
        ("VSync", "화면 찢어짐을 막으려고 프레임을 모니터 주사율에 맞추는 기능이에요. 반응이 약간 느려질 수 있어요."),
        ("G-SYNC", "모니터가 게임 프레임에 맞춰 화면을 바꿔서 찢어짐과 끊김을 없애는 기능이에요."),
        ("프레임 제한", "게임이 초당 그리는 화면 수에 상한을 두는 거예요. 화면이 고르게 나오고 그래픽카드 열이 줄어요."),

        // System / game info
        ("DX12", "게임이 그래픽카드와 대화하는 방식(DirectX 12)이에요. DLSS 5는 주로 이 방식에서 잘 돼요."),
        ("DX11", "게임이 그래픽카드와 대화하는 방식(DirectX 11)이에요. 조금 옛날 방식이에요."),
        ("Vulkan", "DirectX 대신 쓰는 그래픽 방식이에요. 일부 게임이 이걸로 돌아가요."),
        ("OpenGL", "아주 오래된 그래픽 방식이에요."),
        ("그래픽 API", "게임이 그래픽카드와 대화하는 방식이에요(DX11, DX12, Vulkan 등). 보통은 자동(Auto)이면 돼요."),
        ("64비트", "게임 프로그램의 종류예요. 요즘 게임은 대부분 64비트예요."),
        ("32비트", "게임 프로그램의 종류예요. 옛날 게임이 주로 32비트예요."),
        ("비트", "게임 프로그램이 32비트인지 64비트인지예요. 보통은 자동(Auto)이면 돼요."),
        ("DLL", "게임이 불러 쓰는 부품 파일이에요. 모드는 이 파일을 게임 폴더에 넣어서 작동해요."),
        ("INI", "설정이 글자로 적혀 있는 파일이에요."),
        ("NVIDIA 프로필", "엔비디아 드라이버가 게임마다 따로 기억하는 설정이에요."),
        ("드라이버", "그래픽카드를 움직이는 기본 프로그램이에요. 엔비디아 앱에서 업데이트해요."),
        ("안티치트", "온라인 게임의 해킹 방지 프로그램이에요. 모드를 넣으면 계정이 정지될 수 있으니 온라인 게임엔 쓰지 마세요."),
        ("관리자 모드", "윈도우 관리자 권한으로 실행하는 거예요. 일부 기능(ReBAR, 드라이버 설정 등)은 이게 있어야 바뀌어요."),
        ("Nexus", "게임 모드를 모아 둔 사이트(Nexus Mods)예요. 누르면 이 게임의 모드 페이지가 열려요."),
        ("PCGW", "PCGamingWiki예요. 게임의 PC 설정·문제 해결 정보를 모아 둔 사이트예요."),
        ("Global", "모든 게임에 똑같이 적용되는 공통 설정을 쓴다는 뜻이에요."),
        ("Stable", "충분히 검증된 안정판이에요. 잘 모르겠으면 이걸 고르세요."),
        ("Nightly", "매일 나오는 최신 시험판이에요. 새 기능이 있지만 문제가 생길 수 있어요."),
        ("재정의", "이 게임에만 따로 적용할 설정이에요. 자동으로 정해진 값을 바꿀 때 써요."),
        ("오버레이", "게임 화면 위에 겹쳐 띄우는 작은 창이에요."),
    };

    private static readonly (string Term, string Tip)[] ByLength =
        Glossary.OrderByDescending(g => g.Term.Length).ToArray();

    /// <summary>Plain explanation for the first term named in <paramref name="text"/>, or null.</summary>
    internal static string? Find(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var t = text.Trim();
        if (t.Length > 60) return null; // long sentences already explain themselves
        foreach (var (term, tip) in ByLength)
        {
            int i = t.IndexOf(term, StringComparison.OrdinalIgnoreCase);
            if (i < 0) continue;
            // whole-word for Latin terms ("FSR" not inside "FSR3SomethingElse" is fine, but "DLSS" must not hit "DLSSG")
            bool startOk = i == 0 || !char.IsLetterOrDigit(t[i - 1]) || !IsLatin(term[0]);
            int end = i + term.Length;
            bool endOk = end >= t.Length || !IsLatin(term[^1]) || !(char.IsLetter(t[end]) && IsLatin(t[end]));
            if (startOk && endOk) return tip;
        }
        return null;
    }

    private static bool IsLatin(char c) => c < 128;

    /// <summary>Adds plain tooltips under <paramref name="root"/> to labels and buttons that name a known term.</summary>
    internal static void Attach(DependencyObject? root)
    {
        if (root == null) return;
        _added = 0;
        try { Walk(root, 0); if (_added > 0) CrashReporter.Log($"[PlainTips.Attach] {_added} tooltip(s) added under {root.GetType().Name}"); }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            CrashReporter.Log($"[PlainTips.Attach] {ex.Message}");
        }
    }

    private static void Walk(DependencyObject node, int depth)
    {
        if (depth > 60) return;
        switch (node)
        {
            case Button b when ToolTipService.GetToolTip(b) == null:
                if (Find(LabelOf(b.Content)) is { } bt) Set(b, bt);
                break;
            case ToggleSwitch ts when ToolTipService.GetToolTip(ts) == null:
                if (Find(LabelOf(ts.Header)) is { } tt) Set(ts, tt);
                break;
            case ComboBox cb when ToolTipService.GetToolTip(cb) == null:
                if (Find(LabelOf(cb.Header)) is { } ct) Set(cb, ct);
                break;
            case TextBlock tb when ToolTipService.GetToolTip(tb) == null && !InsideTipped(tb):
                if (Find(tb.Text) is { } tbt) Set(tb, tbt);
                break;
        }
        int n = VisualTreeHelper.GetChildrenCount(node);
        for (int i = 0; i < n; i++) Walk(VisualTreeHelper.GetChild(node, i), depth + 1);
    }

    /// <summary>A label inside a button that already has a tooltip keeps the button's tooltip.</summary>
    private static bool InsideTipped(DependencyObject el)
    {
        var p = VisualTreeHelper.GetParent(el);
        for (int i = 0; p != null && i < 6; i++, p = VisualTreeHelper.GetParent(p))
            if (p is Button or ComboBox or ToggleSwitch) return ToolTipService.GetToolTip(p) != null;
        return false;
    }

    private static string? LabelOf(object? content) => content switch
    {
        string s => s,
        TextBlock tb => tb.Text,
        _ => null,
    };

    private static int _added;

    private static void Set(DependencyObject el, string tip)
    {
        _added++;
        ToolTipService.SetToolTip(el, new ToolTip
        {
            Content = new TextBlock { Text = tip, TextWrapping = TextWrapping.Wrap, MaxWidth = 320, FontSize = 13 },
        });
    }
}
