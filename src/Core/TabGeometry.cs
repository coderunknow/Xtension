using System;
using System.Drawing;

namespace Xtension
{
    /// <summary>
    /// Pure geometry rules. Constants are defined at 96 DPI and scaled by the Brave window's DPI,
    /// so a 200 % display uses thresholds that match its physical tab size.
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

        private const int BaseOverlaySize = 26;
        private const int BaseOverlayInset = 3;

        public static double Scale(int dpi)
        {
            int effective = dpi > 0 ? dpi : BaseDpi;
            return effective / (double)BaseDpi;
        }

        private static int Px(int baseValue, int dpi)
        {
            return (int)Math.Round(baseValue * Scale(dpi));
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
        /// Returns Rectangle.Empty when the tab is too small to host the button safely.
        /// </summary>
        public static Rectangle GetOverlayBounds(Rectangle tab, int dpi)
        {
            if (tab.IsEmpty) return Rectangle.Empty;
            if (tab.Width < Px(BaseMinTabWidth, dpi) || tab.Height < Px(BaseMinTabHeight, dpi)) return Rectangle.Empty;

            int size = Px(BaseOverlaySize, dpi);
            int inset = Px(BaseOverlayInset, dpi);
            int x = tab.Right - size - inset;
            int y = tab.Top + (tab.Height - size) / 2;
            return new Rectangle(x, y, size, size);
        }

        /// <summary>Logical overlay size used by the painter at the given DPI.</summary>
        public static int OverlaySize(int dpi)
        {
            return Px(BaseOverlaySize, dpi);
        }
    }
}
