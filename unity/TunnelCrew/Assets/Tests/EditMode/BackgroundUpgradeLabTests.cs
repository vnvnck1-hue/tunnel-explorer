using System.Collections.Generic;
using NUnit.Framework;
using TunnelCrew.Presentation.Visual;
using UnityEngine;

namespace TunnelCrew.Tests
{
    public class BackgroundUpgradeLabTests
    {
        [Test]
        public void EveryWalkableCellIsReachableFromSpawn()
        {
            var field=ArraySolidField.Parse(BackgroundUpgradeLab.Room());
            var seen=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();
            var start=Vector2Int.FloorToInt(BackgroundUpgradeLab.Spawn);queue.Enqueue(start);seen.Add(start);
            var dirs=new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
            while(queue.Count>0)
            {
                var p=queue.Dequeue();foreach(var d in dirs)
                {var q=p+d;if(!field.IsSolid(q.x,q.y)&&seen.Add(q))queue.Enqueue(q);}
            }
            int count=0;
            for(int y=0;y<field.Rows;y++)for(int x=0;x<field.Cols;x++)if(!field.IsSolid(x,y))count++;
            Assert.That(seen.Count,Is.EqualTo(count));Assert.That(count,Is.GreaterThan(150));
        }
        [TestCase("###/#.#/###")]
        [TestCase("#./.#")]
        [TestCase(".##/##./#..")]
        public void OrganicContoursAreFiniteBoundedAndDeterministic(string room)
        {
            var field=ArraySolidField.Parse(room.Split('/'));
            var a=new OrganicLabGeometry(field);var b=new OrganicLabGeometry(field);
            Assert.That(a.Loops.Count,Is.GreaterThan(0));Assert.That(a.Loops.Count,Is.EqualTo(b.Loops.Count));
            for(int l=0;l<a.Loops.Count;l++)for(int i=0;i<a.Loops[l].Count;i++)
            {
                Assert.That(a.Loops[l][i],Is.EqualTo(b.Loops[l][i]));
                Assert.That(float.IsNaN(a.Loops[l][i].x),Is.False);
                var contour=a.Contours[l];int edge=i/4;float t=(i%4)*.25f;
                var p=contour.Points[edge];var q=contour.Points[(edge+1)%contour.Points.Count];
                var original=Vector2.Lerp(new Vector2(p.X,p.Y),new Vector2(q.X,q.Y),t);
                Assert.That(Vector2.Distance(original,a.Loops[l][i]),Is.LessThanOrEqualTo(OrganicLabGeometry.MaxOffset+.0001f));
                Assert.That(a.Point(original.x,original.y),Is.EqualTo(a.Loops[l][i]));
            }
        }
        [Test]
        public void MiningAndRestoringRebuildsOriginalContour()
        {
            var field=ArraySolidField.Parse(new[]{"#####","#...#","#.#.#","#...#","#####"});
            var before=new OrganicLabGeometry(field);field.SetSolid(2,2,false);
            var mined=new OrganicLabGeometry(field);Assert.That(mined.Loops.Count,Is.LessThan(before.Loops.Count));
            field.SetSolid(2,2,true);var restored=new OrganicLabGeometry(field);
            for(int i=0;i<before.Loops.Count;i++)CollectionAssert.AreEqual(before.Loops[i],restored.Loops[i]);
        }
    }
}
