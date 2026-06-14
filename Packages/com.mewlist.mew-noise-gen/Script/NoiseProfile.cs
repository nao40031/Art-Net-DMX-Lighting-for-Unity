#if FN_USE_DOUBLES
    using FN_DECIMAL = System.Double;
#else
    using FN_DECIMAL = System.Single;
#endif
    using System;
    using System.Linq;
    using UnityEngine;

namespace Mewlist.MewNoiseGen
{
    [CreateAssetMenu(fileName = "NoiseProfile", menuName = "Mewlist/MewNoiseGen/NoiseProfile", order = 0)]
    public class NoiseProfile : ScriptableObject
    {
        public enum ColorFormat
        {
            R8,
            A8,
            RGBA32
        }
        
        public enum Noise { Value, ValueFractal, ValueWarp, Perlin, PerlinFractal, PerlinWarp, Simplex, SimplexFractal, SimplexWarp, Cellular, CellularWarp, WhiteNoise, WhiteNoiseWarp, Cubic, CubicFractal, CubicWarp };

        [SerializeField] private int seed = 1337;
        [Range(0.001f, 0.1f)]
        [SerializeField] private FN_DECIMAL frequency = (FN_DECIMAL)0.01;
        [SerializeField] private FastNoise.Interp interp = FastNoise.Interp.Quintic;
        [SerializeField] private Noise noiseType = Noise.Simplex;
        [Range(0, 16)]
        [SerializeField] private int octaves = 3;
        [Range(0f, 10f)]
        [SerializeField] private FN_DECIMAL lacunarity = (FN_DECIMAL)2.0;
        [SerializeField] private FN_DECIMAL gain = (FN_DECIMAL)0.5;
        [SerializeField] private FastNoise.FractalType fractalType = FastNoise.FractalType.FBM;
        [SerializeField] private FastNoise.CellularDistanceFunction cellularDistanceFunction = FastNoise.CellularDistanceFunction.Euclidean;
        [SerializeField] private FastNoise.CellularReturnType cellularReturnType = FastNoise.CellularReturnType.CellValue;
        [SerializeField] private NoiseProfile cellularNoiseLookup = null;
        [Range(0, 3)]
        [SerializeField] private int cellularDistanceIndex0 = 0;
        [Range(0, 3)]
        [SerializeField] private int cellularDistanceIndex1 = 1;
        [Range(-1f, 1f)]
        [SerializeField] private float cellularJitter = 0.45f;
        [SerializeField] private FN_DECIMAL gradientPerturbAmp = (FN_DECIMAL)1.0;
        
        [Range(0, 3)]
        [SerializeField] private int previewQuality = 2;
        [Range(0, 3)]
        [SerializeField] private int resolution = 2;
        [SerializeField] private bool autoRedraw = true;
        [Range(0f, 5f)]
        [SerializeField] private float baseValue = 0f;
        [SerializeField] private ColorFormat colorFormat = ColorFormat.RGBA32;
        
        public bool Warp
        {
            get
            {
                return noiseType == Noise.ValueWarp
                   || noiseType == Noise.PerlinWarp
                   || noiseType == Noise.CubicWarp
                   || noiseType == Noise.CellularWarp
                   || noiseType == Noise.SimplexWarp
                   || noiseType == Noise.ValueWarp
                   || noiseType == Noise.WhiteNoiseWarp;
            }
        }

        public int PreviewQuality
        {
            get { return previewQuality; }
        }

        public float BaseValue
        {
            get { return baseValue; }
        }

        [SerializeField] private Shape[] shapes;

        public int Seed
        {
            get { return seed; }
            set { seed = value; }
        }

        public float Frequency
        {
            get { return frequency; }
            set { frequency = value; }
        }

        public FastNoise.Interp Interp
        {
            get { return interp; }
            set { interp = value; }
        }

        public Noise NoiseType
        {
            get
            {
                return noiseType;
            }
        }

        public ColorFormat TargetColorFormat
        {
            get
            {
                return colorFormat;
            }
        }

        public FastNoise.NoiseType FastNoiseType
        {
            get {
                switch (noiseType)
                {
                    case Noise.Value:
                    case Noise.ValueWarp:
                        return FastNoise.NoiseType.Value;
                    case Noise.ValueFractal:
                        return FastNoise.NoiseType.ValueFractal;
                    case Noise.Perlin:
                    case Noise.PerlinWarp:
                        return FastNoise.NoiseType.Perlin;
                    case Noise.PerlinFractal:
                        return FastNoise.NoiseType.PerlinFractal;
                    case Noise.Simplex:
                    case Noise.SimplexWarp:
                        return FastNoise.NoiseType.Simplex;
                    case Noise.SimplexFractal:
                        return FastNoise.NoiseType.SimplexFractal;
                    case Noise.Cellular:
                    case Noise.CellularWarp:
                        return FastNoise.NoiseType.Cellular;
                    case Noise.WhiteNoise:
                    case Noise.WhiteNoiseWarp:
                        return FastNoise.NoiseType.WhiteNoise;
                    case Noise.Cubic:
                    case Noise.CubicWarp:
                        return FastNoise.NoiseType.Cubic;
                    case Noise.CubicFractal:
                        return FastNoise.NoiseType.CubicFractal;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public int Octaves
        {
            get { return octaves; }
            set { octaves = value; }
        }

        public float Lacunarity
        {
            get { return lacunarity; }
            set { lacunarity = value; }
        }

        public float Gain
        {
            get { return gain; }
            set { gain = value; }
        }

        public FastNoise.FractalType FractalType
        {
            get { return fractalType; }
            set { fractalType = value; }
        }

        public FastNoise.CellularDistanceFunction CellularDistanceFunction
        {
            get { return cellularDistanceFunction; }
            set { cellularDistanceFunction = value; }
        }

        public FastNoise.CellularReturnType CellularReturnType
        {
            get { return cellularReturnType; }
            set { cellularReturnType = value; }
        }

        public NoiseProfile CellularNoiseLookup
        {
            get { return cellularNoiseLookup; }
            set { cellularNoiseLookup = value; }
        }

        public int CellularDistanceIndex0
        {
            get { return cellularDistanceIndex0; }
            set { cellularDistanceIndex0 = value; }
        }

        public int CellularDistanceIndex1
        {
            get { return cellularDistanceIndex1; }
            set { cellularDistanceIndex1 = value; }
        }

        public float CellularJitter
        {
            get { return cellularJitter; }
            set { cellularJitter = value; }
        }

        public float GradientPerturbAmp
        {
            get { return gradientPerturbAmp; }
            set { gradientPerturbAmp = value; }
        }

        public Shape[] Shapes
        {
            get { return shapes ?? (shapes = new Shape [] {}); }
            set { shapes = value; }
        }

        private FastNoise fastNoise;
        public FastNoise FastNoise
        {
            get { return fastNoise ?? (fastNoise = new FastNoise()); }
        }

        public int Resolution
        {
            get { return resolution; }
        }

        public bool AutoRedraw
        {
            get
            {
                return autoRedraw;
            }
        }

        public void Apply()
        {
            FastNoise.SetSeed(seed);
            FastNoise.SetFrequency(frequency);
            FastNoise.SetInterp(interp);
            FastNoise.SetNoiseType(FastNoiseType);
            FastNoise.SetFractalOctaves(octaves);
            FastNoise.SetFractalLacunarity(lacunarity);
            FastNoise.SetFractalGain(gain);
            FastNoise.SetFractalType(fractalType);
            FastNoise.SetCellularDistanceFunction(cellularDistanceFunction);
            FastNoise.SetCellularReturnType(cellularReturnType);
            FastNoise.SetCellularNoiseLookup(cellularNoiseLookup == null ? null : cellularNoiseLookup.FastNoise);
            FastNoise.SetCellularDistance2Indicies(cellularDistanceIndex0, cellularDistanceIndex1);
            FastNoise.SetCellularJitter(cellularJitter);
            FastNoise.SetGradientPerturbAmp(gradientPerturbAmp);
        }

        public void ForEach2D(Vector2Int texSize, Action<Shape, int, int> callback)
        {
            foreach (var shape in Shapes)
            {
                var actualShape = shape.Factory();
                var bounds      = actualShape.Bounds();
                var tw = texSize.x;
                var th = texSize.y;

                var minX = (int) (bounds.min.x * tw);
                var minY = (int) (bounds.min.y * th);
                var maxX = (int) (bounds.max.x * tw);
                var maxY = (int) (bounds.max.y * th);

                for (var y = minY; y < maxY; ++y)
                    for (var x = minX; x < maxX; ++x)
                        callback(actualShape, x, y);
            }
        }

        public bool ForEach3D(Vector3Int texSize, Action<Shape, Vector3Int> callback, Func<int, int, bool> progressionCallback)
        {
            var total = Shapes.Select(x =>
            {
                var size = x.Factory().Bounds().size;
                return (int)(size.x * texSize.x * size.y * texSize.y * size.z * texSize.z);
            }).Sum();
            var i = 0;

            foreach (var shape in Shapes)
            {
                var actualShape = shape.Factory();
                var bounds      = actualShape.Bounds();
                var tw = texSize.x;
                var th = texSize.y;
                var td = texSize.z;

                var minX = (int) (bounds.min.x * tw);
                var minY = (int) (bounds.min.y * th);
                var minZ = (int) (bounds.min.z * td);
                var maxX = (int) (bounds.max.x * tw);
                var maxY = (int) (bounds.max.y * th);
                var maxZ = (int) (bounds.max.z * td);

                for (var z = minZ; z < maxZ; ++z)
                {
                    if (progressionCallback(i, total)) return false;
                    for (var y = minY; y < maxY; ++y)
                    for (var x = minX; x < maxX; ++x, ++i)
                        callback(actualShape, new Vector3Int(x, y, z));
                }
            }

            return true;
        }
    }
}