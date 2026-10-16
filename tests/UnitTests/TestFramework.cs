using System;
using System.Collections.Generic;
using System.Reflection;

namespace Xtension.Tests
{
    /// <summary>Marks a static void method with no parameters as a test. Discovered by reflection in TestRunner.</summary>
    [AttributeUsage(AttributeTargets.Method)]
    internal sealed class TestAttribute : Attribute
    {
    }

    /// <summary>Thrown by Assert helpers. Anything else thrown by a test is also reported as a failure.</summary>
    internal sealed class AssertionFailure : Exception
    {
        public AssertionFailure(string message) : base(message)
        {
        }
    }

    internal static class Assert
    {
        public static void IsTrue(bool condition, string message)
        {
            if (!condition) throw new AssertionFailure(message);
        }

        public static void IsFalse(bool condition, string message)
        {
            if (condition) throw new AssertionFailure(message);
        }

        public static void AreEqual<T>(T expected, T actual, string message)
        {
            if (!EqualityComparer<T>.Default.Equals(expected, actual))
            {
                throw new AssertionFailure(message + " (expected '" + expected + "', actual '" + actual + "')");
            }
        }

        /// <summary>
        /// Everything must still differ. Used where a duplicated identifier would be a silent aliasing bug: two
        /// browsers sharing a mask bit, or two contracts reported under the same name, would each case a real
        /// misidentification, and equality assertions alone cannot state that.
        /// </summary>
        public static void AreNotEqual<T>(T unexpected, T actual, string message)
        {
            if (EqualityComparer<T>.Default.Equals(unexpected, actual))
            {
                throw new AssertionFailure(message + " (both values were '" + actual + "')");
            }
        }
    }

    internal static class TestRunner
    {
        public static int Run()
        {
            int passed = 0;
            int failed = 0;
            Type[] types = Assembly.GetExecutingAssembly().GetTypes();
            Array.Sort(types, delegate(Type a, Type b) { return string.CompareOrdinal(a.FullName, b.FullName); });

            foreach (Type type in types)
            {
                MethodInfo[] methods = type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                Array.Sort(methods, delegate(MethodInfo a, MethodInfo b) { return string.CompareOrdinal(a.Name, b.Name); });
                foreach (MethodInfo method in methods)
                {
                    if (method.GetCustomAttributes(typeof(TestAttribute), false).Length == 0) continue;
                    string name = type.Name + "." + method.Name;
                    try
                    {
                        method.Invoke(null, null);
                        passed++;
                        Console.WriteLine("PASS " + name);
                    }
                    catch (TargetInvocationException ex)
                    {
                        failed++;
                        Console.WriteLine("FAIL " + name + ": " + Unwrap(ex.InnerException));
                    }
                }
            }

            Console.WriteLine();
            Console.WriteLine(passed + " passed, " + failed + " failed.");
            return failed == 0 ? 0 : 1;
        }

        private static string Unwrap(Exception ex)
        {
            if (ex == null) return "unknown error";
            return ex is AssertionFailure ? ex.Message : ex.GetType().Name + ": " + ex.Message;
        }
    }
}
