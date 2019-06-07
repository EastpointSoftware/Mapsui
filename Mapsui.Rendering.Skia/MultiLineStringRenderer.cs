using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.ExceptionServices;
using Mapsui.Geometries;
using Mapsui.Providers;
using Mapsui.Styles;
using SkiaSharp;

namespace Mapsui.Rendering.Skia
{
    public static class MultiLineStringRenderer
    {
        public static TimeSpan TotalTimeSpan = TimeSpan.Zero;

        public static void Draw(SKCanvas canvas, IReadOnlyViewport viewport, IStyle style, IFeature feature,
            IGeometry geometry,
            float opacity, float labelTextPadding)
        {
            var multiLineString = (MultiLineString) geometry;
            if (multiLineString.LineStrings.Count <= 1) return;

            var a = multiLineString.First().BoundingBox.Centroid;
            var b = multiLineString.Last().BoundingBox.Centroid;
            
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

                var startPoints = multiLineString
                    .LineStrings
                    .Where(x => x.BoundingBox.Intersects(viewport.Extent))
                    .Select(x => x.StartPoint)
                    .Append(multiLineString
                        .LineStrings.Last().EndPoint)
                    .ToArray();

                var resampleLimit = 500;

                if (startPoints.Length > resampleLimit)
                {
                    var temp = new Point[resampleLimit];
                    temp[0] = startPoints[0];

                    var sampleStep = startPoints.Length / resampleLimit;
                    for (int i = 1; i < resampleLimit-1; i++)
                    {
                        temp[i] = startPoints[(int)( i * sampleStep)];
                    }
                    temp[resampleLimit - 1] = startPoints[startPoints.Length - 1];
                    startPoints = temp;
                }

                var path = startPoints.ToSkiaPath(viewport, canvas.LocalClipBounds);
                canvas.DrawPath(path, paint);

                var timeTaken = DateTime.UtcNow - startTime;
                TotalTimeSpan = TotalTimeSpan.Add(timeTaken);
                Console.WriteLine($"********* MultiLineStringRenderer.Draw ({startPoints?.Count()} items) *** took {timeTaken.TotalSeconds}.{timeTaken.Milliseconds}. Total time used:  {TotalTimeSpan.TotalSeconds}.{TotalTimeSpan.Milliseconds}");
            }
        }
    }
}