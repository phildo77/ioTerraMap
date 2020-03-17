using System;
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
        public void SerializeTerraMap()
        {
            var fullFileName = FilePath + "\\TerraMap" + FileName;
            if (File.Exists(fullFileName))
                File.Delete(fullFileName);

            Trace.WriteLine("Creating settings file at " + fullFileName);


            

            
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
            
            // Persist to file
            var stream = File.Create(fullFileName);
            var formatter = new BinaryFormatter();
            Trace.WriteLine("Serializing TerraMap");
            formatter.Serialize(stream, finishedMap);
            stream.Close();
            
            
            
            // Restore from file
            stream = File.OpenRead(fullFileName);
            Trace.WriteLine("Deserializing TerraMap");
            var v = (TerraMap) formatter.Deserialize(stream);
            stream.Close();


            Assert.True(mapExists);
        }
    }
}