using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using TunnelCrew.Presentation.Visual;
using UnityEngine;

namespace TunnelCrew.Tests
{
    public class OrganicWallSamplingTests
    {
        [TestCase("###/#.#/###")]
        [TestCase("#./.#")]
        [TestCase(".##/##./#..")]
        [TestCase("#####/#.#.#/#.#.#/#...#/#####")]
        public void SamplesAreBoundedDeterministicAndLeaveTheFieldUnchanged(string room)
        {
            var field=ArraySolidField.Parse(room.Split('/'));
            for(int y=0;y<=field.Rows*4;y++) for(int x=0;x<=field.Cols*4;x++)
            {
                var p=new Vector2(x*.25f,y*.25f);
                var q=OrganicWallSampling.Point(field,p.x,p.y);
                Assert.That(float.IsNaN(q.x)||float.IsNaN(q.y),Is.False);
                Assert.That(Vector2.Distance(p,q),Is.LessThanOrEqualTo(OrganicWallSampling.MaxOffset+.0001f));
                Assert.That(OrganicWallSampling.Point(field,p.x,p.y),Is.EqualTo(q));
                if(x%4!=0&&y%4!=0)Assert.That(q,Is.EqualTo(p));
            }
            var copy=ArraySolidField.Parse(room.Split('/'));
            for(int y=0;y<field.Rows;y++)for(int x=0;x<field.Cols;x++)
                Assert.That(field.IsSolid(x,y),Is.EqualTo(copy.IsSolid(x,y)));
        }
        [Test]
        public void RebuildingNeighbourChunksInEitherOrderProducesIdenticalSeams()
        {
            var f=new ArraySolidField(34,34);
            for(int y=0;y<34;y++)for(int x=0;x<34;x++)f.SetSolid(x,y,(x+y)%3!=0);
            var seam=new List<Vector2>();
            for(int q=0;q<=136;q++)seam.Add(OrganicWallSampling.Point(f,16,q*.25f));
            for(int q=136;q>=0;q--)Assert.That(OrganicWallSampling.Point(f,16,q*.25f),Is.EqualTo(seam[q]));
            f.SetSolid(16,16,!f.IsSolid(16,16));
            for(int q=0;q<56;q++)Assert.That(OrganicWallSampling.Point(f,16,q*.25f),Is.EqualTo(seam[q]));
        }
        [Test]
        public void BossWallBoundaryStaysAlignedWithAuthoredArt()
        {
            var f=new ArraySolidField(5,5); f.SetSolid(2,2,true);f.SetBossWall(2,2,true);
            for(int i=0;i<=4;i++)
            {
                float x=2+i*.25f;
                Assert.That(OrganicWallSampling.Point(f,x,2),Is.EqualTo(new Vector2(x,2)));
                Assert.That(OrganicWallSampling.Point(f,x,3),Is.EqualTo(new Vector2(x,3)));
            }
        }
        [Test]
        public void MiningAtFourChunkCornerInvalidatesAllFourAndRestoreIsExact()
        {
            var f=new ArraySolidField(34,34); f.SetSolid(16,16,true);
            var before=OrganicWallSampling.Point(f,16,16);
            var dirty=new HashSet<int>();SurfaceTopologyBuilder.DirtyChunks(16,16,34,34,dirty);
            CollectionAssert.AreEquivalent(new[]{0,1,3,4},dirty);
            f.SetSolid(16,16,false); Assert.That(OrganicWallSampling.Point(f,16,16),Is.Not.EqualTo(before));
            f.SetSolid(16,16,true); Assert.That(OrganicWallSampling.Point(f,16,16),Is.EqualTo(before));
        }
        [Test]
        public void RuntimeStyleIncludesSupportedShaderAndDressing()
        {
            var style=Resources.Load<OrganicEnvironmentStyle>("Visual/OrganicEnvironmentStyle");
            Assert.That(style,Is.Not.Null);Assert.That(style.rockShader,Is.Not.Null);
            Assert.That(style.rockShader.isSupported,Is.True);Assert.That(style.rock,Is.Not.Null);
            Assert.That(style.floorMacro,Is.Not.Null);
            Assert.That(style.wallTopMacro,Is.Not.Null);
            Assert.That(style.wallFrontMacro,Is.Not.Null);
            Assert.That(style.wallRimMacro,Is.Not.Null);
            Assert.That(new[]
            {
                style.floorNormal, style.floorAo, style.floorEmission,
                style.wallTopNormal, style.wallTopAo, style.wallTopEmission,
                style.wallFrontNormal, style.wallFrontAo, style.wallFrontEmission,
                style.wallRimNormal, style.wallRimAo, style.wallRimEmission,
            }, Has.All.Not.Null);
            Assert.That(style.floorAoStrength, Is.InRange(0.01f, 1f));
            Assert.That(style.wallAoStrength, Is.InRange(0.01f, 1f));
            Assert.That(style.wallSupports,Is.Not.Null);
            Assert.That(style.wallSupports.Length,Is.EqualTo(3));
            Assert.That(style.wallSupports,Has.All.Not.Null);
            Assert.That(style.wallConduits,Is.Not.Null);
            Assert.That(style.wallConduits.Length,Is.EqualTo(9));
            Assert.That(style.wallConduits,Has.All.Not.Null);
            Assert.That(style.wallJunctions,Is.Not.Null);
            Assert.That(style.wallJunctions.Length,Is.EqualTo(9));
            Assert.That(style.wallJunctions,Has.All.Not.Null);
            Assert.That(style.servicePylons,Is.Not.Null);
            Assert.That(style.servicePylons.Length,Is.EqualTo(3));
            Assert.That(style.servicePylons,Has.All.Not.Null);
            Assert.That(style.floorMacroSizeCells,Is.GreaterThan(style.wallFrontMacroSizeCells));
            Assert.That(style.floorDerivedNormalStrength,Is.GreaterThan(0f));
            Assert.That(style.wallFrontDerivedNormalStrength,Is.GreaterThan(style.floorDerivedNormalStrength));
            var material=new Material(style.rockShader);
            try
            {
                Assert.That(material.HasProperty("_LabScale"),Is.True);
                Assert.That(material.HasProperty("_LabMirrorRepeat"),Is.True);
                Assert.That(material.HasProperty("_LabAccentEmission"),Is.True);
                Assert.That(material.HasProperty("_LabDerivedNormal"),Is.True);
            }
            finally { Object.DestroyImmediate(material); }
        }

        [Test]
        public void IntegratedSupportsOnlyOccupyLongFacadeInteriorSlots()
        {
            for (int i = 0; i < 5; i++)
                Assert.That(OrganicEnvironmentRenderer.IsWallSupportSlot(i, 4), Is.False);
            Assert.That(OrganicEnvironmentRenderer.IsWallSupportSlot(0, 5), Is.False);
            Assert.That(OrganicEnvironmentRenderer.IsWallSupportSlot(1, 5), Is.True);
            Assert.That(OrganicEnvironmentRenderer.IsWallSupportSlot(2, 5), Is.False);
            Assert.That(OrganicEnvironmentRenderer.IsWallSupportSlot(3, 5), Is.True);
            Assert.That(OrganicEnvironmentRenderer.IsWallSupportSlot(4, 5), Is.False);
            CollectionAssert.AreEqual(new[] { 1, 6, 11 },
                System.Linq.Enumerable.Range(0, 13)
                    .Where(i => OrganicEnvironmentRenderer.IsWallSupportSlot(i, 13)).ToArray());
        }

        [Test]
        public void IntegratedConduitsFormOneLeftMiddleRightSpanBetweenSupports()
        {
            for (int i = 0; i < 4; i++)
                Assert.That(OrganicEnvironmentRenderer.ConduitPieceFor(i, 4), Is.EqualTo(-1));

            CollectionAssert.AreEqual(new[] { -1, 0, 1, 2, -1 },
                System.Linq.Enumerable.Range(0, 5)
                    .Select(i => OrganicEnvironmentRenderer.ConduitPieceFor(i, 5)).ToArray());
            CollectionAssert.AreEqual(new[] { -1, 0, 1, 1, 1, 1, 1, 2, -1 },
                System.Linq.Enumerable.Range(0, 9)
                    .Select(i => OrganicEnvironmentRenderer.ConduitPieceFor(i, 9)).ToArray());
        }

        [Test]
        public void IntegratedConduitRowIsStableForTheSameFacade()
        {
            int row = OrganicEnvironmentRenderer.ConduitRowFor(7, 11);
            Assert.That(row, Is.InRange(0, 2));
            Assert.That(OrganicEnvironmentRenderer.ConduitRowFor(7, 11), Is.EqualTo(row));
        }

        [Test]
        public void WallJunctionsOnlyTurnAtServiceFacadeEndsWithRealSideWalls()
        {
            Assert.That(OrganicEnvironmentRenderer.JunctionPieceFor(0, 4, 4, 0), Is.EqualTo(-1));
            Assert.That(OrganicEnvironmentRenderer.JunctionPieceFor(0, 5, 1, 0), Is.EqualTo(-1));
            Assert.That(OrganicEnvironmentRenderer.JunctionPieceFor(0, 5, 2, 0), Is.EqualTo(0));
            Assert.That(OrganicEnvironmentRenderer.JunctionPieceFor(4, 5, 0, 2), Is.EqualTo(2));
            Assert.That(OrganicEnvironmentRenderer.JunctionPieceFor(2, 5, 4, 4), Is.EqualTo(-1));
        }

        [TestCase(0, 0)]
        [TestCase(1, 0)]
        [TestCase(2, 2)]
        [TestCase(4, 4)]
        [TestCase(9, 4)]
        public void SideWallInfrastructureUsesAControlledVerticalSpan(int sideCells, int expected)
        {
            Assert.That(OrganicEnvironmentRenderer.VerticalJunctionSegmentCount(sideCells), Is.EqualTo(expected));
        }
    }
}
