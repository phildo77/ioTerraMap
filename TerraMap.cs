using System;

namespace ioTerraMap
{
    [Serializable]
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
            var verts = TMesh.ElevatedVerts();
            for (var idx = 0; idx < verts.Length; ++idx)
                if (verts[idx].z < WaterSurfaceZ)
                    verts[idx].Set(verts[idx].x, verts[idx].y, WaterSurfaceZ);

            return verts;
        }

        [Serializable]
        public class WaterNode
        {
            public float Flux;
            public WaterNode NodeTo;
            public int SiteIdx;
        }
    }
}