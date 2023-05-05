using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Trove
{
    [TestFixture]
    public class BitUtilitiesTests : MonoBehaviour
    {
        [Test]
        public void Test()
        {
            byte testByte = 0;
            Assert.IsFalse(BitUtilities.GetBit(testByte, 0));
            Assert.IsFalse(BitUtilities.GetBit(testByte, 4));
            Assert.IsFalse(BitUtilities.GetBit(testByte, 7));
            Assert.IsFalse(BitUtilities.GetBit(testByte, 11));

            BitUtilities.SetBit(true, ref testByte, 2);
            Assert.AreEqual(4, testByte);
            Assert.IsFalse(BitUtilities.GetBit(testByte, 0));
            Assert.IsFalse(BitUtilities.GetBit(testByte, 1));
            Assert.IsTrue(BitUtilities.GetBit(testByte, 2));

            int testInt = 0;
            Assert.IsFalse(BitUtilities.GetBit(testInt, 0));
            Assert.IsFalse(BitUtilities.GetBit(testInt, 4));
            Assert.IsFalse(BitUtilities.GetBit(testInt, 7));
            Assert.IsFalse(BitUtilities.GetBit(testInt, 11));

            BitUtilities.SetBit(true, ref testInt, 16);
            Assert.AreEqual(65536, testInt);
            Assert.IsFalse(BitUtilities.GetBit(testInt, 0));
            Assert.IsFalse(BitUtilities.GetBit(testInt, 1));
            Assert.IsTrue(BitUtilities.GetBit(testInt, 16));

            BitUtilities.SetBit(true, ref testInt, 31);
            Assert.AreEqual(-2147418112, testInt);
            Assert.IsTrue(BitUtilities.GetBit(testInt, 31));

            uint testUint = 0;
            Assert.IsFalse(BitUtilities.GetBit(testUint, 0));
            Assert.IsFalse(BitUtilities.GetBit(testUint, 4));
            Assert.IsFalse(BitUtilities.GetBit(testUint, 7));
            Assert.IsFalse(BitUtilities.GetBit(testUint, 11));

            BitUtilities.SetBit(true, ref testUint, 16);
            Assert.AreEqual(65536, testUint);
            Assert.IsFalse(BitUtilities.GetBit(testUint, 0));
            Assert.IsFalse(BitUtilities.GetBit(testUint, 1));
            Assert.IsTrue(BitUtilities.GetBit(testUint, 16));
        }
    }
}