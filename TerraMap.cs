using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Random = System.Random;
using Rect = ioDelaunay.Rect;
using ioUtils;

namespace ioTerraMap
{
    using ioDelaunay;

    public partial class TerraMap
    {
        
        public readonly Settings settings;
        public TerraMesh TMesh;
        public BiomeStuff TBiome;
        public TerraTexture TTex;
        
        public WaterNode[] WaterFlux;
        public float WaterFluxMax;
        public float WaterFluxMin => settings.RainfallGlobal;
        public float WaterFluxSpan => WaterFluxMax - WaterFluxMin;
        
        public int[] RiverSites;

        private float m_WaterLevelPct;

        public float WaterSurfaceZ => m_WaterLevelPct; //Pct from Min z to Max z - TODO change to actual Z height?

        private TerraMap()
        {
            settings = Settings.Default;
        }

        private TerraMap(Settings _settings)
        {
            settings = _settings;
        }
        
        public Vector3[] GetMeshVertsWaterTop()
        {
            var verts = TMesh.ElevatedVerts();
            for (int idx = 0; idx < verts.Length; ++idx)
            {
                if (verts[idx].z < WaterSurfaceZ)
                    verts[idx].Set(verts[idx].x, verts[idx].y, WaterSurfaceZ);
            }

            return verts;
        }

        
        
        
        public class WaterNode
        {
            public int SiteIdx;
            public WaterNode NodeTo;
            public float Flux;

        }
        
    }

}