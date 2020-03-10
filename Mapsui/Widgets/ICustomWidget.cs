using System;
using Mapsui.Geometries;

namespace Mapsui.Widgets
{
    public interface ICustomWidget : IWidget
    {
        Type WidgetRenderer { get; }
    }
}
