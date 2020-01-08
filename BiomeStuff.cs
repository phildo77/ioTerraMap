namespace ioTerraMap
{
    public partial class TerraMap
    {
        
        public class BiomeStuff
        {
            
            public Biome[][] BiomeConfig;
            public Biome BiomeWater;
            public int[] SiteBiomeMoistZone;
            public TerraMap Host;
    
    
            public BiomeStuff(TerraMap _host)
            {
                Host = _host;
                //Set up Biomes
                SetupBiomes();
                
                //TODO Config Biomes on map using units of distance
                var sitePos = Host.TMesh.SitePos;
                SiteBiomeMoistZone = new int[sitePos.Length];
                //SiteBiomeElevZone = new int[sitePos.Length];
                //SiteBiomes = new Biome[sitePos.Length];
                for (int sIdx = 0; sIdx < SiteBiomeMoistZone.Length; ++sIdx)
                {
                    SiteBiomeMoistZone[sIdx] = 5;
                }
                    
            }
    
            public TerraTexture.Color GetBiomeColor(int _mzIdx, float _elevNorm)
            {
                var biomes = BiomeConfig[_mzIdx];
    
                if (_elevNorm < 0)
                    return BiomeWater.ColTerrain;
                if (_elevNorm < 0.2f)
                    return biomes[0].ColTerrain;
                if (_elevNorm < 0.5)
                    return biomes[1].ColTerrain;
                if (_elevNorm < 0.8)
                    return biomes[2].ColTerrain;
                return biomes[3].ColTerrain;
            }
            
            private void SetupBiomes()  //TODO Expand dynamically
            {
                BiomeConfig = new Biome[6][];
                
                //High elev (3)
                var snow = new Biome() { Name = "Snow", ColTerrain = new TerraTexture.Color(248,248,248)};
                var tundra = new Biome() { Name = "Tundra", ColTerrain = new TerraTexture.Color(221,221,187)};
                var bare = new Biome() { Name = "Bare", ColTerrain = new TerraTexture.Color(187,187,187)};
                var scorched = new Biome() { Name = "Scorched", ColTerrain = new TerraTexture.Color(153,153,153)};
                
                //Upper Mid Elev(2)
                var taiga = new Biome() { Name = "Taiga", ColTerrain = new TerraTexture.Color(204, 212, 187)};
                var shrubland = new Biome() { Name = "Shrubland", ColTerrain = new TerraTexture.Color(196, 204, 187)};
                var tempDesert = new Biome() { Name = "Temperate Desert", ColTerrain = new TerraTexture.Color(228, 232, 202)};
                
                //Lower Mid Elev(1)
                var tempRainFor = new Biome() { Name = "Temperate Rain Forest", ColTerrain = new TerraTexture.Color(164, 196, 168)};
                var tempDecidFor = new Biome() { Name = "Temperate Deciduous Forest", ColTerrain = new TerraTexture.Color(180, 201, 169)};
                var grassland = new Biome() { Name = "Grassland", ColTerrain = new TerraTexture.Color(196, 212, 170)};
                //var tempDesert = new Biome() { Name = "Temperate Desert", ColTerrain = new Color(228, 232, 202)};
                
                //Low Elev (0)
                var tropRainFor = new Biome() { Name = "Temperate Rain Forest", ColTerrain = new TerraTexture.Color(156, 187, 169)};
                var tropSeasFor = new Biome() { Name = "Temperate Deciduous Forest", ColTerrain = new TerraTexture.Color(169, 204, 164)};
                //var grassland = new Biome() { Name = "Grassland", ColTerrain = new Color(196, 212, 170)};
                var subtropDes = new Biome() { Name = "Temperate Desert", ColTerrain = new TerraTexture.Color(233, 221, 199)};
    
                //Water Elev
                BiomeWater = new Biome() { Name = "Water", ColTerrain = new TerraTexture.Color(54, 54, 97)};
                
                BiomeConfig[0] = new Biome[4];
                BiomeConfig[1] = new Biome[4];
                BiomeConfig[2] = new Biome[4];
                BiomeConfig[3] = new Biome[4];
                BiomeConfig[4] = new Biome[4];
                BiomeConfig[5] = new Biome[4];
                
                BiomeConfig[5][3] = BiomeConfig[4][3] = BiomeConfig[3][3] = snow;
                BiomeConfig[2][3] = tundra;
                BiomeConfig[1][3] = bare;
                BiomeConfig[0][3] = scorched;
                
                BiomeConfig[5][2] = BiomeConfig[4][2] = taiga;
                BiomeConfig[3][2] = BiomeConfig[2][2] = shrubland;
                BiomeConfig[1][2] = BiomeConfig[0][2] = tempDesert;
                
                BiomeConfig[5][1] = tempRainFor;
                BiomeConfig[4][1] = BiomeConfig[3][1] = tempDecidFor;
                BiomeConfig[2][1] = BiomeConfig[1][1] = grassland;
                BiomeConfig[0][1] = tempDesert;
                
                BiomeConfig[5][0] = BiomeConfig[4][0] = tropRainFor;
                BiomeConfig[3][0] = BiomeConfig[2][0] = tropSeasFor;
                BiomeConfig[1][0] = grassland;
                BiomeConfig[0][0] = subtropDes;
                
    
            }
            
            public class Biome
            {
                public string Name;
                public TerraTexture.Color ColTerrain;
            }
        }
    
    }
}