﻿using Mapsui.Geometries;
using SkiaSharp;

namespace Mapsui.Rendering.Skia
{
    public static class SKRectExtensions
    {
        public static BoundingBox ToMapsui(this SKRect rect)
        {
            return new BoundingBox(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }
    }
}
