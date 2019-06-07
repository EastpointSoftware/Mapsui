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

                double resampleLimit = 250;

                //var countInView = multiLineString.LineStrings.Count(x => x.BoundingBox.Intersects(viewport.Extent));

                //var reductionFactor = (double)countInView / (resampleLimit - 1);

                // get the line start points, but reduce sample set up 500x
                var startPoints = multiLineString
                    .LineStrings
                    .Where(x => x.BoundingBox.Intersects(viewport.Extent))
                    //.Select((x, index) => new {x.StartPoint.X, x.StartPoint.Y, Qualifier = (int)(index / reductionFactor)})
                    //.GroupBy(x => x.Qualifier )
                    //.Select(x => new Point(x.ElementAt(0).X, x.ElementAt(0).Y))
                    .Select(x => x.StartPoint)
                    .Append(multiLineString.LineStrings.Last().EndPoint)
                    .ToArray();

                var resampleTimeTaken = DateTime.UtcNow - startTime;
                TotalTimeSpan = TotalTimeSpan.Add(resampleTimeTaken);
                Console.WriteLine($"********* MultiLineStringRenderer.Draw - Resample:  ({multiLineString.Count()} down to {startPoints?.Count()} items) *** took {resampleTimeTaken.TotalSeconds}.{resampleTimeTaken.Milliseconds}.");

                //var resampleLimit = 1000;

                //if (startPoints.Length > resampleLimit)
                //{
                //    var temp = new Point[resampleLimit];
                //    temp[0] = startPoints[0];

                //    int sampleStep = (int)startPoints.Length / resampleLimit; // rounds down to nearest integer
                //    int tempIndex = 0;
                //    int sampleIndex = 0;
                //    int repeatCondition = resampleLimit - 1;

                //    temp[tempIndex++] = startPoints[sampleIndex]; // tempIndex should increment after operation in the same CPU cycle

                //    while (tempIndex < repeatCondition)
                //    {
                //        sampleIndex += sampleStep; // same as (tempIndex * sampleIndex)
                //        temp[tempIndex++] = startPoints[sampleIndex]; // tempIndex should increment after this opetion in the same CPU cycle
                //    }

                //    //unsafe
                //    //{
                //    //    //// -- first optimise the loop
                //    //    //var sampleStep = startPoints.Length / resampleLimit;
                //    //    //int nSamples = 0;
                //    //    //while (nSamples < resampleLimit -1)
                //    //    //{
                //    //    //    temp[nSamples] = startPoints[(int) (nSamples * sampleStep)];
                //    //    //    nSamples++;
                //    //    //}

                //    //    // -- second optimise the array get

                //    //    //// -- third optimise the array get with pointers -- need to convert Point class to value type (use struct instead of class)
                //    //    //int sampleStep = (int)startPoints.Length / resampleLimit; // rounds down to nearest integer
                //    //    //int tempIndex = 0;
                //    //    //int sampleIndex = 0;

                //    //    //ref Point tmpPtr = ref temp[0];
                //    //    //ref Point dataPtr = ref startPoints[0];

                //    //    //temp[tempIndex++] = startPoints[sampleIndex]; // tempIndex should increment after operation in same cycle

                //    //    //while (tempIndex < resampleLimit - 1)
                //    //    //{
                //    //    //    // update data pointer
                //    //    //    //sampleIndex += sampleStep; // same as (tempIndex * sampleIndex)
                //    //    //    Unsafe.Add(ref dataPtr, sampleStep); // adds offeset to pointer -- should be accumulating every loop
                //    //    //    temp[tempIndex++] = new Point // <-- this would be much faster if Point was a VALUE type (struct) not ref
                //    //    //    {
                //    //    //        X = dataPtr.X, // <-- I'm hoping that this reference would stay as value and not update in the next iteration
                //    //    //        Y = dataPtr.Y
                //    //    //    }; 
                //    //    //}

                //    //    // --- fourth option: do the same as above but use pointers for temp array too

                //    //    // --- fifth optimise to use ONLY byte pointers.
                //    //    // 1. convert startPoints array to array of bytes
                //    //    // 2. create byte pointer
                //    //    // 3. loop through for number of samples you want to display
                //    //    // 4. each loop, increment the pointer position like: pointer + sizeof(Point) * NumberOfSamplesToSkip
                //    //    // 5. assing the value from where the pointer is at -- probably need to use Marshall to move a block of bytes, and then convert to Point object

                //    //}


                //    //var sampleStep = startPoints.Length / resampleLimit;
                //    //for (int i = 1; i < resampleLimit-1; i++)
                //    //{
                //    //    temp[i] = startPoints[(int)( i * sampleStep)];
                //    //}
                //    temp[resampleLimit - 1] = startPoints[startPoints.Length - 1];
                //    startPoints = temp;
                //}

                var path = startPoints.ToSkiaPath(viewport, canvas.LocalClipBounds);
                canvas.DrawPath(path, paint);

                var timeTaken = DateTime.UtcNow - startTime;
                TotalTimeSpan = TotalTimeSpan.Add(timeTaken);
                Console.WriteLine($"********* MultiLineStringRenderer.Draw ({startPoints?.Count()} items) *** took {timeTaken.TotalSeconds}.{timeTaken.Milliseconds}. Total time used:  {TotalTimeSpan.TotalSeconds}.{TotalTimeSpan.Milliseconds}");
            }
        }
    }
}