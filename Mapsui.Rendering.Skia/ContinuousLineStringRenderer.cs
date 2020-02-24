using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using BruTile.Tms;
using Mapsui.Geometries;
using Mapsui.Providers;
using Mapsui.Styles;
using SkiaSharp;

namespace Mapsui.Rendering.Skia
{
    public static class ContinuousLineStringRenderer
    {
        public static TimeSpan TotalTimeSpan = TimeSpan.Zero;

        public static void Draw(SKCanvas canvas, IReadOnlyViewport viewport, IStyle style, IFeature feature,
            IGeometry geometry, float opacity)
        {
            var continuousLineString = (ContinuousLineString)geometry;
            if (continuousLineString.Vertices.Count <= 1) return;


            float lineWidth = 1;
            var lineColor = new Color();

            var vectorStyle = style as VectorStyle;
            var strokeCap = PenStrokeCap.Butt;
            var strokeJoin = StrokeJoin.Miter;
            var strokeMiterLimit = 4f;
            var strokeStyle = PenStyle.Solid;
            float[] dashArray = null;

            if (vectorStyle != null)
            {
                lineWidth = (float)vectorStyle.Line.Width;
                lineColor = vectorStyle.Line.Color;
                strokeCap = vectorStyle.Line.PenStrokeCap;
                strokeJoin = vectorStyle.Line.StrokeJoin;
                strokeMiterLimit = vectorStyle.Line.StrokeMiterLimit;
                strokeStyle = vectorStyle.Line.PenStyle;
                dashArray = vectorStyle.Line.DashArray;
            }


            using (var paint = new SKPaint { IsAntialias = true })
            {
                paint.IsStroke = true;
                paint.StrokeWidth = lineWidth;
                paint.Color = lineColor.ToSkia(opacity);
                paint.StrokeCap = strokeCap.ToSkia();
                paint.StrokeJoin = strokeJoin.ToSkia();
                paint.StrokeMiter = strokeMiterLimit;
                if (strokeStyle != PenStyle.Solid)
                    paint.PathEffect = strokeStyle.ToSkia(lineWidth, dashArray);
                else
                    paint.PathEffect = null;

                DateTime startTime = DateTime.UtcNow;

                var path = continuousLineString.Vertices.ToContinuousSkiaPath(viewport, canvas.LocalClipBounds, 300);
                canvas.DrawPath(path, paint);

                var timeTaken = DateTime.UtcNow - startTime;
                TotalTimeSpan = TotalTimeSpan.Add(timeTaken);
                //Console.WriteLine($"********* MultiLineStringRenderer.Draw ({continuousLineString.Vertices.Count} items) *** took {timeTaken.TotalSeconds}.{timeTaken.Milliseconds}. Total time used:  {TotalTimeSpan.TotalSeconds}.{TotalTimeSpan.Milliseconds}");
            }
        }
    }
}