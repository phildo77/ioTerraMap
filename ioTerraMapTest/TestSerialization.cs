using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;
using ioTerraMap;
using NUnit.Framework;
using ioDelaunay;
using ioUtils;
using System.Threading;

namespace ioTerraMapTest
{
    [TestFixture]
    public class TestSerialization
    {
        private const string FileName = "Save.dat";
        private readonly string FilePath = AppDomain.CurrentDomain.BaseDirectory;

        [Test]
        public void SerializeSettings()
        {
            var fullFileName = FilePath + "\\" + FileName;
            if (File.Exists(fullFileName))
                File.Delete(fullFileName);

            Trace.WriteLine("Creating settings file at " + fullFileName);

            var settings = new TerraMap.Settings();

            // Persist to file
            var stream = File.Create(fullFileName);
            var formatter = new BinaryFormatter();
            Trace.WriteLine("Serializing settings");
            formatter.Serialize(stream, settings);
            stream.Close();

            // Restore from file
            stream = File.OpenRead(fullFileName);
            Trace.WriteLine("Deserializing settings");
            var v = (TerraMap.Settings) formatter.Deserialize(stream);
            stream.Close();


            Assert.True(true);
        }

       [Test]
        public void TestSerializationTerraMesh()
        {
            var fullFileName = FilePath + "\\TerraMesh" + FileName;
            
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


            bool isDone = false;
            TerraMap finishedMap = null;
            TerraMap.Generator.OnComplete onComplete = _tMap =>
            {
                finishedMap = _tMap;
                isDone = true;
            };
            Thread genThread = new Thread(() => TerraMap.Generator.Generate(sets, onComplete, progOnUpdate));
            genThread.Start();

            while (isDone == false)
            {
                System.Threading.Thread.Sleep(100);
            }

            var mapExists = finishedMap != null;

            var tMeshOrig = finishedMap.TMesh;
            var serializedTMesh = TerraMap.TerraMesh.Serialize(tMeshOrig);

            File.WriteAllBytes(fullFileName, serializedTMesh);

            var dataFromFile = File.ReadAllBytes(fullFileName);

            var tMeshFromFile = TerraMap.TerraMesh.Deserialize(dataFromFile);

            Assert.True(true);
        }
        
        [Test]
        public void TestSerializeVectorArray2()
        {
            Vector3[] array = new Vector3[]
            {
                Vector3.up,
                Vector3.right,
                Vector3.down,
                Vector3.left,
                Vector3.back,
                Vector3.forward,
                Vector3.zero
            };
            byte[][] byteArray = new byte[array.Length][];


            
            var fullFileName = FilePath + "\\VectorArray" + FileName;
            if (File.Exists(fullFileName)) 
                File.Delete(fullFileName);
            Trace.WriteLine("Writing Vector array to file: " + fullFileName);
            using (BinaryWriter bw = new BinaryWriter(File.Open(fullFileName,FileMode.Create)))
            {
                for (int vecIdx = 0; vecIdx < array.Length; ++vecIdx)
                {
                    var curVec = array[vecIdx];
                    bw.Write(curVec.x);
                    bw.Write(curVec.y);
                    bw.Write(curVec.z);
                }
            }


            Trace.WriteLine("Reading Vector array from file: " + fullFileName);

            var arrayFromFile = new List<Vector3>();
            using (BinaryReader br = new BinaryReader(File.OpenRead(fullFileName)))
            {
                while (br.BaseStream.Position != br.BaseStream.Length)
                {
                    var x = br.ReadSingle();
                    var y = br.ReadSingle();
                    var z = br.ReadSingle();
                    arrayFromFile.Add(new Vector3(x, y, z));
                }
            }

            Assert.True(true);


        }
    }
}