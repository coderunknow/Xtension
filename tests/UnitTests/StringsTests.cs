using System;
using System.Collections.Generic;
using System.Drawing;

namespace Xtension.Tests
{
    /// <summary>
    /// The language toggle only works if the text table, the enum and the format arguments stay in step. These
    /// tests are what catch an off-by-one row, a missing translation or a placeholder mismatch before it reaches a
    /// user, because the compiler cannot check any of it.
    /// </summary>
    internal static class StringsTests
    {
        private static IEnumerable<StringId> AllIds()
        {
            Array values = Enum.GetValues(typeof(StringId));
            foreach (StringId id in values) yield return id;
        }

        [Test]
        public static void TableHasExactlyOneRowPerId()
        {
            int enumCount = Enum.GetValues(typeof(StringId)).Length;
            Assert.AreEqual(enumCount, Strings.TableCount, "every StringId needs exactly one row, in the same order");
        }

        [Test]
        public static void EveryIdHasTextInBothLanguages()
        {
            AppLanguage previous = Strings.Language;
            try
            {
                foreach (AppLanguage language in new AppLanguage[] { AppLanguage.Vietnamese, AppLanguage.English })
                {
                    Strings.Language = language;
                    foreach (StringId id in AllIds())
                    {
                        if (id == StringId.None) continue;
                        string text = Strings.Get(id);
                        Assert.IsTrue(!string.IsNullOrEmpty(text), "missing " + language + " text for " + id);
                        Assert.IsTrue(text.Trim().Length == text.Length, "unexpected surrounding whitespace for " + id + " in " + language);
                    }
                }
            }
            finally { Strings.Language = previous; }
        }

        [Test]
        public static void VietnameseAndEnglishDifferEverywhere()
        {
            AppLanguage previous = Strings.Language;
            try
            {
                // A row that is identical in both columns usually means a copy/paste or an alignment mistake.
                Strings.Language = AppLanguage.Vietnamese;
                List<string> vietnamese = new List<string>();
                foreach (StringId id in AllIds()) vietnamese.Add(Strings.Get(id));
                Strings.Language = AppLanguage.English;
                for (int index = 0; index < vietnamese.Count; index++)
                {
                    if ((StringId)index == StringId.None) continue;
                    Assert.IsTrue(vietnamese[index] != Strings.Get((StringId)index),
                        "row " + (StringId)index + " is identical in both languages");
                }
            }
            finally { Strings.Language = previous; }
        }

        [Test]
        public static void PlaceholderTemplatesAcceptOneArgumentWithoutThrowing()
        {
            AppLanguage previous = Strings.Language;
            try
            {
                foreach (AppLanguage language in new AppLanguage[] { AppLanguage.Vietnamese, AppLanguage.English })
                {
                    Strings.Language = language;
                    foreach (StringId id in AllIds())
                    {
                        string template = Strings.Get(id);
                        string formatted;
                        // Rows differ in how many arguments their sentence needs, so pick the matching overload.
                        if (template.Contains("{2}")) formatted = Strings.Get(id, "ARG0", "ARG1", "ARG2");
                        else if (template.Contains("{1}")) formatted = Strings.Get(id, "ARG0", "ARG1");
                        else formatted = Strings.Get(id, "ARG0");

                        Assert.IsFalse(formatted.Contains("{0}") || formatted.Contains("{1}") || formatted.Contains("{2}"),
                            "unfilled placeholder in " + id + " (" + language + ")");
                        if (template.Contains("{0}")) Assert.IsTrue(formatted.Contains("ARG0"), "argument 0 missing in " + id + " (" + language + ")");
                        if (template.Contains("{1}")) Assert.IsTrue(formatted.Contains("ARG1"), "argument 1 missing in " + id + " (" + language + ")");
                        if (template.Contains("{2}")) Assert.IsTrue(formatted.Contains("ARG2"), "argument 2 missing in " + id + " (" + language + ")");
                    }
                }
            }
            finally { Strings.Language = previous; }
        }

        [Test]
        public static void ShippedVietnameseWordingIsPreserved()
        {
            AppLanguage previous = Strings.Language;
            try
            {
                Strings.Language = AppLanguage.Vietnamese;
                // These are the exact strings dev2 users already saw; dev3 must not silently reword them.
                Assert.AreEqual("Quét UI Automation thất bại. Đã ẩn nút ×.", Strings.Get(StringId.ScanFailed), "scan failure wording");
                Assert.AreEqual("Nút × đang tắt.", Strings.Get(StringId.HiddenOverlayDisabled), "disabled wording");
                Strings.Language = AppLanguage.English;
                Assert.IsTrue(Strings.Get(StringId.ScanFailed).StartsWith("The UI Automation scan"), "English translation applied");
            }
            finally { Strings.Language = previous; }
        }

        [Test]
        public static void LanguageTokenRoundTripIsStable()
        {
            Assert.AreEqual("vi", Strings.LanguageToken(AppLanguage.Vietnamese), "Vietnamese token");
            Assert.AreEqual("en", Strings.LanguageToken(AppLanguage.English), "English token");
        }

        [Test]
        public static void DescribeReportsTheReasonForEveryScanOutcome()
        {
            AppLanguage previous = Strings.Language;
            try
            {
                Strings.Language = AppLanguage.Vietnamese;
                Assert.IsTrue(Strings.Describe(ScanResult.Idle(IntPtr.Zero, ScanOutcome.ScanIncomplete, ScanMessageKey.Incomplete)).Contains("chưa đầy đủ"), "incomplete wording");
                Assert.IsTrue(Strings.Describe(ScanResult.Idle(IntPtr.Zero, ScanOutcome.Error, ScanMessageKey.ScanFailed)).Contains("thất bại"), "error wording");

                ScanResult noTabs = ScanResult.Idle(IntPtr.Zero, ScanOutcome.NoTabs, ScanMessageKey.NoTabs);
                noTabs.NodesVisited = 42;
                Assert.IsTrue(Strings.Describe(noTabs).Contains("42"), "node count is reported");

                ScanResult ambiguous = ScanResult.Idle(IntPtr.Zero, ScanOutcome.AmbiguousSelection, ScanMessageKey.AmbiguousSelection);
                ambiguous.AmbiguousCount = 3;
                Assert.IsTrue(Strings.Describe(ambiguous).Contains("3"), "ambiguous count is reported");

                ScanResult ready = ScanResult.Idle(IntPtr.Zero, ScanOutcome.Ready, ScanMessageKey.Ready);
                ready.Tabs.Add(new TabInfo { Bounds = new Rectangle(0, 0, 10, 10) });
                ready.Tabs.Add(new TabInfo { Bounds = new Rectangle(0, 0, 10, 10) });
                Assert.IsTrue(Strings.Describe(ready).Contains("2"), "tab count is reported");

                // The result's Message property is the same text, so callers that display it stay correct.
                Assert.AreEqual(Strings.Describe(ready), ready.Message, "Message derives from the key");
                ready.Message = "explicit fixture text";
                Assert.AreEqual("explicit fixture text", ready.Message, "an explicit message still wins for fixtures");
            }
            finally { Strings.Language = previous; }
        }

        [Test]
        public static void ScanResultReturnsNoTextRatherThanNullForAnUnsetKey()
        {
            ScanResult result = new ScanResult();
            Assert.IsTrue(result.Message != null, "never null");
            Assert.IsTrue(result.Message.Length > 0, "an unset key still explains itself");
        }
    }
}
