
using Mapsui.Geometries;
using System;
using Mapsui.Styles;

namespace Mapsui.Widgets
{
    public class ToggleButtonWidget : Widget
    {
        public ToggleButtonWidget(ImageStyle normalStyle, ImageStyle selectedImage)
        {
            ImageNormal = normalStyle;
            ImageSelected = selectedImage;
        }

        public bool IsSelected { get; set; }

        public event EventHandler<ImageButtonWidgetArguments> Touched;

        public override bool HandleWidgetTouched(INavigator navigator, Point position)
        {
            IsSelected = !IsSelected;
            var args = new ImageButtonWidgetArguments(IsSelected);
            Touched?.Invoke(this, args);
            return args.Handled;
        }

        public ImageStyle ImageNormal { get; set; }
        public ImageStyle ImageSelected { get; set; }

        public int Left { get; set; } = 20;
        public int Top { get; set; } = 20;
        public int Height { get; set; } = 50;
        public int Width { get; set; } = 50;
    }

    public class ImageButtonWidgetArguments
    {
        public ImageButtonWidgetArguments(bool isSelected)
        {
            IsSelected = isSelected;
        }
        public bool Handled = false;
        public bool IsSelected { get; set; }
    }
}
