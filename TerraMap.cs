using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Random = System.Random;
using Rect = ioDelaunay.Rect;

namespace ioTerraMapGen
{
    using ioDelaunay;

    public partial class TerraMap
    {
        
        public Settings settings;
        public TerraMesh TMesh;
        public BiomeStuff TBiome;
        public TerraTexture TTex;
        private Random m_Rnd;
        public WaterNode[] Waterways;

        public TerraMap Generate()
        {
            return Generate(Settings.Default);
        }
        
        public TerraMap Generate(Settings _settings)
        {
            settings = _settings;
            var seed = settings.Seed ?? (int)DateTime.Now.Ticks;
            m_Rnd = new Random(seed);
            TMesh = new TerraMesh(this);
            
            //Land morphing
            TMesh.Conify(settings.ConifyStrength);
            var gSlpDir = settings.GlobalSlopeDir == Vector2.zero
                ? new Vector2((float) (m_Rnd.NextDouble() - 0.5f), (float) (m_Rnd.NextDouble() - 0.5f))
                : settings.GlobalSlopeDir;
            TMesh.SlopeGlobal(gSlpDir, settings.GlobalSlopeMag);
            for (int hIdx = 0; hIdx < settings.HillRndCnt.Count; ++hIdx)
            {
                for (int hCnt = 0; hCnt < settings.HillRndCnt[hIdx]; ++hCnt)
                    TMesh.Blob(settings.HillRndStr[hIdx], settings.HillRndRad[hIdx]);
            }

            //Erosion
            TMesh.Erode();
            
            
            //TODO
            TBiome = new BiomeStuff(this);
            
			//Calculate Water Level
            WaterSurfaceZ = TMesh.CalcWaterLevel();
			
            //Paint
            TTex = new TerraTexture(this);
            
            
            
            return this; //TODO
        }
        
        
        public class WaterNode
        {
            public int SiteIdx;
            public WaterNode NodeTo;
            public float Flux;

        }
        
    }

}