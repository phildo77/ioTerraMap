using System.Diagnostics;
using System.Threading;
using ioSS.TerraMapLib;
using ioSS.Util;
using ioSS.Util.Maths.Geometry;
using NUnit.Framework;

namespace ioTerraMapTest
{
    [TestFixture]
    public class Tests
    {
        public void Test2()
        {
        }

        [Test]
        public void Test1()
        {
            var resolution = 1f;
            var sets = new TerraMap.Settings();
            sets.Resolution = 1;
            sets.Bounds = new Rect(Vector2.one, 256 * Vector2.one);
            sets.GlobalSlopeDir = Vector2.left;

            Progress.OnUpdate progOnUpdate = (_pct, _str) =>
            {
                Trace.WriteLine(_str);
                Trace.WriteLine(_pct * 100 + "%");
            };


            var isDone = false;
            TerraMap finishedMap = null;
            TerraMap.Generator.OnComplete onComplete = _tMap =>
            {
                finishedMap = _tMap;
                isDone = true;
            };
            var gen = TerraMap.Generator.StageMapCreation(sets);
            var genThread = new Thread(() => gen.Generate(onComplete, progOnUpdate));
            genThread.Start();

            while (isDone == false) Thread.Sleep(100);

            var mapExists = finishedMap != null;


            Assert.True(mapExists);
        }
    }
}