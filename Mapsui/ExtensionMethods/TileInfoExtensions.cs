using BruTile;

// ReSharper disable once CheckNamespace
namespace BruTile.Extensions
{
    public static class TileInfoExtensions
    {
        public static string Description(this TileInfo info)
        {
            return $"L{info.Index.Level}-C{info.Index.Col}-R{info.Index.Row}";
        }
    }
}