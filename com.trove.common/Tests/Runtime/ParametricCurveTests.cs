using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Mathematics;
using UnityEngine;

namespace Trove
{
    [TestFixture]
    public class ParametricCurveTests
    {
        [Test]
        public void Bypass()
        {
            ParametricCurve curve = ParametricCurve.GetDefault(ParametricCurveType.Bypass);
            curve.Shape = 3f;
            curve.Slope = 1f;
            curve.VerticalShift = 23f;
            curve.HorizontalShift = 13f;

            Assert.IsTrue(curve.Evaluate(-2f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(-1f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(0f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(1f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(2f).IsRoughlyEqual(0f));
        }

        [Test]
        public void Step()
        {
            ParametricCurve curve = ParametricCurve.GetDefault(ParametricCurveType.Step);
            curve.Shape = 0f;
            curve.Slope = 1f;
            curve.VerticalShift = 0f;
            curve.HorizontalShift = 0.5f;

            Assert.IsTrue(curve.Evaluate(-2f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(-1f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(0f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(1f).IsRoughlyEqual(1f));
            Assert.IsTrue(curve.Evaluate(2f).IsRoughlyEqual(1f));
        }

        [Test]
        public void Linear()
        {
            ParametricCurve curve = ParametricCurve.GetDefault(ParametricCurveType.Linear);
            curve.Shape = 0f;
            curve.Slope = 1f;
            curve.VerticalShift = 0f;
            curve.HorizontalShift = 0f;

            Assert.IsTrue(curve.Evaluate(-2f).IsRoughlyEqual(-2f));
            Assert.IsTrue(curve.Evaluate(-1f).IsRoughlyEqual(-1f));
            Assert.IsTrue(curve.Evaluate(0f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(1f).IsRoughlyEqual(1f));
            Assert.IsTrue(curve.Evaluate(2f).IsRoughlyEqual(2f));
        }

        [Test]
        public void Exponential()
        {
            ParametricCurve curve = ParametricCurve.GetDefault(ParametricCurveType.Exponential);
            curve.Shape = 2f;
            curve.Slope = 1f;
            curve.VerticalShift = 0f;
            curve.HorizontalShift = 0f;

            Assert.IsTrue(curve.Evaluate(-2f).IsRoughlyEqual(4f));
            Assert.IsTrue(curve.Evaluate(-1f).IsRoughlyEqual(1f));
            Assert.IsTrue(curve.Evaluate(0f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(1f).IsRoughlyEqual(1f));
            Assert.IsTrue(curve.Evaluate(2f).IsRoughlyEqual(4f));
        }

        [Test]
        public void Sine()
        {
            ParametricCurve curve = ParametricCurve.GetDefault(ParametricCurveType.Sine);
            curve.Shape = 2f;
            curve.Slope = 0.5f;
            curve.VerticalShift = 0.5f;
            curve.HorizontalShift = 0f;

            Assert.IsTrue(curve.Evaluate(-2f).IsRoughlyEqual(1f));
            Assert.IsTrue(curve.Evaluate(-1.5f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(-1f).IsRoughlyEqual(1f));
            Assert.IsTrue(curve.Evaluate(-0.5f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(0f).IsRoughlyEqual(1f));
            Assert.IsTrue(curve.Evaluate(0.5f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(1f).IsRoughlyEqual(1f));
            Assert.IsTrue(curve.Evaluate(1.5f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(2f).IsRoughlyEqual(1f));
        }

        [Test]
        public void Logistic()
        {
            ParametricCurve curve = ParametricCurve.GetDefault(ParametricCurveType.Logistic);
            curve.Shape = 1f;
            curve.Slope = 1f;
            curve.VerticalShift = 0f;
            curve.HorizontalShift = 0f;

            Assert.IsTrue(curve.Evaluate(-2f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(-1f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(0f).IsRoughlyEqual(0f, 0.02f));
            Assert.IsTrue(curve.Evaluate(0.5f).IsRoughlyEqual(0.5f, 0.02f));
            Assert.IsTrue(curve.Evaluate(1f).IsRoughlyEqual(1f, 0.02f));
            Assert.IsTrue(curve.Evaluate(2f).IsRoughlyEqual(1f));
        }

        [Test]
        public void Logit()
        {
            ParametricCurve curve = ParametricCurve.GetDefault(ParametricCurveType.Logit);
            curve.Shape = 0f;
            curve.Slope = 0.4f;
            curve.VerticalShift = 0f;
            curve.HorizontalShift = 0f;

            Assert.IsTrue(curve.Evaluate(-2f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(-1f).IsRoughlyEqual(0f));
            Assert.IsTrue(curve.Evaluate(0f) < -1000f);
            Assert.IsTrue(curve.Evaluate(0.5f).IsRoughlyEqual(0.5f, 0.02f));
            Assert.IsTrue(curve.Evaluate(1f) > 1000f);
            Assert.IsTrue(curve.Evaluate(2f).IsRoughlyEqual(0f));
        }
    }
}