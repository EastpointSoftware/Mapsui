using System;
using System.Collections.Generic;
using System.Text;
using Mapsui.Geometries;
using Mapsui.Widgets;
using SkiaSharp;

namespace Mapsui.Rendering.Skia.SkiaWidgets
{
    public class ImageButtonWidgetRenderer : ISkiaWidgetRenderer
    {
        public void Draw(SKCanvas canvas, IReadOnlyViewport viewport, IWidget widget, float layerOpacity, SymbolCache symbolCache)
        {
            var buttonWidget = (ToggleButtonWidget)widget;
            var position = new BoundingBox(buttonWidget.Left, buttonWidget.Top, buttonWidget.Left + buttonWidget.Width, buttonWidget.Top + buttonWidget.Height);
            var rect = new SKRect(
                (int)position.Left, (int)position.Top,
                (int)position.Right, (int)position.Bottom);

            buttonWidget.Envelope = rect.ToMapsui();

            int bitmapId = !buttonWidget.IsSelected ? 
                buttonWidget.ImageNormal.BitmapId : 
                buttonWidget.ImageSelected.BitmapId;

            var bitmap = symbolCache.GetOrCreate(bitmapId);
            BitmapHelper.RenderRaster(canvas,
                bitmap.Bitmap,
                position.ToSkia(),
                1);
        }
    }
}
