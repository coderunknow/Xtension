using System;
using System.Globalization;

namespace Xtension
{
    /// <summary>Interface language. Vietnamese is the default and matches the shipped wording.</summary>
    internal enum AppLanguage
    {
        Vietnamese = 0,
        English = 1
    }

    /// <summary>
    /// Identifies every user-visible string. Scan results and controller state store these keys instead of
    /// localized text, so changing the language is reflected immediately and no stale translation can survive
    /// in cached state. Log lines and internal decision reasons stay English on purpose.
    /// </summary>
    internal enum StringId
    {
        /// <summary>No text. Used as the "no notice" sentinel, so the table stays aligned with the enum.</summary>
        None,

        // Scan outcomes
        ScanReady,
        ScanIncomplete,
        ScanNoTabs,
        ScanNoSelectedTab,
        ScanAmbiguousSelection,
        ScanUnknownSelection,
        ScanSelectedNotHorizontal,
        ScanSelectedTabTooSmall,
        ScanWindowBoundsUnavailable,
        ScanWindowRootUnavailable,
        ScanFailed,
        ScanWorkerFailed,

        // Controller idle reasons
        IdleStarting,
        IdleBraveNotForeground,
        IdleBraveMinimized,

        // Controller notices (shown on the dashboard after an action or a rejected click)
        NoticeBraveNotForeground,
        NoticeCloseLaneBusy,
        NoticeValidating,
        NoticeEpochChanged,
        NoticeClickRejected,
        NoticeWindowOrModifierChanged,
        NoticeClosed,
        NoticeCloseFailed,
        NoticeValidationCancelled,
        NoticeValidationExpired,

        // Reasons the button is currently hidden
        HiddenOverlayDisabled,
        HiddenStalled,
        HiddenCloseInFlight,
        HiddenCooldown,
        HiddenWaitingScan,
        HiddenStaleConfirmation,
        HiddenHoverOutside,
        OverlayShowFailed,

        // Dashboard
        DashboardTagline,
        BadgeStarting,
        BadgeDisabled,
        BadgeStalled,
        BadgeActive,
        BadgeWaitingBrave,
        BadgeReady,
        BadgeUnknown,
        HeadlineStarting,
        HeadlineDisabled,
        HeadlineStalled,
        HeadlineActive,
        HeadlineWaitingBrave,
        HeadlineReady,
        HeadlineUnknown,
        DetailDisabled,
        DetailStalled,
        DetailActive,
        DetailWaitingFirstScan,
        LabelActiveTab,
        NoConfirmedTab,
        UntitledTab,
        ToggleTurnOff,
        ToggleTurnOn,
        ButtonDiagnostics,
        ButtonBraveSettings,
        ButtonXtensionSettings,
        ButtonOpenLog,
        ButtonExit,
        GeneralNote,
        Footer,

        // Tray menu
        MenuOpenDashboard,
        MenuOverlay,
        MenuStartup,
        MenuDiagnostics,
        MenuBraveSettings,
        MenuXtensionSettings,
        MenuLog,
        MenuExit,
        TrayDisabledSuffix,

        // Dialogs
        ConfirmExitTitle,
        ConfirmExitBody,
        ConfirmDisableTitle,
        ConfirmDisableBody,
        StartupRegistrationFailedTitle,
        StartupRegistrationFailedBody,
        BraveSettingsFailedTitle,
        BraveSettingsFailedBody,
        LogOpenFailedTitle,
        LogOpenFailedBody,
        TrayTipMinimizedTitle,
        TrayTipMinimizedBody,
        SingleInstanceUnavailable,
        SingleInstanceNotResponding,
        FatalStartupError,
        LogFileHeader,

        // Diagnostics
        DiagTitle,
        DiagColumnTab,
        DiagColumnSelected,
        DiagColumnHorizontal,
        DiagColumnBounds,
        DiagColumnOffscreen,
        DiagWaiting,
        DiagVisibleNow,
        DiagLastScanLine,
        DiagNoScanYet,
        Yes,
        No,
        Unknown,
        DiagSummary,
        DiagSummaryVisible,
        DiagSummaryHidden,
        DiagErrorLabel,
        DiagNoRect,
        DiagUntitledTab,

        // Overlay
        OverlayTooltip,
        OverlayAccessibleName,
        OverlayAccessibleDescription,

        // Xtension Settings window
        SettingsTitle,
        SettingsIntro,
        SettingsGroupButton,
        SettingsEnable,
        SettingsEnableHelp,
        SettingsGroupVisibility,
        SettingsModeAlways,
        SettingsModeHover,
        SettingsModeHelp,
        SettingsGroupSize,
        SettingsSizeLabel,
        SettingsInsetLabel,
        SettingsGeometryHelp,
        SettingsGroupAdvanced,
        SettingsHoverGraceLabel,
        SettingsScanIntervalLabel,
        SettingsAdvancedHelp,
        SettingsGroupGeneral,
        SettingsLanguage,
        LanguageVietnamese,
        LanguageEnglish,
        SettingsStartWithWindows,
        SettingsStartupHelp,
        SettingsBraveNote,
        SettingsButtonDiagnostics,
        SettingsButtonLog,
        SettingsReset,
        SettingsResetConfirm,
        SettingsClose,
        SettingsResetDone,
        SettingsSaveFailed
    }

    /// <summary>
    /// The single place where user-visible text lives. Every entry is a pair: Vietnamese first, English second,
    /// in the exact order of <see cref="StringId"/>. <see cref="TableCount"/> and the unit tests keep the table
    /// and the enum in step.
    /// </summary>
    internal static class Strings
    {
        private static volatile AppLanguage _language = AppLanguage.Vietnamese;

        /// <summary>
        /// Vietnamese and English text for every <see cref="StringId"/>, in enum order.
        /// Placeholders ({0}, {1}) are filled by <see cref="Get(StringId, object)"/>.
        /// </summary>
        private static readonly string[][] Table = new string[][]
        {
            new string[] { "", "" },

            // Scan outcomes
            new string[] { "Đã xác nhận tab đang chọn. Tìm thấy {0} tab.", "The selected tab is confirmed. {0} tabs found." },
            new string[] { "Quét UI Automation chưa đầy đủ (đạt giới hạn duyệt/thời gian hoặc phần tử đã biến mất). Đã ẩn nút × để tránh đóng nhầm.", "The UI Automation scan was incomplete (a traversal or time limit was reached, or an element disappeared). The × is hidden to avoid closing the wrong tab." },
            new string[] { "Không tìm thấy tab nào trong {0} phần tử UIA. Đã ẩn nút ×.", "No tabs were found among {0} UI Automation elements. The × is hidden." },
            new string[] { "Đã thấy {0} tab nhưng không xác nhận được tab đang chọn. Đã ẩn nút ×.", "Found {0} tabs but could not confirm which one is selected. The × is hidden." },
            new string[] { "Có {0} tab cùng được đánh dấu đang chọn. Đã ẩn nút × để tránh đóng nhầm.", "{0} tabs report themselves as selected. The × is hidden to avoid closing the wrong tab." },
            new string[] { "Có tab chưa đọc được trạng thái chọn. Không thể xác nhận duy nhất một tab; đã ẩn nút ×.", "One or more tabs did not report their selection state, so a single selected tab cannot be confirmed. The × is hidden." },
            new string[] { "Tab đang chọn không nằm trong thanh tab ngang hợp lệ (có thể bị cuộn ra ngoài hoặc là tab ghim/dọc). Đã ẩn nút ×.", "The selected tab is not in a valid horizontal tab strip (it may be scrolled out, pinned or vertical). The × is hidden." },
            new string[] { "Tab đang chọn quá nhỏ để đặt nút × an toàn với kích thước đang chọn. Đã ẩn nút ×.", "The selected tab is too small to hold the × button safely at the configured size. The × is hidden." },
            new string[] { "Không đọc được vị trí cửa sổ Brave. Đã ẩn nút ×.", "The Brave window position could not be read. The × is hidden." },
            new string[] { "UI Automation không trả về phần tử gốc của cửa sổ Brave. Đã ẩn nút ×.", "UI Automation returned no root element for the Brave window. The × is hidden." },
            new string[] { "Quét UI Automation thất bại. Đã ẩn nút ×.", "The UI Automation scan failed. The × is hidden." },
            new string[] { "Quét UI Automation gặp lỗi bất ngờ. Đã ẩn nút ×.", "The UI Automation scan raised an unexpected error. The × is hidden." },

            // Controller idle reasons
            new string[] { "Đang khởi động. Mở Brave để nhận diện tab đang chọn.", "Starting up. Open Brave to detect the selected tab." },
            new string[] { "Brave chưa ở phía trước. Chuyển sang Brave để hiện nút ×; mở bảng điều khiển để xem trạng thái gần nhất.", "Brave is not in the foreground. Switch to Brave to show the ×; open the dashboard to see the last state." },
            new string[] { "Cửa sổ Brave đang thu nhỏ. Khôi phục Brave để hiện nút ×.", "The Brave window is minimized. Restore Brave to show the ×." },

            // Controller notices
            new string[] { "Brave không còn ở phía trước. Không tab nào bị đóng.", "Brave is no longer in the foreground. No tab was closed." },
            new string[] { "UI Automation còn đang xử lý lần xác nhận trước. Lần nhấp này đã bị bỏ qua.", "UI Automation is still processing the previous validation. This click was ignored." },
            new string[] { "Đang xác nhận tab đang chọn…", "Validating the selected tab…" },
            new string[] { "Trạng thái cửa sổ đã thay đổi. Không tab nào bị đóng.", "The window state changed during validation. No tab was closed." },
            new string[] { "Lần nhấp × bị bỏ qua vì chưa xác nhận chắc chắn tab đang chọn. Không tab nào bị đóng.", "The × click was ignored because the selected tab could not be confirmed with certainty. No tab was closed." },
            new string[] { "Cửa sổ hoặc phím bổ trợ đã thay đổi. Không tab nào bị đóng.", "The window or a modifier key changed. No tab was closed." },
            new string[] { "Đã gửi lệnh đóng tab đang chọn.", "The close command was sent to the selected tab." },
            new string[] { "Không gửi được lệnh đóng tab. Mở Chẩn đoán để xem chi tiết.", "The close command could not be delivered. Open Diagnostics for details." },
            new string[] { "Xác nhận đã bị hủy vì trạng thái thay đổi. Không tab nào bị đóng.", "Validation was cancelled because the state changed. No tab was closed." },
            new string[] { "Xác nhận nhấp đã hết hạn. Không tab nào bị đóng.", "The click validation expired. No tab was closed." },

            // Hidden reasons
            new string[] { "Nút × đang tắt.", "The × button is turned off." },
            new string[] { "UI Automation chưa phản hồi; nút × bị ẩn. Không có lệnh đóng tab.", "UI Automation has not responded; the × is hidden and no close command is sent." },
            new string[] { "Đang xác nhận lần nhấp; nút × tạm ẩn.", "Validating the click; the × is temporarily hidden." },
            new string[] { "Đang chờ sau lệnh đóng tab để tránh đóng liên tiếp.", "Waiting after the last close so that two tabs cannot be closed in a row." },
            new string[] { "Đang chờ quét cho trạng thái cửa sổ hiện tại.", "Waiting for a scan of the current window state." },
            new string[] { "Kết quả xác nhận đã cũ hoặc cửa sổ đã thay đổi; đang chờ quét mới.", "The confirmation is stale or the window changed; waiting for a new scan." },
            new string[] { "Chế độ chỉ hiện khi trỏ chuột: đưa con trỏ lên tab đang chọn để hiện nút ×.", "Hover mode: move the pointer over the selected tab to show the ×." },
            new string[] { "Không hiện được nút ×: {0}", "The × could not be shown: {0}" },

            // Dashboard
            new string[] { "Chỉ hiện nút đóng trên tab Brave đang được chọn", "Shows one close button on the selected Brave tab" },
            new string[] { "●  ĐANG KHỞI ĐỘNG", "●  STARTING" },
            new string[] { "●  NÚT × ĐANG TẮT", "●  × BUTTON OFF" },
            new string[] { "●  UIA KHÔNG PHẢN HỒI", "●  UIA NOT RESPONDING" },
            new string[] { "●  ĐANG HOẠT ĐỘNG", "●  ACTIVE" },
            new string[] { "●  CHỜ BRAVE", "●  WAITING FOR BRAVE" },
            new string[] { "●  SẴN SÀNG", "●  READY" },
            new string[] { "●  CHƯA NHẬN DIỆN", "●  NOT CONFIRMED" },
            new string[] { "Đang kiểm tra tab Brave…", "Checking the Brave tab…" },
            new string[] { "Nút × tùy chỉnh đang tắt", "The custom × button is off" },
            new string[] { "Brave chưa phản hồi trợ năng", "Brave is not responding to accessibility" },
            new string[] { "Nút × đang gắn với tab được chọn", "The × is attached to the selected tab" },
            new string[] { "Mở Brave để bắt đầu", "Open Brave to begin" },
            new string[] { "Nút × đang ẩn", "The × is hidden" },
            new string[] { "Chưa xác nhận được tab đang chọn", "The selected tab is not confirmed" },
            new string[] { "Xtension không hiện nút × và không gửi lệnh đóng tab.", "Xtension shows no × and sends no close command." },
            new string[] { "Nút × được ẩn cho đến khi Brave phản hồi. Xtension không đóng tab khi chưa xác nhận được.", "The × stays hidden until Brave responds. Xtension does not close a tab that it could not confirm." },
            new string[] { "Nút × chỉ đóng tab đang được chọn sau khi được xác nhận lại ngay trước khi nhấp.", "The × only closes the selected tab, after the selection is confirmed again immediately before the click." },
            new string[] { "Đang chờ kết quả quét đầu tiên.", "Waiting for the first scan result." },
            new string[] { "TAB ĐƯỢC NHẬN DIỆN", "CONFIRMED TAB" },
            new string[] { "Chưa có tab được xác nhận", "No confirmed tab yet" },
            new string[] { "(tab không có tiêu đề)", "(tab has no title)" },
            new string[] { "Tắt nút ×", "Turn the × off" },
            new string[] { "Bật nút ×", "Turn the × on" },
            new string[] { "Chẩn đoán", "Diagnostics" },
            new string[] { "Cài đặt Brave", "Brave settings" },
            new string[] { "Cài đặt Xtension", "Xtension settings" },
            new string[] { "Mở nhật ký", "Open log" },
            new string[] { "Thoát ứng dụng", "Exit Xtension" },
            new string[] { "Xtension không thay đổi cài đặt của Brave. Nếu Brave có tùy chọn ẩn nút đóng tab trong Cài đặt → Giao diện, hãy bật tùy chọn đó để giảm nhầm với nút đóng gốc. Nếu tắt nút × hoặc thoát ứng dụng, nút đóng gốc của Brave có thể vẫn bị ẩn cho đến khi bạn đổi lại tùy chọn đó trong Brave.", "Xtension never changes Brave's settings. If your Brave version offers a setting that hides the native tab close button, turn it on in Brave's Settings → Appearance to avoid confusion with the original button. Turning the × off or exiting Xtension does not change that Brave setting, so the native close buttons can stay hidden until you change it in Brave yourself." },
            new string[] { "Phiên bản {0} · Đóng cửa sổ này sẽ thu nhỏ ứng dụng xuống khay hệ thống.", "Version {0} · Closing this window keeps Xtension running in the notification area." },

            // Tray menu
            new string[] { "Mở bảng điều khiển", "Open dashboard" },
            new string[] { "Hiện nút × trên tab đang chọn", "Show the × on the selected tab" },
            new string[] { "Khởi chạy cùng Windows", "Start with Windows" },
            new string[] { "Chẩn đoán…", "Diagnostics…" },
            new string[] { "Mở cài đặt Giao diện Brave", "Open Brave appearance settings" },
            new string[] { "Cài đặt Xtension…", "Xtension settings…" },
            new string[] { "Mở nhật ký", "Open log" },
            new string[] { "Thoát ứng dụng", "Exit Xtension" },
            new string[] { " (nút × đang tắt)", " (× button off)" },

            // Dialogs
            new string[] { "Thoát ứng dụng", "Exit Xtension" },
            new string[] { "Thoát Xtension? Nút × tùy chỉnh sẽ ngừng hoạt động. Nếu Brave đang ẩn nút đóng gốc bằng tùy chọn của Brave, nút đóng gốc có thể vẫn bị ẩn cho đến khi bạn đổi tùy chọn đó trong Cài đặt Brave.\r\n\r\nBạn có chắc muốn thoát không?", "Exit Xtension? The custom × stops working. If Brave currently hides its native close buttons through Brave's own setting, those buttons can stay hidden until you change that setting in Brave.\r\n\r\nExit Xtension?" },
            new string[] { "Tắt nút ×", "Turn the × off" },
            new string[] { "Tắt nút × tùy chỉnh? Nếu Brave đang ẩn nút đóng gốc bằng tùy chọn của Brave, nút đóng gốc có thể vẫn bị ẩn. Khi đó bạn cần mở Cài đặt Brave để hiện lại.\r\n\r\nTiếp tục tắt nút ×?", "Turn the custom × off? If Brave currently hides its native close buttons through Brave's own setting, those buttons can stay hidden, and you would need Brave's settings to bring them back.\r\n\r\nTurn the × off?" },
            new string[] { "Khởi chạy cùng Windows", "Start with Windows" },
            new string[] { "Không thể cập nhật khởi chạy cùng Windows. Hãy kiểm tra quyền ghi Registry của tài khoản hiện tại.\r\n\r\n{0}", "The start-with-Windows entry could not be updated. Check that this account may write to the registry.\r\n\r\n{0}" },
            new string[] { "Cài đặt Brave", "Brave settings" },
            new string[] { "Không thể mở cài đặt tự động. Trong Brave, hãy mở:\r\nbrave://settings/appearance\r\n\r\n{0}", "Brave's settings page could not be opened automatically. In Brave, open:\r\nbrave://settings/appearance\r\n\r\n{0}" },
            new string[] { "Mở nhật ký", "Open log" },
            new string[] { "Không thể mở nhật ký:\r\n{0}", "The log file could not be opened:\r\n{0}" },
            new string[] { "Ứng dụng vẫn đang chạy", "Xtension is still running" },
            new string[] { "Xtension đã thu nhỏ xuống khay hệ thống. Nhấp biểu tượng gần đồng hồ để mở lại.", "Xtension was minimized to the notification area. Click its icon near the clock to open it again." },
            new string[] { "Không xác nhận được phiên Xtension duy nhất. Ứng dụng không khởi chạy để tránh đóng tab hai lần.", "A single Xtension session could not be confirmed. The app did not start, so a tab cannot be closed twice." },
            new string[] { "Xtension đã đang chạy nhưng không phản hồi. Hãy mở Trình quản lý tác vụ để đóng tiến trình Xtension cũ rồi thử lại.", "Xtension is already running but did not respond. Close the old Xtension process in Task Manager and try again." },
            new string[] { "Xtension không khởi động được. Chi tiết đã được ghi vào nhật ký:\r\n{0}", "Xtension could not start. Details were written to the log file:\r\n{0}" },
            new string[] { "Xtension log", "Xtension log" },

            // Diagnostics
            new string[] { "Chẩn đoán", "Diagnostics" },
            new string[] { "Tab / tên truy cập", "Tab / accessible name" },
            new string[] { "Đang chọn", "Selected" },
            new string[] { "Trong thanh ngang", "In the horizontal strip" },
            new string[] { "Vị trí màn hình (px)", "Screen position (px)" },
            new string[] { "Ngoài màn hình", "Offscreen" },
            new string[] { "Đang chờ kết quả quét…", "Waiting for a scan result…" },
            new string[] { "Nút × đang hiển thị.", "The × is visible." },
            new string[] { "\r\nQuét gần nhất: {0}   [{1}]", "\r\nLast scan: {0}   [{1}]" },
            new string[] { "Chưa có kết quả quét. Nút × được ẩn cho đến khi Xtension xác nhận được tab đang chọn.", "No scan result yet. The × stays hidden until Xtension confirms the selected tab." },
            new string[] { "CÓ", "YES" },
            new string[] { "không", "no" },
            new string[] { "không rõ", "unknown" },
            new string[] { "Quét {0} phần tử UIA trong {1} ms; DPI cửa sổ {2}.", "Scanned {0} UI Automation elements in {1} ms; window DPI {2}." },
            new string[] { " Nút × nằm sát mép phải tab được xác nhận.", " The × sits inside the right edge of the confirmed tab." },
            new string[] { " Nút × được ẩn để tránh đóng nhầm.", " The × is hidden to avoid closing the wrong tab." },
            new string[] { " Lỗi: {0}", " Error: {0}" },
            new string[] { "(không có)", "(none)" },
            new string[] { "(TabItem không có tên)", "(TabItem has no name)" },

            // Overlay
            new string[] { "Đóng tab Brave đang được chọn (nhấp một lần)", "Close the selected Brave tab (one click)" },
            new string[] { "Đóng tab Brave đang được chọn", "Close the selected Brave tab" },
            new string[] { "Chỉ đóng tab đã được xác nhận là tab đang hoạt động.", "Only closes the tab confirmed as the active one." },

            // Xtension Settings
            new string[] { "Cài đặt Xtension", "Xtension settings" },
            new string[] { "Cài đặt này thuộc Xtension. Brave không bị thay đổi.", "These settings belong to Xtension. Brave is not modified." },
            new string[] { "Nút × tùy chỉnh", "Custom × button" },
            new string[] { "Bật nút × tùy chỉnh", "Enable the custom × button" },
            new string[] { "Khi tắt, Xtension không hiện nút × và không gửi lệnh đóng tab. Đây không phải là thoát ứng dụng, và cũng không bật lại nút đóng gốc của Brave.", "When this is off, Xtension shows no × and sends no close command. This is not the same as exiting the app, and it does not bring back Brave's native close buttons." },
            new string[] { "Chế độ hiển thị", "Visibility mode" },
            new string[] { "Luôn hiện khi an toàn", "Always show when safe" },
            new string[] { "Chỉ hiện khi trỏ chuột lên tab đang chọn", "Show only when the pointer is over the selected tab" },
            new string[] { "Chế độ “luôn hiện khi an toàn” giữ nguyên hành vi cũ. Chế độ chỉ hiện khi trỏ chuột không hiện nút × khi con trỏ ở tab khác, thanh tiêu đề của Brave, thanh tác vụ Windows hay ứng dụng khác; nút × vẫn xuất hiện ngay khi con trỏ nằm trên tab đang được xác nhận hoặc trên chính nút ×.", "“Always show when safe” keeps the previous behaviour. The hover mode never shows the × when the pointer is over another tab, Brave's title bar, the Windows taskbar or another application; it appears as soon as the pointer is over the confirmed selected tab or the × itself." },
            new string[] { "Kích thước và vị trí", "Size and position" },
            new string[] { "Kích thước nút × (điểm ảo, 18–40)", "× button size (logical pixels, 18–40)" },
            new string[] { "Khoảng cách từ mép phải tab (điểm ảo, 0–12)", "Inset from the right edge of the tab (logical pixels, 0–12)" },
            new string[] { "Giá trị được nhân theo DPI của cửa sổ Brave. Nếu tab đang chọn không đủ chỗ cho kích thước đã chọn, Xtension ẩn nút × thay vì đặt nút ra ngoài tab.", "Values are scaled by the DPI of the Brave window. If the selected tab cannot contain the configured size, Xtension hides the × instead of placing it outside the tab." },
            new string[] { "Tùy chọn nâng cao", "Advanced options" },
            new string[] { "Thời gian giữ nút × sau khi rời tab (ms, 0–1000)", "Keep the × after leaving the tab (ms, 0–1000)" },
            new string[] { "Khoảng cách quét UI Automation (ms, 300–2000)", "UI Automation scan interval (ms, 300–2000)" },
            new string[] { "Thời gian giữ chỉ áp dụng cho chế độ trỏ chuột và giúp nút × không nhấp nháy khi con trỏ đi từ tab sang nút ×. Khoảng cách quét chỉ ảnh hưởng tốc độ cập nhật; mỗi lần nhấp × vẫn được xác nhận lại bằng một lần quét mới.", "The keep time applies to hover mode only and prevents flicker when the pointer moves from the tab onto the ×. The scan interval affects how quickly the button follows changes; every × click is still validated by its own fresh scan." },
            new string[] { "Chung", "General" },
            new string[] { "Ngôn ngữ", "Language" },
            new string[] { "Tiếng Việt", "Vietnamese" },
            new string[] { "English", "English" },
            new string[] { "Khởi chạy cùng Windows", "Start with Windows" },
            new string[] { "Chỉ ghi một mục vào Run của tài khoản hiện tại; không cần quyền quản trị. Mặc định là tắt.", "Writes one entry to the current user's Run key; no administrator rights are needed. Off by default." },
            new string[] { "Ghi chú về Brave: Xtension không sửa cài đặt Brave. Nếu bạn tắt nút × hoặc thoát Xtension, nút đóng gốc của Brave có thể vẫn đang bị ẩn bởi tùy chọn trong Brave, và bạn cần bật lại tùy chọn đó trong Cài đặt Brave.", "Note about Brave: Xtension does not change Brave's settings. If you turn the × off or exit Xtension, Brave's own close buttons may still be hidden by Brave's own setting, and you would need to change that setting in Brave." },
            new string[] { "Chẩn đoán…", "Diagnostics…" },
            new string[] { "Mở nhật ký", "Open log" },
            new string[] { "Khôi phục mặc định", "Reset to defaults" },
            new string[] { "Khôi phục tất cả cài đặt Xtension về mặc định? Nút × sẽ bật lại, chế độ hiển thị trở về “luôn hiện khi an toàn”, kích thước về 26, khoảng cách về 3, thời gian giữ về 250 ms, khoảng cách quét về 500 ms và ngôn ngữ về tiếng Việt. Mục Khởi chạy cùng Windows không bị thay đổi.", "Reset every Xtension setting to its default? The × turns back on, the mode returns to “always show when safe”, the size to 26, the inset to 3, the keep time to 250 ms, the scan interval to 500 ms and the language to Vietnamese. The start-with-Windows entry is not changed." },
            new string[] { "Đóng", "Close" },
            new string[] { "Đã khôi phục cài đặt mặc định.", "Defaults restored." },
            new string[] { "Không lưu được cài đặt: {0}", "The settings could not be saved: {0}" }
        };

        /// <summary>Number of entries in the table. Must equal the number of <see cref="StringId"/> values.</summary>
        public static int TableCount
        {
            get { return Table.Length; }
        }

        /// <summary>Current interface language. Unknown values fall back to Vietnamese.</summary>
        public static AppLanguage Language
        {
            get { return _language; }
            set { _language = value == AppLanguage.English ? AppLanguage.English : AppLanguage.Vietnamese; }
        }

        public static bool IsVietnamese
        {
            get { return _language == AppLanguage.Vietnamese; }
        }

        /// <summary>Parses the persisted language token. Anything unknown keeps the default (Vietnamese).</summary>
        public static AppLanguage ParseLanguage(string token, AppLanguage fallback)
        {
            if (string.Equals(token, "en", StringComparison.OrdinalIgnoreCase)) return AppLanguage.English;
            if (string.Equals(token, "vi", StringComparison.OrdinalIgnoreCase)) return AppLanguage.Vietnamese;
            return fallback;
        }

        /// <summary>Stable persisted token for a language.</summary>
        public static string LanguageToken(AppLanguage language)
        {
            return language == AppLanguage.English ? "en" : "vi";
        }

        /// <summary>Text for an id. Never returns null and never throws, whatever the table contains.</summary>
        public static string Get(StringId id)
        {
            int index = (int)id;
            if (index < 0 || index >= Table.Length) return string.Empty;
            string[] pair = Table[index];
            if (pair == null || pair.Length < 2) return string.Empty;
            string text = IsVietnamese ? pair[0] : pair[1];
            return text == null ? string.Empty : text;
        }

        public static string Get(StringId id, object arg0)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(id), new object[] { arg0 });
        }

        public static string Get(StringId id, object arg0, object arg1)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(id), new object[] { arg0, arg1 });
        }

        public static string Get(StringId id, object arg0, object arg1, object arg2)
        {
            return string.Format(CultureInfo.InvariantCulture, Get(id), new object[] { arg0, arg1, arg2 });
        }

        /// <summary>
        /// Localized description of a scan result, derived from its key and counters. Rendering it on demand
        /// (instead of storing text in the result) is what makes a language change take effect immediately.
        /// </summary>
        public static string Describe(ScanResult result)
        {
            if (result == null) return Get(StringId.HiddenWaitingScan);
            switch (result.Key)
            {
                case ScanMessageKey.Ready:
                    return Get(StringId.ScanReady, result.Tabs.Count);
                case ScanMessageKey.Incomplete:
                    return Get(StringId.ScanIncomplete);
                case ScanMessageKey.NoTabs:
                    return Get(StringId.ScanNoTabs, result.NodesVisited);
                case ScanMessageKey.NoSelectedTab:
                    return Get(StringId.ScanNoSelectedTab, result.Tabs.Count);
                case ScanMessageKey.AmbiguousSelection:
                    return Get(StringId.ScanAmbiguousSelection, result.AmbiguousCount);
                case ScanMessageKey.UnknownSelection:
                    return Get(StringId.ScanUnknownSelection);
                case ScanMessageKey.SelectedNotHorizontal:
                    return Get(StringId.ScanSelectedNotHorizontal);
                case ScanMessageKey.SelectedTabTooSmall:
                    return Get(StringId.ScanSelectedTabTooSmall);
                case ScanMessageKey.WindowBoundsUnavailable:
                    return Get(StringId.ScanWindowBoundsUnavailable);
                case ScanMessageKey.WindowRootUnavailable:
                    return Get(StringId.ScanWindowRootUnavailable);
                case ScanMessageKey.ScanFailed:
                    return Get(StringId.ScanFailed);
                case ScanMessageKey.WorkerFailed:
                    return Get(StringId.ScanWorkerFailed);
                default:
                    return Get(StringId.HiddenWaitingScan);
            }
        }
    }
}
