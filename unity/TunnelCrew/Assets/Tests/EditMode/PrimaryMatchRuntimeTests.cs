using System.Collections.Generic;
using NUnit.Framework;
using TunnelCrew.Presentation.Visual;
using UnityEngine;

namespace TunnelCrew.Tests
{
    public class PrimaryMatchRuntimeTests
    {
        [Test]
        public void 본선_카탈로그는_기존과_PrimaryMatch_후보를_모두_포함한다()
        {
            var catalog = Resources.Load<SetPieceCatalog>("Visual/SetPieceCatalog_PrimaryMatchRuntime");

            Assert.IsNotNull(catalog, "Resources 본선 카탈로그");
            Assert.AreEqual(65, catalog.Count, "기존 27 + Primary Match 38");
            Assert.IsNotNull(catalog.Find("TR01-PM-HERO-ORE-CRUSHER"));
            Assert.IsNotNull(catalog.Find("TR01-PM-HERO-VENTILATION-TURBINE"));
            Assert.IsNotNull(catalog.Find("TR01-PM-HERO-POWER-RELAY"));
            Assert.IsNotNull(catalog.Find("TR01-PM-FOREGROUND-RIGHT-FOREGROUND-SHELF"));
        }

        [Test]
        public void 확대_세트피스는_그림자_윤곽도_같이_확대한다()
        {
            var go = new GameObject("scaled-setpiece-contour-test");
            try
            {
                var instance = go.AddComponent<SetPieceInstance>();
                var def = new SetPieceDef
                {
                    assetId = "TEST-SCALED",
                    footprintCells = new Vector2Int(2, 1),
                    shadowContourCells = new[] { -1f, 0f, 1f, 0f, 1f, 1f, -1f, 1f },
                };
                instance.Bind(def, new Vector2(10f, 20f), 1.5f);

                var contours = new List<Vector2[]>();
                instance.AppendContours(contours);

                Assert.AreEqual(1, contours.Count);
                Assert.AreEqual(new Vector2(8.5f, 20f), contours[0][0]);
                Assert.AreEqual(new Vector2(11.5f, 21.5f), contours[0][2]);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }
    }
}
