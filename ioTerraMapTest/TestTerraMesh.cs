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
            var width = sets.Bounds.width;
            var height = sets.Bounds.height;
            var density = sets.Resolution;
            var seed = sets.Seed;
            var vertices = TerraMap.TerraMesh.Generator.GenerateRandomVertices(width, height, density, seed);
            var generator = TerraMap.TerraMesh.Generator.StageMeshGeneration(vertices);

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