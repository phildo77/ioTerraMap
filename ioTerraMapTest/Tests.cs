using System;
using System.Diagnostics;
using ioDelaunay;
using ioTerraMap;
using NUnit.Framework;
using ioUtils;

namespace ioTerraMapTest
{
    [TestFixture]
    public class Tests
    {
        [Test]
        public void Test1()
        {
            var resolution = 1f;
            var sets = new TerraMap.Settings();
            sets.Resolution = 1;
            sets.Bounds = new Rect(Vector2.one, 256 * Vector2.one);

            Progress.OnUpdate progOnUpdate = (_pct, _str) =>
            {
                Trace.WriteLine(_str);
                Trace.WriteLine(_pct * 100 + "%");
            };


            bool isDone = false;
            TerraMap finishedMap = null;
            TerraMap.Generator.OnComplete onComplete = _tMap =>
            {
                finishedMap = _tMap;
                isDone = true;
            };
            TerraMap.Generator.Generate(sets, onComplete, progOnUpdate);

            while (isDone == false)
            {
                System.Threading.Thread.Sleep(100);
            }

            var mapExists = finishedMap != null;
            
            
            Assert.True(mapExists);
        }

        public void Test2()
        {
            
        }
    }
}