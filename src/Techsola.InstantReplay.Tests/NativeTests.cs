using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using NUnit.Framework;
using Shouldly;

namespace Techsola.InstantReplay.Tests
{
    public static class NativeTests
    {
        public static IEnumerable<TestCaseData> NativeAPIMethods =>
            from t in typeof(InstantReplayCamera).Assembly.GetTypes()
            from m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            where m.IsDefined(typeof(DllImportAttribute), inherit: false)
            select new TestCaseData(m).SetArgDisplayNames($"{t.Name}.{m.Name}");

        [TestCaseSource(nameof(NativeAPIMethods))]
        public static void Native_APIs_are_annotated_with_required_OS_version(MethodInfo apiMethod)
        {
            var attribute = CustomAttributeData.GetCustomAttributes(apiMethod)
                .Where(a => a.Constructor?.DeclaringType?.FullName == "System.Runtime.Versioning.SupportedOSPlatformAttribute")
                .ShouldHaveSingleItem();
        }
    }
}
