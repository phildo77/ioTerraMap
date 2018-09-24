namespace ioTerraMapGen
{
using ioDelaunay;
    public partial class TerraMap
    {
        public enum TerraType
        {
            CoastEuropean
        }
        
        
        public class Settings
        {
            //Randomization
            public int? Seed = null;

            //Size and Bounds
            public Rect Bounds = new Rect(Vector2.one, Vector2.one * 500);
            public float Resolution = 1; //Points per km
            
            //Erosion / Water / Terrain
            public float RainfallGlobal = 89; //cm annual
            public float MinPDSlope = 0.01f;
            public float LandWaterRatio = 0.7f;
            public Vector2 GlobalSlopeDir = Vector2.zero;
            public float GlobalSlopeMag = 15f;

            //Terrain massaging
            
            
            public static Settings Default = new Settings();
            //NOTES
            //Ave walk speed 5 kph
            //Ave horse speed trot 15 kph gallop 43 kph

            
            private static Settings CoastEuropean = new Settings
            {
                RainfallGlobal = 89
            };
        }

    }
}