using System;
using System.Collections.Generic;
using Mapsui.Widgets;
using SkiaSharp;

namespace Mapsui.Rendering.Skia.SkiaWidgets
{
    public static class WidgetRenderer
    {
        public static void Render(object target, IReadOnlyViewport viewport, IEnumerable<IWidget> widgets,
            IDictionary<Type, IWidgetRenderer> renders, float layerOpacity,
            SymbolCache symbolCache)
        {
            var canvas = (SKCanvas) target;

            foreach (var widget in widgets)
            {
                var type = widget.GetType();
                if (renders.ContainsKey(type))
                {
                    ((ISkiaWidgetRenderer)renders[type]).Draw(canvas, viewport, widget, layerOpacity, symbolCache);
                }
                else if (widget is ICustomWidget customWidget)
                {
                    var renderType = customWidget.WidgetRenderer;
                    var renderer = (ISkiaWidgetRenderer)Activator.CreateInstance(renderType);
                    renderer.Draw(canvas, viewport, widget, layerOpacity, symbolCache);
                }
            }
        }
    }
}