using System;
using ioSS.Util.Maths.Geometry;
namespace ioSS.TerraMapLib
{
    
    public partial class TerraMap
    {
        public readonly Settings settings;

        public int[] RiverSites;
        public BiomeStuff TBiome;
        public TerraMesh TMesh;
        public TerraTexture TTex;

        public WaterNode[] WaterFlux;
        public float WaterFluxMax;

        private TerraMap()
        {
            settings = Settings.Default;
        }

        private TerraMap(Settings _settings)
        {
            settings = _settings;
        }

        public float WaterFluxMin => settings.RainfallGlobal;
        public float WaterFluxSpan => WaterFluxMax - WaterFluxMin;

        public float WaterSurfaceZ { get; private set; }

        public Vector3[] GetMeshVertsWaterTop()
        {
            var verts = new Vector3[TMesh.Vertices.Length];
            for (var idx = 0; idx < verts.Length; ++idx)
                if (verts[idx].z < WaterSurfaceZ)
                    verts[idx].Set(TMesh.Vertices[idx].x, TMesh.Vertices[idx].y, WaterSurfaceZ);
                else
                    verts[idx] = TMesh.Vertices[idx];

            return verts;
        }

        
        public class WaterNode
        {
            public float Flux;
            public WaterNode NodeTo;
            public int SiteIdx;
        }
    }
}