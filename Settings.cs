using System;
using System.Collections.Generic;

namespace ioTerraMap
{
using ioDelaunay;
    public partial class TerraMap
    {
        public enum TerraType
        {
            CoastEuropean
        }
        
        
        [Serializable]
        public class Settings
        {
            //Randomization
            public int Seed;
            internal Random m_Rnd;

            //Size and Bounds
            public Rect Bounds = new Rect(Vector2.one, Vector2.one * 500);
            public float Resolution = 1; //Points per km
            
            //Erosion / Water / Terrain
            public float RainfallGlobal = 0.000089f; //km annual
            public float MaxErosionRate = 0.010f; //km
            public float MinPDSlope = 0.01f;
            public float LandWaterRatio = 0.7f;
            public float m_WaterwayThresh = 0.2f;

            public float WaterwayThresh
            {
                get
                {
                    return m_WaterwayThresh;
                }
                set
                {
                    if (value < 0) m_WaterwayThresh = 0f;
                    else if (value > 1f) m_WaterwayThresh = 1f;
                }
            }
            
            //Land Morphing
            public float ConifyStrength = 15f;
            public Vector2 GlobalSlopeDir = Vector2.zero;
            public float GlobalSlopeMag = 15f;
            public List<int> HillRndCnt = new List<int> {20, 5};
            public List<float> HillRndStr = new List<float> { 0.050f, 0.8f};
            public List<float> HillRndRad = new List<float> {80, 200};
            

            //Painting / Texture
            public int TextureResolution = 10; //Pixels per km
            
            public static Settings Default = new Settings();
            //NOTES
            //Ave walk speed 5 kph
            //Ave horse speed trot 15 kph gallop 43 kph

            
            private static Settings CoastEuropean = new Settings
            {
                
            };

            public Settings()
            {
                Seed = Guid.NewGuid().GetHashCode();
                m_Rnd = new Random(Seed);
            }

            public Settings(int _seed)
            {
                Seed = _seed;
                m_Rnd = new Random(Seed);

            }
        }

    }
}