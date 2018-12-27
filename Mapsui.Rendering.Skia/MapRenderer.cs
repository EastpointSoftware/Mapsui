using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mapsui.Geometries;
using Mapsui.Layers;
using Mapsui.Logging;
using Mapsui.Providers;
using Mapsui.Rendering.Skia.SkiaWidgets;
using Mapsui.Styles;
using Mapsui.Styles.Thematics;
using Mapsui.Widgets;
using Mapsui.Widgets.ScaleBar;
using Mapsui.Widgets.Zoom;
using Newtonsoft.Json;
using SkiaSharp;

namespace Mapsui.Rendering.Skia
{
    public class MapRenderer : IRenderer
    {
        private const int TilesToKeepMultiplier = 3;
        private const int MinimumTilesToKeep = 32;
        private readonly SymbolCache _symbolCache = new SymbolCache();

        private readonly IDictionary<object, BitmapInfo> _tileCache =
            new Dictionary<object, BitmapInfo>(new IdentityComparer<object>());

        private long _currentIteration;

        public ISymbolCache SymbolCache => _symbolCache;

        public IDictionary<Type, IWidgetRenderer> WidgetRenders { get; } = new Dictionary<Type, IWidgetRenderer>();

        static MapRenderer()
        {
            DefaultRendererFactory.Create = () => new MapRenderer();
        }

        public MapRenderer()
        {
            WidgetRenders[typeof(Hyperlink)] = new HyperlinkWidgetRenderer();
            WidgetRenders[typeof(ScaleBarWidget)] = new ScaleBarWidgetRenderer();
            WidgetRenders[typeof(ZoomInOutWidget)] = new ZoomInOutWidgetRenderer();
        }

        public void Render(object target, IReadOnlyViewport viewport, IEnumerable<ILayer> layers,
            IEnumerable<IWidget> widgets, Color background = null)
        {
            var allWidgets = layers.Select(l => l.Attribution).Where(w => w != null).ToList().Concat(widgets);
            RenderTypeSave((SKCanvas)target, viewport, layers, allWidgets, background);
        }

        private void RenderTypeSave(SKCanvas canvas, IReadOnlyViewport viewport, IEnumerable<ILayer> layers,
            IEnumerable<IWidget> widgets, Color background = null)
        {
            if (!viewport.HasSize) return;

            if (background != null) canvas.Clear(background.ToSkia(1));
            Render(canvas, viewport, layers);
            Render(canvas, viewport, widgets, 1);
        }

        public MemoryStream RenderToBitmapStream(IReadOnlyViewport viewport, IEnumerable<ILayer> layers, Color background = null)
        {
            try
            {
                using (var surface = SKSurface.Create(
                    (int)viewport.Width, (int)viewport.Height, SKImageInfo.PlatformColorType, SKAlphaType.Unpremul))
                {
                    if (surface == null) return null;
                    // Not sure if this is needed here:
                    if (background != null) surface.Canvas.Clear(background.ToSkia(1));
                    Render(surface.Canvas, viewport, layers);
                    using (var image = surface.Snapshot())
                    {
                        using (var data = image.Encode())
                        {
                            var memoryStream = new MemoryStream();
                            data.SaveTo(memoryStream);
                            return memoryStream;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log(LogLevel.Error, ex.Message);
                return null;
            }
        }

        private void Render(SKCanvas canvas, IReadOnlyViewport viewport, IEnumerable<ILayer> layers)
        {
            try
            {
                layers = layers.ToList();

                var mergedBaseTiles = new TileLayer(null);
                
                foreach (var layer in layers)
                {
                    if (layer.Enabled == false) continue;
                    if (layer.MinVisible > viewport.Resolution) continue;
                    if (layer.MaxVisible < viewport.Resolution) continue;

                    IterateLayer(canvas, viewport, layer);

                    // RenderFeature(canvas, v, l, s, o);
                }

                //VisibleFeatureIterator.IterateMapLayers(viewport, layers, (v, l, s, o) => { RenderFeature(canvas, v, l, s, o); });

                RemovedUnusedBitmapsFromCache();

                _currentIteration++;
            }
            catch (Exception exception)
            {
                Logger.Log(LogLevel.Error, "Unexpected error in skia renderer", exception);
            }
        }

        private void callback(SKCanvas canvas, IReadOnlyViewport v, IStyle l, IFeature s, float o)
        {
            RenderFeature(canvas, v, l, s, o);
        }

        private void IterateLayer(SKCanvas canvas, IReadOnlyViewport viewport, ILayer layer)
        {
            var features = layer.GetFeaturesInView(viewport.Extent, viewport.Resolution).ToList();
            Console.WriteLine("LAYER: " + layer.Name);
            Console.WriteLine();
            var layerStyles = ToArray(layer);
            foreach (var layerStyle in layerStyles)
            {
                var style = layerStyle; // This is the default that could be overridden by an IThemeStyle

                foreach (var feature in features)
                {
                    if (layerStyle is IThemeStyle) style = (layerStyle as IThemeStyle).GetStyle(feature);
                    if (ShouldNotBeApplied(style, viewport)) continue;

                    if (style is StyleCollection styles) // The ThemeStyle can again return a StyleCollection
                    {
                        foreach (var s in styles)
                        {
                            if (ShouldNotBeApplied(s, viewport)) continue;
                            callback(canvas, viewport, s, feature, (float)layer.Opacity);
                        }
                    }
                    else
                    {
                        if(feature?.Geometry is IRaster raster)
                            Console.WriteLine("FEATURE: " + raster.Description);
                        callback(canvas, viewport, style, feature, (float)layer.Opacity);
                    }
                }
            }

            foreach (var feature in features)
            {
                var featureStyles = feature.Styles ?? Enumerable.Empty<IStyle>(); // null check
                foreach (var featureStyle in featureStyles)
                {
                    if (ShouldNotBeApplied(featureStyle, viewport)) continue;

                    callback(canvas, viewport, featureStyle, feature, (float)layer.Opacity);

                }
            }
        }

        private static bool ShouldNotBeApplied(IStyle style, IReadOnlyViewport viewport)
        {
            return style == null || !style.Enabled || style.MinVisible > viewport.Resolution || style.MaxVisible < viewport.Resolution;
        }

        private static IStyle[] ToArray(ILayer layer)
        {
            return (layer.Style as StyleCollection)?.ToArray() ?? new[] { layer.Style };
        }


        private void RemovedUnusedBitmapsFromCache()
        {
            var tilesUsedInCurrentIteration =
                _tileCache.Values.Count(i => i.IterationUsed == _currentIteration);
            var tilesToKeep = tilesUsedInCurrentIteration * TilesToKeepMultiplier;
            tilesToKeep = Math.Max(tilesToKeep, MinimumTilesToKeep);
            var tilesToRemove = _tileCache.Keys.Count - tilesToKeep;

            if (tilesToRemove > 0) RemoveOldBitmaps(_tileCache, tilesToRemove);
        }

        private static void RemoveOldBitmaps(IDictionary<object, BitmapInfo> tileCache, int numberToRemove)
        {
            var counter = 0;
            var orderedKeys = tileCache.OrderBy(kvp => kvp.Value.IterationUsed).Select(kvp => kvp.Key).ToList();
            foreach (var key in orderedKeys)
            {
                if (counter >= numberToRemove) break;
                var textureInfo = tileCache[key];
                tileCache.Remove(key);
                textureInfo.Bitmap.Dispose();
                counter++;
            }
        }

        private void RenderFeature(SKCanvas canvas, IReadOnlyViewport viewport, IStyle style, IFeature feature, float layerOpacity)
        {
            if (feature.Geometry is Point)
                PointRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, _symbolCache, layerOpacity * style.Opacity);
            else if (feature.Geometry is MultiPoint)
                MultiPointRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, _symbolCache, layerOpacity * style.Opacity);
            else if (feature.Geometry is LineString)
                LineStringRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, layerOpacity * style.Opacity);
            else if (feature.Geometry is MultiLineString)
                MultiLineStringRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, layerOpacity * style.Opacity);
            else if (feature.Geometry is Polygon)
                PolygonRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, layerOpacity * style.Opacity, _symbolCache);
            else if (feature.Geometry is MultiPolygon)
                MultiPolygonRenderer.Draw(canvas, viewport, style, feature, feature.Geometry, layerOpacity * style.Opacity, _symbolCache);
            else if (feature.Geometry is IRaster)
                RasterRenderer.Draw(canvas, viewport, style, feature, layerOpacity * style.Opacity, _tileCache, _currentIteration);
        }

        private void Render(object canvas, IReadOnlyViewport viewport, IEnumerable<IWidget> widgets, float layerOpacity)
        {
            WidgetRenderer.Render(canvas, viewport, widgets, WidgetRenders, layerOpacity);
        }
    }

    public class IdentityComparer<T> : IEqualityComparer<T> where T : class
    {
        public bool Equals(T obj, T otherObj)
        {
            return obj == otherObj;
        }

        public int GetHashCode(T obj)
        {
            return obj.GetHashCode();
        }
    }
}