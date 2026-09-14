using System.Collections.Generic;
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
        }
    }
}
