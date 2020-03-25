using System.Diagnostics;
using System.Threading;
using ioSS.TerraMapLib;
using ioSS.Util;
using NUnit.Framework;

namespace ioTerraMapTest
{
    [TestFixture]
    public class TestTerraMesh
    {
        [Test]
        public void TestGenerate()
        {
            var sets = TerraMap.Settings.Default;
            var generator = TerraMap.TerraMesh.Generator.Stage(sets);

            TerraMap.TerraMesh tMesh;
            
            Progress.OnUpdate onUpdate = (_pct, _str) => Trace.WriteLine(_str + " : " + _pct);
            TerraMap.TerraMesh.Generator.OnComplete onComplete = _tm => tMesh = _tm;
            
            var genThread = new Thread(() => generator.Generate(onUpdate, onComplete));
            genThread.Start();

            var secondCounter = 0;
            while (genThread.IsAlive)
            {
                
                Trace.WriteLine("Waiting for thread: " + secondCounter++);
                System.Threading.Thread.Sleep(1000);
            }
        }
    }
}