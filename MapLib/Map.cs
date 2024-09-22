using System.ComponentModel;
using System.Runtime.Serialization;
using System.Xml.Serialization;

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
        }

        public void SetTile(int x, int y, int layer, TileType type)
        {
            Layers[layer][x, y] = type;
        }

        public TileType GetTile(int x, int y, int layer)
        {
            return Layers[layer][x, y];
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
    }
}
