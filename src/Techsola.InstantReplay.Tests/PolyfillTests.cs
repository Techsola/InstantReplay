using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Shouldly;

namespace Techsola.InstantReplay.Tests
{
    public static class PolyfillTests
    {
        public static IEnumerable<Type> PolyfillTypes =>
            from t in typeof(InstantReplayCamera).Assembly.GetTypes()
            where t.Namespace?.StartsWith("System", StringComparison.Ordinal) ?? false
            select t;

        [TestCaseSource(nameof(PolyfillTypes))]
        public static void Polyfill_types_are_not_exposed(Type polyfillType)
        {
            (polyfillType.Attributes & TypeAttributes.VisibilityMask).ShouldBe(TypeAttributes.NotPublic);
        }
    }
}
