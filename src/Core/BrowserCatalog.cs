using System;
using System.Collections.Generic;

namespace Xtension
{
    /// <summary>Which browser a window belongs to. None is "not a browser this build can identify".</summary>
    internal enum BrowserKind
    {
        None = 0,
        /// <summary>Chromium-family browser, verified against a real installation by the release acceptance run.</summary>
        Brave = 1,
        /// <summary>Chromium-family browser; the accessibility contract is the same implementation as Brave's.</summary>
        Chrome = 2,
        /// <summary>Chromium-family browser from Microsoft; same contract as the other two.</summary>
        Edge = 3,
        /// <summary>Gecko-family browser. Its accessibility tree is a different implementation and is not assumed to match.</summary>
        Firefox = 4
    }

    /// <summary>
    /// How a browser exposes its tab strip. This is the boundary between "we can read this browser's selected tab"
    /// and "we assume we can": each family has its own accessibility implementation, and sharing a rendering engine
    /// is not evidence that the tab contract matches. A scanner must state which contract it implements and refuse
    /// everything else, so a new browser is added by implementing its contract, never by widening a shared path.
    /// </summary>
    internal enum TabAccessContract
    {
        /// <summary>No implemented contract. The window is never scanned and the × is never placed in it.</summary>
        None = 0,
        /// <summary>
        /// Blink/Chromium: the tab strip is a TabList whose children are TabItem elements, the selected one reports
        /// SelectionItemPattern.IsSelected, and each TabItem exposes a BoundingRectangle that is the tab rectangle.
        /// This is the contract exercised by the real-browser acceptance run for Brave.
        /// </summary>
        ChromiumSelectionItem = 1,
        /// <summary>
        /// Gecko: Firefox exposes tabs through its own accessibility bridge (IAccessible2 surfaced as UIA). Its tab
        /// list, selected-state and geometry contract has not been exercised here, so it is declared but not
        /// implemented and the scanner refuses it.
        /// </summary>
        GeckoSelectionItem = 2
    }

    /// <summary>
    /// How far a browser has been verified. A browser is only offered to a user at or above
    /// <see cref="BrowserSupport.Experimental"/>, and the × is only placed for a browser the user enabled, so a browser
    /// whose tree has not been exercised cannot receive an active button merely because its UI looks familiar.
    /// </summary>
    internal enum BrowserSupport
    {
        /// <summary>No verified tab-confirmation behaviour. The app refuses to treat such a window as a target.</summary>
        Unsupported = 0,
        /// <summary>The window and process model is verified; the tab scan is exercised in CI, confirmation may still refuse.</summary>
        Experimental = 1,
        /// <summary>Real-browser acceptance for placement and an actual single-tab close has passed.</summary>
        Verified = 2
    }

    /// <summary>One browser Xtension knows about: identity, token, family and support level.</summary>
    internal sealed class BrowserDescriptor
    {
        public BrowserKind Kind;
        /// <summary>Stable persisted token. Never localized.</summary>
        public string Token;
        /// <summary>Exact executable file name compared against the process image name.</summary>
        public string ExecutableName;
        /// <summary>Brand name for log lines and diagnostic reports. Proper noun, identical in every language.</summary>
        public string DisplayName;
        public BrowserSupport Support;
        /// <summary>Accessibility contract the scanner implements for this browser, or None.</summary>
        public TabAccessContract Contract;
        /// <summary>True when at least one contract the scanner implements covers this browser.</summary>
        public bool ScannerImplemented;
    }

    /// <summary>
    /// The single place where "which browsers does this build support" is decided. The tray, the controller, the
    /// settings window and the diagnostics all read this table, so a support level cannot disagree between them.
    /// A window is only ever identified through its process image name (see ProcessIdentity); the catalog never
    /// matches a window title, a class name or an unverified substring.
    /// </summary>
    internal static class BrowserCatalog
    {
        public const int NoBrowsers = 0;

        private static readonly BrowserDescriptor[] Items = new BrowserDescriptor[]
        {
            new BrowserDescriptor { Kind = BrowserKind.Brave, Token = "brave", ExecutableName = "brave.exe",
                DisplayName = "Brave", Support = BrowserSupport.Verified,
                Contract = TabAccessContract.ChromiumSelectionItem, ScannerImplemented = true },
            new BrowserDescriptor { Kind = BrowserKind.Chrome, Token = "chrome", ExecutableName = "chrome.exe",
                DisplayName = "Google Chrome", Support = BrowserSupport.Experimental,
                Contract = TabAccessContract.ChromiumSelectionItem, ScannerImplemented = true },
            new BrowserDescriptor { Kind = BrowserKind.Edge, Token = "edge", ExecutableName = "msedge.exe",
                DisplayName = "Microsoft Edge", Support = BrowserSupport.Experimental,
                Contract = TabAccessContract.ChromiumSelectionItem, ScannerImplemented = true },
            new BrowserDescriptor { Kind = BrowserKind.Firefox, Token = "firefox", ExecutableName = "firefox.exe",
                DisplayName = "Mozilla Firefox", Support = BrowserSupport.Unsupported,
                Contract = TabAccessContract.GeckoSelectionItem, ScannerImplemented = false }
        };

        /// <summary>
        /// Recorded real-browser acceptance for every browser this build declares as verified. Promotion to verified
        /// is impossible without an entry here, because the static check refuses the claim otherwise: sharing a
        /// rendering engine with a verified browser is never accepted as evidence (the Chrome and Edge entries below
        /// declare the same contract and are deliberately still unsupported, because no acceptance run has been made
        /// for them).
        /// </summary>
        private static readonly string[] AcceptanceEvidence = new string[]
        {
            "brave@1.97.56: isolated profile, real window, overlay placed inside the confirmed selected tab on the CI runner (Windows Server 2022, x86 and x64); single close removed the confirmed tab"
        };

        /// <summary>Every browser this build knows about, in a stable order.</summary>
        public static BrowserDescriptor[] All
        {
            get { return (BrowserDescriptor[])Items.Clone(); }
        }

        public static bool TryGet(BrowserKind kind, out BrowserDescriptor descriptor)
        {
            foreach (BrowserDescriptor item in Items)
            {
                if (item.Kind == kind)
                {
                    descriptor = item;
                    return true;
                }
            }
            descriptor = null;
            return false;
        }

        /// <summary>
        /// Descriptor for a process image file name, or null when the name is not one of the known browsers.
        /// Comparison is ordinal and case-insensitive on the exact file name; nothing about the window is trusted.
        /// </summary>
        public static BrowserDescriptor ForExecutable(string fileName)
        {
            if (string.IsNullOrEmpty(fileName)) return null;
            foreach (BrowserDescriptor item in Items)
            {
                if (string.Equals(item.ExecutableName, fileName, StringComparison.OrdinalIgnoreCase)) return item;
            }
            return null;
        }

        /// <summary>Stable persisted token for a browser, or an empty string for None.</summary>
        public static string Token(BrowserKind kind)
        {
            BrowserDescriptor descriptor;
            return TryGet(kind, out descriptor) ? descriptor.Token : string.Empty;
        }

        /// <summary>Parses a persisted token. An unknown token is None, which is never a target.</summary>
        public static BrowserKind ParseToken(string token)
        {
            if (string.IsNullOrEmpty(token)) return BrowserKind.None;
            string trimmed = token.Trim();
            foreach (BrowserDescriptor item in Items)
            {
                if (string.Equals(item.Token, trimmed, StringComparison.OrdinalIgnoreCase)) return item.Kind;
            }
            return BrowserKind.None;
        }

        /// <summary>True when the scanner really implements a contract for this browser.</summary>
        public static bool IsScannerImplemented(BrowserKind kind)
        {
            BrowserDescriptor descriptor;
            return TryGet(kind, out descriptor) && descriptor.ScannerImplemented;
        }

        /// <summary>True when the scanner implements this contract.</summary>
        public static bool IsImplemented(TabAccessContract contract)
        {
            return contract == TabAccessContract.ChromiumSelectionItem;
        }

        /// <summary>Stable, non-localized name of a contract, for logs and diagnostic reports.</summary>
        public static string ContractToken(TabAccessContract contract)
        {
            if (contract == TabAccessContract.ChromiumSelectionItem) return "chromium-selectionitem";
            if (contract == TabAccessContract.GeckoSelectionItem) return "gecko-selectionitem";
            return "none";
        }

        /// <summary>Recorded acceptance evidence for a browser, or an empty string when none is recorded.</summary>
        public static string EvidenceFor(BrowserKind kind)
        {
            string token = Token(kind);
            if (string.IsNullOrEmpty(token)) return string.Empty;
            string prefix = token + "@";
            foreach (string entry in AcceptanceEvidence)
            {
                if (entry.StartsWith(prefix, StringComparison.Ordinal)) return entry;
            }
            return string.Empty;
        }

        /// <summary>Every browser whose support level lets a user enable it, in catalog order.</summary>
        public static BrowserDescriptor[] Offerable()
        {
            List<BrowserDescriptor> offerable = new List<BrowserDescriptor>();
            foreach (BrowserDescriptor item in Items)
            {
                if (IsSupported(item.Kind)) offerable.Add(item);
            }
            return offerable.ToArray();
        }

        public static bool IsSupported(BrowserKind kind)
        {
            BrowserDescriptor descriptor;
            return TryGet(kind, out descriptor) && descriptor.Support != BrowserSupport.Unsupported;
        }

        /// <summary>Mask of every browser whose support level allows the × to be offered at all.</summary>
        public static int SupportedMask()
        {
            int mask = NoBrowsers;
            foreach (BrowserDescriptor item in Items)
            {
                if (item.Support != BrowserSupport.Unsupported) mask |= Mask(item.Kind);
            }
            return mask;
        }

        public static int Mask(BrowserKind kind)
        {
            return kind == BrowserKind.None ? NoBrowsers : 1 << (int)kind;
        }

        /// <summary>
        /// True when the user's selection includes this browser. A browser whose tab-access contract this build does
        /// not implement is never enabled, whatever the mask says, so a call site that forgets to check support
        /// separately still cannot scan an unverified browser.
        /// </summary>
        public static bool IsEnabled(int browserMask, BrowserKind kind)
        {
            if (kind == BrowserKind.None) return false;
            if (!IsSupported(kind)) return false;
            return (browserMask & Mask(kind)) != NoBrowsers;
        }

        /// <summary>
        /// Parses a comma-separated token list into a mask. Unknown tokens and browsers below the experimental level
        /// are dropped, so a hand-edited file cannot enable a browser this build refuses to confirm.
        /// </summary>
        public static int ParseMask(string tokenList)
        {
            if (string.IsNullOrEmpty(tokenList)) return NoBrowsers;
            int mask = NoBrowsers;
            string[] parts = tokenList.Split(new char[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string part in parts)
            {
                BrowserKind kind = ParseToken(part);
                if (kind != BrowserKind.None && IsSupported(kind)) mask |= Mask(kind);
            }
            return mask;
        }

        /// <summary>Stable token list for a mask, in catalog order. Only browsers this build supports are written.</summary>
        public static string MaskTokenList(int browserMask)
        {
            List<string> tokens = new List<string>();
            foreach (BrowserDescriptor item in Items)
            {
                if (IsSupported(item.Kind) && IsEnabled(browserMask, item.Kind)) tokens.Add(item.Token);
            }
            return string.Join(",", tokens.ToArray());
        }

        /// <summary>Descriptors the user enabled, in catalog order. Used by the settings window and diagnostics.</summary>
        public static BrowserDescriptor[] Enabled(int browserMask)
        {
            List<BrowserDescriptor> enabled = new List<BrowserDescriptor>();
            foreach (BrowserDescriptor item in Items)
            {
                if (IsSupported(item.Kind) && IsEnabled(browserMask, item.Kind)) enabled.Add(item);
            }
            return enabled.ToArray();
        }
    }
}
