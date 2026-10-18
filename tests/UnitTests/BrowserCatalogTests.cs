using System;

namespace Xtension.Tests
{
    /// <summary>
    /// The browser-provider boundary. These tests exist because the cheapest way to "support" another browser is to
    /// assume it behaves like the verified one, and that assumption is exactly what must be impossible to make
    /// silently: identity is an exact process image name, support is only granted with recorded acceptance evidence,
    /// and the scanner only implements contracts it declares.
    /// </summary>
    internal static class BrowserCatalogTests
    {
        [Test]
        public static void IdentityIsAnExactProcessImageNameAndNothingElse()
        {
            Assert.AreEqual(BrowserKind.Brave, BrowserCatalog.ForExecutable("brave.exe").Kind, "brave.exe identity");
            Assert.AreEqual(BrowserKind.Brave, BrowserCatalog.ForExecutable("BRAVE.EXE").Kind, "identity is case-insensitive");
            Assert.AreEqual(BrowserKind.Chrome, BrowserCatalog.ForExecutable("chrome.exe").Kind, "chrome.exe identity");
            Assert.AreEqual(BrowserKind.Edge, BrowserCatalog.ForExecutable("msedge.exe").Kind, "msedge.exe identity");
            Assert.AreEqual(BrowserKind.Firefox, BrowserCatalog.ForExecutable("firefox.exe").Kind, "firefox.exe identity");

            // Anything that merely contains or resembles a browser name must not be identified as one.
            Assert.IsTrue(BrowserCatalog.ForExecutable("brave.exe.bak") == null, "a decorated name is not the executable");
            Assert.IsTrue(BrowserCatalog.ForExecutable("notbrave.exe") == null, "a substring is not the executable");
            Assert.IsTrue(BrowserCatalog.ForExecutable("brave-update.exe") == null, "the updater is not the browser");
            Assert.IsTrue(BrowserCatalog.ForExecutable("") == null, "an empty name identifies nothing");
            Assert.IsTrue(BrowserCatalog.ForExecutable(null) == null, "a missing name identifies nothing");
            Assert.IsTrue(BrowserCatalog.ForExecutable("explorer.exe") == null, "a non-browser is not a browser");
        }

        [Test]
        public static void ASharedEngineIsNeverEvidenceOfSupport()
        {
            // Edge shares the family but failed independent acceptance: matching an engine does not grant support.
            Assert.AreEqual(TabAccessContract.ChromiumSelectionItem, BrowserCatalog.ForExecutable("chrome.exe").Contract,
                "Chrome declares the Chromium contract");
            Assert.AreEqual(BrowserCatalog.ForExecutable("brave.exe").Contract, BrowserCatalog.ForExecutable("chrome.exe").Contract,
                "the two Chromium browsers really do share a contract");
            Assert.IsTrue(BrowserCatalog.IsSupported(BrowserKind.Brave), "Brave is verified");
            Assert.IsTrue(BrowserCatalog.IsSupported(BrowserKind.Chrome), "Chrome implements the tested contract");
            Assert.IsFalse(BrowserCatalog.IsSupported(BrowserKind.Edge), "Edge failed independent acceptance and is quarantined");
            Assert.IsFalse(BrowserCatalog.IsSupported(BrowserKind.Firefox), "Firefox is not verified");
            Assert.IsFalse(BrowserCatalog.IsSupported(BrowserKind.None), "no window is not a supported browser");
        }

        [Test]
        public static void EveryVerifiedBrowserCarriesRecordedAcceptanceEvidence()
        {
            foreach (BrowserDescriptor descriptor in BrowserCatalog.All)
            {
                if (descriptor.Support != BrowserSupport.Verified) continue;
                string evidence = BrowserCatalog.EvidenceFor(descriptor.Kind);
                Assert.IsTrue(!string.IsNullOrEmpty(evidence),
                    descriptor.Token + " is declared verified without recorded real-browser acceptance evidence");
                Assert.IsTrue(evidence.StartsWith(descriptor.Token + "@", StringComparison.Ordinal),
                    descriptor.Token + " evidence must name the browser and the build version it was recorded for: " + evidence);
            }
            Assert.IsTrue(!string.IsNullOrEmpty(BrowserCatalog.EvidenceFor(BrowserKind.Chrome)), "Chrome carries independent actual-browser evidence");
            Assert.IsTrue(string.IsNullOrEmpty(BrowserCatalog.EvidenceFor(BrowserKind.Edge)), "Edge must not carry acceptance evidence it did not earn");
        }

        [Test]
        public static void OnlyAnImplementedContractCanBeScanned()
        {
            Assert.IsTrue(BrowserCatalog.IsImplemented(TabAccessContract.ChromiumSelectionItem),
                "the Chromium SelectionItem contract is implemented and exercised by the Brave acceptance run");
            Assert.IsFalse(BrowserCatalog.IsImplemented(TabAccessContract.GeckoSelectionItem),
                "the Gecko contract is declared but not implemented, so Firefox windows must never be walked");
            Assert.IsFalse(BrowserCatalog.IsImplemented(TabAccessContract.None), "no contract is not implemented");
            Assert.IsTrue(BrowserCatalog.IsScannerImplemented(BrowserKind.Brave), "Brave has an implemented contract");
            Assert.IsFalse(BrowserCatalog.IsScannerImplemented(BrowserKind.Firefox), "Firefox has no implemented contract");
            Assert.IsFalse(BrowserCatalog.IsScannerImplemented(BrowserKind.None), "nothing to implement for no window");

            Assert.AreEqual("chromium-selectionitem", BrowserCatalog.ContractToken(TabAccessContract.ChromiumSelectionItem), "Chromium token");
            Assert.AreEqual("gecko-selectionitem", BrowserCatalog.ContractToken(TabAccessContract.GeckoSelectionItem), "Gecko token");
            Assert.AreEqual("none", BrowserCatalog.ContractToken(TabAccessContract.None), "no-contract token");
        }

        [Test]
        public static void OfferableBrowsersAreExactlyTheSupportedOnes()
        {
            BrowserDescriptor[] offerable = BrowserCatalog.Offerable();
            Assert.AreEqual(2, offerable.Length, "only accepted Brave and Chrome are offered");
            Assert.AreEqual(BrowserKind.Brave, offerable[0].Kind, "the offered browser is the verified one");
            foreach (BrowserDescriptor descriptor in offerable)
            {
                Assert.IsTrue(BrowserCatalog.IsSupported(descriptor.Kind), descriptor.Token + " must be supported to be offered");
                Assert.IsTrue(descriptor.ScannerImplemented, descriptor.Token + " must have an implemented scanner to be offered");
            }
        }

        [Test]
        public static void MasksAndTokensRoundTripAndUnsupportedTokensAreDropped()
        {
            Assert.AreEqual("brave,chrome", BrowserCatalog.MaskTokenList(BrowserCatalog.SupportedMask()), "supported mask");
            int mixed = BrowserCatalog.Mask(BrowserKind.Brave) | BrowserCatalog.Mask(BrowserKind.Chrome);
            Assert.AreEqual("brave,chrome", BrowserCatalog.MaskTokenList(mixed), "opt-in browsers round trip");
            Assert.AreEqual(mixed, BrowserCatalog.ParseMask("brave,chrome"), "explicit opt in");
            Assert.AreEqual(BrowserCatalog.NoBrowsers, BrowserCatalog.ParseMask("firefox"), "unimplemented provider refuses");
            Assert.AreEqual(BrowserCatalog.Mask(BrowserKind.Brave), AppSettings.Defaults().Browsers, "default remains Brave only");
            Assert.AreEqual(BrowserCatalog.NoBrowsers, BrowserCatalog.ParseMask(null), "an empty list enables nothing");
            Assert.AreEqual(BrowserCatalog.Mask(BrowserKind.Brave), BrowserCatalog.ParseMask("BRAVE"), "tokens are case-insensitive");
            Assert.AreEqual(BrowserKind.Brave, BrowserCatalog.ParseToken(BrowserCatalog.Token(BrowserKind.Brave)), "token round trip");
            Assert.AreEqual(BrowserKind.None, BrowserCatalog.ParseToken("netscape"), "an unknown token is not a browser");
            Assert.AreEqual(BrowserCatalog.NoBrowsers, BrowserCatalog.Mask(BrowserKind.None), "no window has no mask bit");
            Assert.AreEqual(BrowserCatalog.NoBrowsers, BrowserCatalog.SupportedMask() & BrowserCatalog.Mask(BrowserKind.Firefox),
                "an unimplemented browser is not in the supported mask");

            BrowserDescriptor[] enabled = BrowserCatalog.Enabled(mixed);
            Assert.AreEqual(2, enabled.Length, "only explicitly enabled browsers are reported");
            Assert.IsTrue(BrowserCatalog.IsEnabled(mixed, BrowserKind.Brave), "the enabled browser is reported enabled");
            Assert.IsTrue(BrowserCatalog.IsEnabled(mixed, BrowserKind.Chrome), "Chrome requires explicit opt in");
            Assert.IsFalse(BrowserCatalog.IsEnabled(BrowserCatalog.NoBrowsers, BrowserKind.Brave), "an empty mask enables nothing");
            Assert.IsFalse(BrowserCatalog.IsEnabled(mixed, BrowserKind.None), "no window is never enabled");
        }

        [Test]
        public static void FailedEdgeProviderCannotBeEnabledByFileOrImport()
        {
            Assert.IsFalse(BrowserCatalog.IsScannerImplemented(BrowserKind.Edge), "shared contract cannot override quarantine");
            Assert.IsFalse(BrowserCatalog.IsEnabled(int.MaxValue, BrowserKind.Edge), "even hostile mask cannot enable Edge");
            Assert.AreEqual(BrowserCatalog.NoBrowsers, BrowserCatalog.ParseMask("edge"), "file cannot opt into failed provider");
            BackupImportResult imported = SettingsBackup.Parse("schema=1\r\nproduct=Xtension\r\nbrowsers=brave,edge\r\n");
            Assert.IsTrue(imported.Accepted, "valid backup metadata accepted before inspecting values");
            Assert.AreEqual(BrowserCatalog.Mask(BrowserKind.Brave), imported.Settings.Browsers, "import drops unsupported Edge");
            Assert.AreEqual(2, BrowserCatalog.Offerable().Length, "settings only offers accepted providers");
        }

        [Test]
        public static void EveryDescriptorIsDistinctAndSelfConsistent()
        {
            foreach (BrowserDescriptor descriptor in BrowserCatalog.All)
            {
                Assert.IsTrue(!string.IsNullOrEmpty(descriptor.Token), "every browser needs a persisted token");
                Assert.IsTrue(!string.IsNullOrEmpty(descriptor.ExecutableName), descriptor.Token + " needs an image name");
                Assert.IsTrue(!string.IsNullOrEmpty(descriptor.DisplayName), descriptor.Token + " needs a display name");
                Assert.AreEqual(descriptor.Kind, BrowserCatalog.ParseToken(descriptor.Token), descriptor.Token + " token round trip");
                Assert.IsTrue(BrowserCatalog.Mask(descriptor.Kind) != BrowserCatalog.NoBrowsers, descriptor.Token + " needs a mask bit");
                Assert.AreNotEqual(TabAccessContract.None, descriptor.Contract, descriptor.Token + " must declare its tab contract");
                foreach (BrowserDescriptor other in BrowserCatalog.All)
                {
                    if (other.Kind == descriptor.Kind) continue;
                    Assert.AreNotEqual(other.Token, descriptor.Token, "two browsers cannot share a token");
                    Assert.AreNotEqual(other.ExecutableName, descriptor.ExecutableName, "two browsers cannot share an image name");
                    Assert.IsFalse((BrowserCatalog.Mask(other.Kind) & BrowserCatalog.Mask(descriptor.Kind)) != 0,
                        "two browsers cannot share a mask bit");
                }
            }
        }
    }
}
