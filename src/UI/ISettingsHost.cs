namespace Xtension
{
    /// <summary>
    /// What the Settings window needs from the application. Implemented by <see cref="TrayAppContext"/>; a test can
    /// substitute its own implementation, so the window's behaviour is verifiable without a tray icon or a browser.
    /// </summary>
    internal interface ISettingsHost
    {
        /// <summary>Effective overlay state right now, including any confirmation the user cancelled.</summary>
        bool OverlayEnabled { get; }

        /// <summary>
        /// Live settings copy. The window reads it before showing and after every apply, so the controls always
        /// reflect what the application is really using rather than what the window last sent.
        /// </summary>
        AppSettings Settings { get; }

        /// <summary>Registry state of the per-user start-with-Windows entry.</summary>
        bool LaunchAtLogin { get; }

        /// <summary>
        /// Asks to turn the custom × on or off. Turning it off shows the warning about Brave's own close buttons and
        /// may be cancelled, so the returned value is the effective state, not the requested one.
        /// </summary>
        bool RequestOverlayEnabled(bool enabled);

        /// <summary>Applies a validated settings set immediately and persists it. Failures are reported by the host.</summary>
        void ApplySettings(AppSettings settings);

        /// <summary>Writes one registry Run entry, or removes it. Never throws; reports its own failure.</summary>
        void SetLaunchAtLogin(bool enabled);

        void ShowDiagnostics();

        void OpenLog();
    }
}
