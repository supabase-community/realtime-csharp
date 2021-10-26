using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Supabase.Realtime.Converters;

namespace RealtimeTests
{
    [TestClass]
    public class Converters
    {
        [TestMethod("Support Array Conversions (WALRUS + Backwards Compat.)")]
        public void SupportArrayConversions()
        {
            var intConverter = new IntArrayConverter();
            CollectionAssert.AreEqual(new List<int>(), intConverter.Parse("{}"));
            CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, intConverter.Parse("{1,2,3}"));
            CollectionAssert.AreEqual(new List<int> { 1, 2, 3 }, intConverter.Parse("[1,2,3]"));
            CollectionAssert.AreEqual(new List<int> { 99, 999, 9999, 999999 }, intConverter.Parse("[99, 999, 9999, 999999]"));

            var strConverter = new StringArrayConverter();
            CollectionAssert.AreEqual(new List<string> { "a", "b", "c" }, strConverter.Parse("{a,b,c}"));
            CollectionAssert.AreEqual(new List<string> { "a", "b", "c" }, strConverter.Parse("[a,b,c]"));
        }
    }
}
