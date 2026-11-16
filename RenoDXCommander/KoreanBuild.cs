namespace RenoDXCommander;

/// <summary>Flags for the unofficial Korean-translated build of RHI.</summary>
internal static class KoreanBuild
{
    /// <summary>
    /// Automatic app update checks are off: installing an official update would
    /// replace this translated build with the English release.
    /// </summary>
    public const bool DisableAppAutoUpdate = true;
}
