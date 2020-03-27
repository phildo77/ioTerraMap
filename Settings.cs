using System;
using System.Collections.Generic;
using ioSS.Util.Maths.Geometry;

namespace ioSS.TerraMapLib
{
    public partial class TerraMap
    {
        public enum TerraType
        {
            CoastEuropean
        }

        public class Settings
        {
            public static Settings Default = new Settings();
            //NOTES
            //Ave walk speed 5 kph
            //Ave horse speed trot 15 kph gallop 43 kph


            private static Settings CoastEuropean = new Settings();

            
            //Size and Bounds
            public Rect Bounds = new Rect(Vector2.one, Vector2.one * 500);

            
            //Land Morphing
            public float ConifyStrength = 15f;
            public Vector2 GlobalSlopeDir = Vector2.zero;
            public float GlobalSlopeMag = 15f;
            public List<int> HillRndCnt = new List<int> {20, 5};
            public List<float> HillRndRad = new List<float> {80, 200};
            public List<float> HillRndStr = new List<float> {0.050f, 0.8f};
            public float LandWaterRatio = 0.7f;

            internal Random m_Rnd;

            //Randomization
            private int m_Seed;
            public float m_WaterwayThresh = 0.2f;
            public float MaxErosionRate = 0.010f; //km
            public float MinPDSlope = 0.01f;

            //Erosion / Water / Terrain
            public float RainfallGlobal = 0.000089f; //km annual
            public float Resolution = 1; //Points per km


            //Painting / Texture
            public int TextureResolution = 10; //Pixels per km

            public Settings()
            {
                Seed = Guid.NewGuid().GetHashCode();
            }

            public Settings(int _seed)
            {
                Seed = _seed;
            }

            public int Seed
            {
                get => m_Seed;
                set
                {
                    m_Seed = value;
                    m_Rnd = new Random(m_Seed);
                }
            }

            public float WaterwayThresh
            {
                get => m_WaterwayThresh;
                set
                {
                    if (value < 0) m_WaterwayThresh = 0f;
                    else if (value > 1f) m_WaterwayThresh = 1f;
                }
            }

            public static Vector2 RndVec2(Rect _bounds, Random _rnd)
            {
                var xSize = _bounds.width;
                var ySize = _bounds.height;
                var x = (float) (_rnd.NextDouble() * xSize) + _bounds.min.x;
                var y = (float) (_rnd.NextDouble() * ySize) + _bounds.min.y;
                return new Vector2(x, y);
            }
        }
    }
}