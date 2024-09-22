using System.ComponentModel;
using System.Runtime.Serialization;
using System.Xml.Serialization;
using static MapLib.Map;

namespace MapLib
{
    public class Map
    {
        [XmlIgnore]
        public List<TileType[,]> Layers { get; set; } = [];

        [XmlArray("Layers")]
        [XmlArrayItem("Layer")]
        [Browsable(false)]
        public TileType[][][] LayersDto
        {
            get
            {
                TileType[][][] layers = new TileType[Layers.Count][][];
                for (int i = 0; i < Layers.Count; i++)
                {
                    TileType[,] layer = Layers[i];
                    layers[i] = new TileType[layer.GetLength(0)][];
                    for (int x = 0; x < layer.GetLength(0); x++)
                    {
                        layers[i][x] = new TileType[layer.GetLength(1)];
                        for (int y = 0; y < layer.GetLength(1); y++)
                        {
                            layers[i][x][y] = layer[x, y];
                        }
                    }
                }
                return layers;
            }
            set
            {
                Layers.Clear();
                for (int i = 0; i < value.Length; i++)
                {
                    TileType[,] layer = new TileType[value[i].Length, value[i][0].Length];
                    for (int x = 0; x < value[i].Length; x++)
                    {
                        for (int y = 0; y < value[i][x].Length; y++)
                        {
                            layer[x, y] = value[i][x][y];
                        }
                    }
                    Layers.Add(layer);
                }
            }
        }

        [XmlIgnore]
        public List<WallTile[,]> WallLayers { get; set; } = [];

        [XmlArray("WallLayers")]
        [XmlArrayItem("WallLayer")]
        [Browsable(false)]
        public WallTile[][][] WallLayerDto
        {
            get
            {
                WallTile[][][] wallTiles = new WallTile[WallLayers.Count][][];
                for (int i = 0; i < WallLayers.Count; i++)
                {
                    WallTile[,] wallTile = WallLayers[i];
                    wallTiles[i] = new WallTile[wallTile.GetLength(0)][];
                    for (int x = 0; x < wallTile.GetLength(0); x++)
                    {
                        wallTiles[i][x] = new WallTile[wallTile.GetLength(1)];
                        for (int y = 0; y < wallTile.GetLength(1); y++)
                        {
                            wallTiles[i][x][y] = wallTile[x, y];
                        }
                    }
                }
                return wallTiles;
            }
            set
            {
                WallLayers.Clear();
                for (int i = 0; i < value.Length; i++)
                {
                    WallTile[,] wallTile = new WallTile[value[i].Length, value[i][0].Length];
                    for (int x = 0; x < value[i].Length; x++)
                    {
                        for (int y = 0; y < value[i][x].Length; y++)
                        {
                            wallTile[x, y] = value[i][x][y];
                        }
                    }
                    WallLayers.Add(wallTile);
                }
            }
        }

        private Map() { }

        public Map(int width, int height, int layers)
        {
            for (int i = 0; i < layers; i++)
            {
                TileType[,] layer = new TileType[width, height];
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        layer[x, y] = TileType.None;
                    }
                }
                Layers.Add(layer);
            }

            for (int i = 0; i < layers; i++)
            {
                WallTile[,] wallLayer = new WallTile[width, height];
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        wallLayer[x, y] = new WallTile();
                    }
                }
                WallLayers.Add(wallLayer);
            }
        }

        public void SetTile(int x, int y, int layer, TileType type)
        {
            Layers[layer][x, y] = type;
        }

        public void SetWallTile(int x, int y, int layer, TileType type, Direction direction)
        {
            WallLayers[layer][x, y] = WallLayers[layer][x, y]?.WithTile(direction, type) ?? new WallTile().WithTile(direction, type);
        }

        public TileType GetTile(int x, int y, int layer)
        {
            return Layers[layer][x, y];
        }

        public TileType GetWallTile(int x, int y, int layer, Direction direction)
        {
            return WallLayers[layer][x, y]?.GetTile(direction) ?? TileType.None;
        }

        public static void WriteToXmlFile(Map map, string filePath)
        {
            TextWriter? writer = null;
            try
            {
                var serializer = new XmlSerializer(typeof(Map));
                writer = new StreamWriter(filePath, false);
                serializer.Serialize(writer, map);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }

        public static Map ReadFromXmlFile(string filePath)
        {
            TextReader? reader = null;
            try
            {
                var serializer = new XmlSerializer(typeof(Map));
                reader = new StreamReader(filePath);
                Map map = (Map)(serializer.Deserialize(reader) ?? throw new ArgumentException("File not found: ", filePath));
                return map;
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }

        public int Width => Layers[0].GetLength(0);
        public int Height => Layers[0].GetLength(1);
        public enum TileType
        {
            None,
            Floor,
            Wall,
            Door
        }
        public enum Direction
        {
            North,
            East,
            South,
            West
        }

        [Serializable]
        [XmlRootAttribute("WallTile", IsNullable = false)]
        public class WallTile
        {
            public TileType NorthTile { get; set; }
            public TileType EastTile { get; set; }
            public TileType SouthTile { get; set; }
            public TileType WestTile { get; set; }

            public WallTile() { }

            public TileType GetTile(Direction direction)
            {
                return direction switch
                {
                    Direction.North => NorthTile,
                    Direction.East => EastTile,
                    Direction.South => SouthTile,
                    Direction.West => WestTile,
                    _ => TileType.None,
                };
            }

            public void SetTile(Direction direction, TileType type)
            {
                switch (direction)
                {
                    case Direction.North:
                        NorthTile = type;
                        break;
                    case Direction.East:
                        EastTile = type;
                        break;
                    case Direction.South:
                        SouthTile = type;
                        break;
                    case Direction.West:
                        WestTile = type;
                        break;
                }
            }

            public WallTile WithTile(Direction direction, TileType type)
            {
                SetTile(direction, type);
                return this;
            }
        }
    }

    public static class DirectionExtensions
    {
        public static int GetRotation(this Direction direction)
        {
            return direction switch
            {
                Direction.North => 0,
                Direction.East => 90,
                Direction.South => 180,
                Direction.West => 270,
                _ => 0,
            };
        }
    }
}