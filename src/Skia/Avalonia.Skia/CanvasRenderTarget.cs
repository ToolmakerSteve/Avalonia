using System;
using System.Collections.Generic;
using System.IO;
using Avalonia.Platform;
using Avalonia.Rendering;
using SkiaSharp;

namespace Avalonia.Skia
{
    internal class CanvasRenderTarget : IDrawingContextLayerImpl
    {
        public CanvasRenderTarget(SKCanvas canvas)
        {
            Canvas = canvas;
        }


        public SKCanvas Canvas { get; }


        #region "-- IDrawingContextLayerImpl --"
        public bool CanBlit => throw new NotImplementedException();

        public Vector Dpi => throw new NotImplementedException();

        public PixelSize PixelSize => throw new NotImplementedException();

        public int Version => throw new NotImplementedException();

        public void Blit(IDrawingContextImpl context)
        {
            throw new NotImplementedException();
        }

        public IDrawingContextImpl CreateDrawingContext(IVisualBrushRenderer visualBrushRenderer)
        {
            return Helpers.DrawingContextHelper.WrapSkiaCanvas(Canvas, new Vector(96, 96));
        }

        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public void Save(string fileName)
        {
            throw new NotImplementedException();
        }

        public void Save(Stream stream)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
