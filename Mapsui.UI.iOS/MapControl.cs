using CoreFoundation;
using Foundation;
using UIKit;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using CoreGraphics;
using Mapsui.Geometries;
using Mapsui.Geometries.Utilities;
using SkiaSharp.Views.iOS;
using static Mapsui.UI.iOS.IosPointExtensions;

namespace Mapsui.UI.iOS
{
    [Register("MapControl"), DesignTimeVisible(true)]
    public partial class MapControl : UIView, IMapControl
    {
        private readonly SKGLView _canvas = new SKGLView();
        private double _innerRotation;
        
        public MapControl(CGRect frame)
            : base(frame)
        {
            Initialize(); 
        }

        [Preserve]
        public MapControl(IntPtr handle) : base(handle) // used when initialized from storyboard
        {
            Initialize();
        }

        public void Initialize()
        {
            Map = new Map();
            BackgroundColor = UIColor.White;

            _canvas.TranslatesAutoresizingMaskIntoConstraints = false;
            _canvas.MultipleTouchEnabled = true;
            _canvas.PaintSurface += OnPaintSurface;
            AddSubview(_canvas);

            AddConstraints(new[] {
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Leading, NSLayoutRelation.Equal, _canvas, NSLayoutAttribute.Leading, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Trailing, NSLayoutRelation.Equal, _canvas, NSLayoutAttribute.Trailing, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Top, NSLayoutRelation.Equal, _canvas, NSLayoutAttribute.Top, 1.0f, 0.0f),
                NSLayoutConstraint.Create(this, NSLayoutAttribute.Bottom, NSLayoutRelation.Equal, _canvas, NSLayoutAttribute.Bottom, 1.0f, 0.0f)
            });

            ClipsToBounds = true;
            MultipleTouchEnabled = true;
            UserInteractionEnabled = true;
            
            var doubleTapGestureRecognizer = new UITapGestureRecognizer(OnDoubleTapped)
            {
                NumberOfTapsRequired = 2,
                CancelsTouchesInView = false,
            };
            AddGestureRecognizer(doubleTapGestureRecognizer);

            var tapGestureRecognizer = new UITapGestureRecognizer(OnSingleTapped)
            {
                NumberOfTapsRequired = 1,
                CancelsTouchesInView = false,
            };
            tapGestureRecognizer.RequireGestureRecognizerToFail(doubleTapGestureRecognizer);
            AddGestureRecognizer(tapGestureRecognizer);

            _viewport.SetSize(ViewportWidth, ViewportHeight);

        }

        public float PixelDensity => (float)_canvas.ContentScaleFactor;
        public float EffectivePixelDensity => ApplyDevicePixelDensity ? (float)PixelDensity : 1; // todo: Check if I need canvas

        private void OnDoubleTapped(UITapGestureRecognizer gesture)
        {
            var position = GetScreenPosition(gesture.LocationInView(this));
            OnInfo(InvokeInfo(position, position, 2));
        }
        
        private void OnSingleTapped(UITapGestureRecognizer gesture)
        {
            var position = GetScreenPosition(gesture.LocationInView(this));
            OnInfo(InvokeInfo(position, position, 1));
        }
       
        void OnPaintSurface(object sender, SKPaintGLSurfaceEventArgs args)
        {
            if (Math.Abs(EffectivePixelDensity - 1) > 0.1)
            {
                args.Surface.Canvas.Scale(EffectivePixelDensity, EffectivePixelDensity);
            }  
            Renderer.Render(args.Surface.Canvas, Viewport, _map.Layers, _map.Widgets, _map.BackColor);
        }

        public override void TouchesBegan(NSSet touches, UIEvent evt)
        {
            base.TouchesBegan(touches, evt);

            _innerRotation = Viewport.Rotation;
            if (evt.AllTouches.Count == 1)
            {
                // this could be a drag (pan) request

                // setup the timer to handle waiting for a delay...
                _lastTouchDownDateTime = DateTime.UtcNow;
                _longHoldFocusCheck = new System.Timers.Timer
                {
                    AutoReset = false,
                    Interval = 550,
                    Enabled = true
                };
                _longHoldFocusCheck.Elapsed -= LongHoldFocusCheckOnElapsed;
                _longHoldFocusCheck.Elapsed += LongHoldFocusCheckOnElapsed;
            }
        }

        public override void TouchesMoved(NSSet touches, UIEvent evt)
        {
            base.TouchesMoved(touches, evt);

            if (evt.AllTouches.Count == 1)
            {
                if (touches.AnyObject is UITouch t)
                {
                    var position = t.LocationInView(this).ToMapsui().ToScaledPoint(ViewportSizeScalar);
                    
                    _previousTouch = t.PreviousLocationInView(this).ToMapsui().ToScaledPoint(ViewportSizeScalar);

                    var touch = position;
                    if (_previousTouch != null && !_previousTouch.IsEmpty())
                    {
                        // check if a layer wants to intercept this motion
                        for (var i = 1; i <= Map.Layers.Count; i++)
                        {
                            var layer = Map.Layers[Map.Layers.Count - i];
                            if (layer.HandleDrag(touch, _previousTouch, _lastTouchDownDateTime))
                            {
                                RefreshGraphics();
                                _previousTouch = touch;
                                return;
                            }
                        }
                        _viewport.Transform(touch, _previousTouch);
                        RefreshGraphics();
                    }
                    _previousTouch = touch;

                    _innerRotation = Viewport.Rotation;


                }
            }
            else if (evt.AllTouches.Count >= 2)
            {
                var previousLocation = evt.AllTouches.Select(t => ((UITouch)t).PreviousLocationInView(this))
                                           .Select(p => new Point(p.X, p.Y)).ToList();

                var locations = evt.AllTouches.Select(t => ((UITouch)t).LocationInView(this))
                                        .Select(p => new Point(p.X, p.Y)).ToList();

                var (previousCenter, previousRadius, previousAngle) = GetPinchValues(previousLocation);
                var (center, radius, angle) = GetPinchValues(locations);

                double rotationDelta = 0;

                if (!Map.RotationLock)
                {
                    _innerRotation += angle - previousAngle;
                    _innerRotation %= 360;

                    if (_innerRotation > 180)
                        _innerRotation -= 360;
                    else if (_innerRotation < -180)
                        _innerRotation += 360;

                    if (Viewport.Rotation == 0 && Math.Abs(_innerRotation) >= Math.Abs(UnSnapRotationDegrees))
                        rotationDelta = _innerRotation;
                    else if (Viewport.Rotation != 0)
                    {
                        if (Math.Abs(_innerRotation) <= Math.Abs(ReSnapRotationDegrees))
                            rotationDelta = -Viewport.Rotation;
                        else
                            rotationDelta = _innerRotation - Viewport.Rotation;
                    }
                }

                _viewport.Transform(center, previousCenter, radius / previousRadius, rotationDelta);
                RefreshGraphics();
            }
        }

        public override void TouchesEnded(NSSet touches, UIEvent e)
        {
            for (var i = 1; i <= Map.Layers.Count; i++)
            {
                var layer = Map.Layers[Map.Layers.Count - i];
                layer.HandleGestureEnd();
            }
            _previousTouch = null;
            if (_longHoldFocusCheck != null)
            {
                _longHoldFocusCheck.Elapsed -= LongHoldFocusCheckOnElapsed;
            }
            Refresh();
        }
         
        /// <summary>
        /// Gets screen position in device independent units (or DIP or DP).
        /// </summary>
        /// <param name="point"></param>
        /// <returns></returns>
        private Point GetScreenPosition(CGPoint point)
        {
            return new Point(point.X * ViewportSizeScalar, point.Y * ViewportSizeScalar);
        }
       
        private void RunOnUIThread(Action action)
        {
            DispatchQueue.MainQueue.DispatchAsync(action);
        }
        
        public void RefreshGraphics()
        {
            RunOnUIThread(() =>
            {
                SetNeedsDisplay();
                _canvas?.SetNeedsDisplay();
            });
        }

        public override CGRect Frame
        {
            get => base.Frame;
            set
            {
                _canvas.Frame = value;
                base.Frame = value;
                SetViewportSize();
                OnPropertyChanged();
            }
        }

        public override void LayoutMarginsDidChange()
        {
            if (_canvas == null) return;

            base.LayoutMarginsDidChange();
            SetViewportSize();
        }

        public void OpenBrowser(string url)
        {
            UIApplication.SharedApplication.OpenUrl(new NSUrl(url));
        }

        public new void Dispose()
        {
            Unsubscribe();
            base.Dispose();
        }

        protected override void Dispose(bool disposing)
        {
            Unsubscribe();
            base.Dispose(disposing);
        }

        private (Point centre, double radius, double angle) GetPinchValues(List<Point> locations)
        {
            if (locations.Count < 2)
                throw new ArgumentException();

            double centerX = 0;
            double centerY = 0;

            foreach (var location in locations)
            {
                centerX += location.X;
                centerY += location.Y;
            }

            centerX = centerX * (int)ViewportSizeScalar / locations.Count ;
            centerY = centerY * (int)ViewportSizeScalar / locations.Count;

            var radius = Algorithms.Distance(centerX, centerY, locations[0].X * (int)ViewportSizeScalar, locations[0].Y * (int)ViewportSizeScalar);

            var angle = Math.Atan2(locations[1].Y - locations[0].Y, locations[1].X - locations[0].X) * 180.0 / Math.PI;

            return (new Point(centerX, centerY), radius, angle);
        }

        /// <summary>
        /// if we're not upscaling the image, we need to resize it from the off
        /// </summary>
        private float ViewportSizeScalar => ApplyDevicePixelDensity ? 1 : PixelDensity;

        private float ViewportWidth => (float)_canvas.Frame.Width * ViewportSizeScalar; // todo: check if we need _canvas
        private float ViewportHeight => (float)_canvas.Frame.Height * ViewportSizeScalar; // todo: check if we need _canvas
    }

    public static class IosPointExtensions{
        public static Point ToScaledPoint(this Point point, float scale)
        {
            return new Point(point.X * scale, point.Y * scale);
        }
    }
}