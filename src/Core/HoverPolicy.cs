using System.Drawing;

namespace Xtension
{
    /// <summary>
    /// The screen region that makes the × eligible in hover mode: the confirmed selected tab, plus the × itself
    /// (which normally lies inside that tab). Nothing else is eligible, so the pointer being over another tab,
    /// Brave's title bar, the Windows taskbar or another application can never show the button.
    /// </summary>
    internal static class HoverRegion
    {
        public static Rectangle ForSelectedTab(Rectangle tabBounds, Rectangle overlayBounds)
        {
            if (tabBounds.IsEmpty) return Rectangle.Empty;
            if (overlayBounds.IsEmpty) return tabBounds;
            return Rectangle.Union(tabBounds, overlayBounds);
        }

        public static bool Contains(Rectangle region, Point pointer)
        {
            if (region.IsEmpty) return false;
            return region.Contains(pointer);
        }
    }

    /// <summary>
    /// Pure hover transition state machine for hover mode. It only decides visibility; eligibility (foreground
    /// Brave window, fresh Ready scan, no stall, no cooldown, overlay enabled) is decided by the caller, so a
    /// hover decision can never authorize a close on its own.
    ///
    /// Transitions:
    /// - Not eligible: hidden immediately and hover state cleared, with no grace at all.
    /// - Pointer inside the eligible region: visible immediately.
    /// - Pointer outside: visible for at most the configured grace, then hidden.
    /// - <see cref="Reset"/> (tab switch, foreground change, geometry change, mode change, scan failure, click):
    ///   hover state is cleared, so no stale hover survives.
    /// </summary>
    internal sealed class HoverTracker
    {
        private bool _pointerInside;
        private bool _visible;
        private long _leftAtMs = Clock.Never;

        public bool Visible
        {
            get { return _visible; }
        }

        public bool PointerInside
        {
            get { return _pointerInside; }
        }

        /// <summary>Forgets all hover state, including any pending grace period.</summary>
        public void Reset()
        {
            _pointerInside = false;
            _visible = false;
            _leftAtMs = Clock.Never;
        }

        /// <summary>
        /// Advances the state machine and returns whether the × may be visible for this update.
        /// Callers must pass the current pointer sample; times are monotonic milliseconds.
        /// </summary>
        public bool Update(bool eligible, bool pointerInside, long nowMs, int graceMs)
        {
            if (!eligible)
            {
                Reset();
                return false;
            }

            if (pointerInside)
            {
                _pointerInside = true;
                _visible = true;
                _leftAtMs = Clock.Never;
                return true;
            }

            if (_pointerInside)
            {
                // The pointer just left: start the bounded grace window.
                _pointerInside = false;
                _leftAtMs = nowMs;
            }

            if (!_visible) return false;
            if (graceMs <= 0)
            {
                _visible = false;
                return false;
            }
            if (_leftAtMs == Clock.Never) _leftAtMs = nowMs;
            if (nowMs < _leftAtMs)
            {
                // An impossible ordering (the clock moved backwards) must not extend the grace window: fail closed.
                _visible = false;
                return false;
            }
            if (nowMs - _leftAtMs >= graceMs) _visible = false;
            return _visible;
        }
    }
}
