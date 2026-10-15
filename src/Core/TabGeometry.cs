using System;
using System.Drawing;

namespace Xtension
{
    /// <summary>
    /// Pure geometry rules. Constants are defined at 96 DPI and scaled by the Brave window's DPI,
    /// so a 200 % display uses thresholds that match its physical tab size.
    /// The × size and its inset from the tab's right edge are configurable values in logical pixels; every
    /// caller (painting, placement, hit testing and close validation) uses this class, so the numbers agree.
    /// </summary>
    internal static class TabGeometry
    {
        public const int BaseDpi = 96;

        private const int BaseMinTabWidth = 55;
        private const int BaseMaxTabWidth = 650;
        private const int BaseMinTabHeight = 16;
        private const int BaseMaxTabHeight = 65;
        // Tab top may sit slightly above the window rectangle (shadow borders) and must lie in the top strip.
        private const int BaseTabTopMin = -5;
        private const int BaseTabTopMax = 105;
        private const int BaseTabBottomMax = 120;
        private const int BaseSideTolerance = 5;
        // A candidate tab may start this far above the window's top edge (window shadow borders).
        private const int BaseTabTopMargin = 5;

        public static double Scale(int dpi)
        {
            int effective = dpi > 0 ? dpi : BaseDpi;
            return effective / (double)BaseDpi;
        }

        private static int Px(int baseValue, int dpi)
        {
            return (int)Math.Round(baseValue * Scale(dpi));
        }

        /// <summary>Clamps a persisted or user-entered × size into the supported logical range.</summary>
        public static int ClampButtonSize(int logical)
        {
            if (logical < AppInfo.MinButtonSizeLogical) return AppInfo.MinButtonSizeLogical;
            if (logical > AppInfo.MaxButtonSizeLogical) return AppInfo.MaxButtonSizeLogical;
            return logical;
        }

        /// <summary>Clamps a persisted or user-entered inset into the supported logical range.</summary>
        public static int ClampButtonInset(int logical)
        {
            if (logical < AppInfo.MinButtonInsetLogical) return AppInfo.MinButtonInsetLogical;
            if (logical > AppInfo.MaxButtonInsetLogical) return AppInfo.MaxButtonInsetLogical;
            return logical;
        }

        /// <summary>Physical pixel size of the × for a logical size at the given DPI. Never below 1.</summary>
        public static int OverlaySize(int dpi, int sizeLogical)
        {
            int size = Px(ClampButtonSize(sizeLogical), dpi);
            return size < 1 ? 1 : size;
        }

        /// <summary>True when a TabItem rectangle looks like a horizontal tab in the top strip of the window.</summary>
        public static bool IsHorizontalCandidate(Rectangle tab, Rectangle window, int dpi)
        {
            if (tab.IsEmpty || window.IsEmpty) return false;
            if (tab.Width < Px(BaseMinTabWidth, dpi) || tab.Width > Px(BaseMaxTabWidth, dpi)) return false;
            if (tab.Height < Px(BaseMinTabHeight, dpi) || tab.Height > Px(BaseMaxTabHeight, dpi)) return false;

            int relativeTop = tab.Top - window.Top;
            int relativeBottom = tab.Bottom - window.Top;
            if (relativeTop < Px(BaseTabTopMin, dpi) || relativeTop > Px(BaseTabTopMax, dpi)) return false;
            if (relativeBottom > Px(BaseTabBottomMax, dpi)) return false;

            int tolerance = Px(BaseSideTolerance, dpi);
            if (tab.Left < window.Left - tolerance || tab.Right > window.Right + tolerance) return false;
            return true;
        }

        /// <summary>
        /// Screen rectangle of the × button for a tab: a square at the right edge, vertically centred.
        /// Returns Rectangle.Empty when the tab cannot contain the configured size safely, which hides the button.
        /// </summary>
        public static Rectangle GetOverlayBounds(Rectangle tab, int dpi, int sizeLogical, int insetLogical)
        {
            if (tab.IsEmpty) return Rectangle.Empty;
            if (tab.Width < Px(BaseMinTabWidth, dpi) || tab.Height < Px(BaseMinTabHeight, dpi)) return Rectangle.Empty;

            int size = OverlaySize(dpi, sizeLogical);
            int inset = Px(ClampButtonInset(insetLogical), dpi);
            if (size <= 0 || inset < 0) return Rectangle.Empty;
            // The whole square plus the inset must fit inside the tab, or the button is not shown at all.
            if (tab.Height < size || tab.Width < size + inset) return Rectangle.Empty;
            int x = tab.Right - size - inset;
            int y = tab.Top + (tab.Height - size) / 2;
            Rectangle bounds = new Rectangle(x, y, size, size);
            if (!tab.Contains(bounds)) return Rectangle.Empty;
            return bounds;
        }

        /// <summary>
        /// Bottom edge (screen Y) that a horizontal tab can reach, relative to the top of the window. Used by
        /// hover mode to avoid UI Automation work while the pointer is far below the tab strip.
        /// </summary>
        public static int TabStripBottomLimit(int dpi)
        {
            return Px(BaseTabBottomMax, dpi);
        }

        /// <summary>
        /// Conservative screen region of the whole horizontal tab strip: the top strip of the window plus the
        /// horizontal range of the window. A tab candidate must lie inside this region, so a pointer outside it
        /// cannot be over a tab. Returns Rectangle.Empty when the window bounds are unusable, which callers treat
        /// as "unknown" and therefore scan.
        /// </summary>
        public static Rectangle GetTabStripRegion(Rectangle window, int dpi)
        {
            if (window.IsEmpty) return Rectangle.Empty;
            int tolerance = Px(BaseSideTolerance, dpi);
            // A candidate tab's top may sit slightly above the window top, so the region starts a little higher.
            int top = window.Top - Px(BaseTabTopMargin, dpi);
            int bottom = window.Top + TabStripBottomLimit(dpi) + tolerance;
            return Rectangle.FromLTRB(window.Left - tolerance, top, window.Right + tolerance, bottom);
        }
    }
}
