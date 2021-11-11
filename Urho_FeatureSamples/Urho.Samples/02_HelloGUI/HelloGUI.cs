/*
// Copyright (c) 2008-2015 the Urho3D project.
// Copyright (c) 2015 Xamarin Inc
// Copyright (c) 2021 ToolmakerSteve and najak3d
//
// MIT License
*/

using System;
using Urho.Gui;
using Urho.Resources;
using SkiaSharp;
using System.Timers;

namespace Urho.Samples
{
	public partial class HelloGUI : Sample
	{
		Window window;
		UIElement uiRoot;

		public HelloGUI(ApplicationOptions options = null) : base(options) { }


		/// <summary>
		/// True to activate my logic, that moves drawing to canvas into background.
		/// False is the original version, that avoids some overhead by making changes
		/// as part of OnUpdate.
		/// </summary>
		const bool MakeChangesInBackground = true;//true;
		private Timer _timer;
		const int DrawsPerSecond = 1000;
		/// <summary>
		/// true to fire at a consistent interval.
		/// false if you want a delay of "interval" AFTER the task finishes, before running again.
		/// Useful if the background task might consume the full interval,
		/// and the purpose of the interval is to ensure that OTHER code gets time to run.
		/// </summary>
		const bool ExactTimeInterval = true;
		/// <summary>
		/// Once the OnUpdate loop has been activated, all canvas accesses must be protected with this lock.
		/// </summary>
		private object _canvasLock = new object();


		private Urho.Gui.Button _sketchPad;
		private SKBitmap _skiaImage;
		private IntPtr _skiaPtr0;
		private Urho.Urho2D.Texture2D _sketchTexture;
		private Urho.Resources.Image _sketchImg;
		private SKCanvas _sketchCanvas;
		private SKPaint _sketchPaint;
		private SKPaint _sketchText;
		private SKPaint _sketchBlank;

		private SKPoint _downPos;
		private SKPoint _newPos;


		#region "-- setup --"
		protected override void Start()
		{
			base.Start();

			uiRoot = UI.Root;
			Input.SetMouseVisible(true, false);
			// Load XML file containing default UI style sheet
			var cache = ResourceCache;
			XmlFile style = cache.GetXmlFile("UI/DefaultStyle.xml");

			// Set the loaded style as default style
			uiRoot.SetDefaultStyle(style);

			// Initialize Window
			InitWindow();
			CreateSkiaSketchPad();
			if (MakeChangesInBackground)
				StartBackgroundTask();

			_AddScene();
		}

		Text windowTitle;

		void InitWindow()
		{
			// Create the Window and add it to the UI's root node
			window = new Window();
			uiRoot.AddChild(window);

			// Set Window size and layout settings
			window.SetMinSize(384, 70);
			window.SetLayout(LayoutMode.Vertical, 6, new IntRect(6, 6, 6, 6));
			//window.SetAlignment(HorizontalAlignment.Center, VerticalAlignment.Center);
			window.Name = "Window";

			// Create Window 'titlebar' container
			UIElement titleBar = new UIElement();
			titleBar.SetMinSize(0, 24);
			titleBar.VerticalAlignment = VerticalAlignment.Top;
			titleBar.LayoutMode = LayoutMode.Horizontal;

			// Create the Window title Text
			var windowTitle = new Text();
			windowTitle.Name = "WindowTitle";
			windowTitle.Value = Graphics.Width + " x " + Graphics.Height; // "Hello GUI!";

			// Create the Window's close button
			Button buttonClose = new Button();
			buttonClose.Name = "CloseButton";

			// Add the controls to the title bar
			titleBar.AddChild(windowTitle);
			titleBar.AddChild(buttonClose);

			// Add the title bar to the Window
			window.AddChild(titleBar);

			// Apply styles
			window.SetStyleAuto(null);
			windowTitle.SetStyleAuto(null);
			buttonClose.SetStyle("CloseButton", null);

			buttonClose.Released += _ => Exit();

			// Subscribe also to all UI mouse clicks just to see where we have clicked
			//UI.UIMouseClick += HandleControlClicked;

			windowTitle = window.GetChild("WindowTitle", true) as Text;

		}

		private unsafe void CreateSkiaSketchPad()
		{
			var cache = ResourceCache;
			var graphics = Graphics;
			SKSizeI size = new SKSizeI(graphics.Width, graphics.Height - 70);

			_skiaImage = new SKBitmap(size.Width, size.Height);
			_sketchCanvas = new SKCanvas(_skiaImage);
			_sketchCanvas.Clear(SKColors.Transparent);

			_sketchPaint = new SKPaint();
			_sketchPaint.StrokeWidth = 5;
			_sketchPaint.IsAntialias = true;
			_sketchPaint.Color = new SKColor(0, 255, 255, 255);// SKColors.Cyan;
			_sketchPaint.Style = SKPaintStyle.Stroke;

			_sketchText = new SKPaint();
			_sketchText.StrokeWidth = 1;
			_sketchText.IsAntialias = true;
			_sketchText.Color = new SKColor(255, 255, 0, 255);
			_sketchText.Style = SKPaintStyle.Fill;
			_sketchText.TextSize = 20;

			_sketchBlank = new SKPaint();
			_sketchBlank.Color = SKColors.Transparent;
			_sketchBlank.BlendMode = SKBlendMode.Clear;
			_sketchBlank.Style = SKPaintStyle.Fill;

			_sketchImg = new Image(this.Context);
			_sketchImg.SetSize(size.Width, size.Height, 4);

			_sketchTexture = new Urho2D.Texture2D(this.Context);
			_sketchTexture.SetNumLevels(1);
			var txtFormat = graphics.GetFormat(Urho.Resources.CompressedFormat.Rgba);
			_sketchTexture.SetSize(size.Width, size.Height, txtFormat, TextureUsage.Static);

			_skiaPtr0 = _skiaImage.GetAddress(0, 0);


			/// Create the Full-screen Button
			_sketchPad = new Button();
			_sketchPad.Texture = _sketchTexture; // cache.GetTexture2D("Textures/UrhoDecal.dds"); // Set texture
			_sketchPad.BlendMode = BlendMode.Alpha; // Add;
			_sketchPad.Opacity = 1f;
			_sketchPad.SetSize(size.Width, size.Height);
			_sketchPad.SetPosition(0, 71); // (graphics.Width - _sketchPad.Width) / 2, 200);
			_sketchPad.Name = "SketchPad";
			uiRoot.AddChild(_sketchPad);

			_sketchPad.DragBegin += SketchBegin;
			_sketchPad.DragMove += SketchDrag;
			_sketchPad.DragEnd += SketchEnd;
		}
		#endregion


		#region "-- event handlers --"
		private bool penIsDown = false;


		void SketchBegin(DragBeginEventArgs args)
		{
			// lock for consistent penIsDown sequence -
			// because SketchEnd must lock, due to its Draw.
			lock (_canvasLock)
			{
				_dragCount = 0;
				_downPos = new SKPoint(args.X, args.Y - _sketchPad.Position.Y);
				penIsDown = true;
			}
		}

		void SketchDrag(DragMoveEventArgs args)
		{
			_newPos = new SKPoint(args.X, args.Y - _sketchPad.Position.Y);
			_dragCount++;
		}
		private int _dragCount;

		void SketchEnd(DragEndEventArgs args) // For reference (not used here)
		{
			// TBD: Might be preferable to queue a command describing the line to be drawn,
			// rather than lock inside an event handler.
			lock (_canvasLock)
			{
				_DrawLineIfHaveMoved();
				// Inside the lock, to make sure it executes before next SketchStart has a chance to set penIsDown to true.
				penIsDown = false;
			}
		}
		#endregion


		#region "-- background work --"
		private void StartBackgroundTask()
		{
			if (_timer == null)
			{
				_timer = new Timer(1000.0 / DrawsPerSecond);
				_timer.Elapsed += BackgroundTimer_Elapsed;
				// true For a regular time interval, independent of how long each update takes.
				// false is better if task might consume the whole interval - to ensure other code gets to run.
				// (In the false case, "game time" must be monitored;
				//  some logic may depend on how much time actually passed since previous update.)
				_timer.AutoReset = ExactTimeInterval;
			}

			_timer.Start();
		}

		private void BackgroundTimer_Elapsed(object sender, ElapsedEventArgs e)
		{
			MakeChanges();

			// Alternative timer implementation. Time used above is not counted as part of the time interval;
			// Start timing again.
			if (!ExactTimeInterval)
				_timer.Start();
		}

		/// <summary>
		/// This code was extracted from OnUpdate.
		/// Now that it is done in background, lock is needed on canvas.
		/// </summary>
		private void MakeChanges()
		{
			// TMS TBD: In future, this might be a LENGTHY lock.
			// If this reduces frame rate too much, then need to
			// rework the changes, determine minimum set of changes to
			// result in a "consistent next frame".
			// First step is to isolate "static" (actually, infrequently changing) content
			// from "dynamic" (may change on every frame) content.
			// Scene object graph needs two layers, OR
			// each object needs a static/dynamic flag.
			// so can process the graph in two different states: one to update static layer, other for dynamic.
			lock (_canvasLock)
			{
				_DrawLineIfHaveMoved();
			}
		}

		/// <summary>
		/// TMS: This was in Update code. My example will be drawing in background.
		/// TBD: Add timer to move DrawLine to background, to show lock usage.
		/// </summary>
		private void _DrawLineIfHaveMoved()
		{
			if (penIsDown)
			{
				// Cache to lessen chance of values changing in middle of this operation.
				var start = _downPos;
				var end = _newPos;

				var delta = end - start;
				if (delta.LengthSquared > 1)
				{
					_sketchCanvas.DrawLine(start, end, _sketchPaint);
					_sketchCanvas.Flush();
					// This was "_newPos", but interrupt could cause "gap".
					_downPos = end;
					_isSketchDirty = true;
					_dirtyCount++;

					_sketchCanvas.DrawRect(new SKRect(0, 0, 150, 60), _sketchBlank);
					_sketchCanvas.DrawText("Dirty: " + _dirtyCount.ToString(), 10, 15, _sketchText);
					_sketchCanvas.DrawText("Blit:  " + _uiBlitCount.ToString(), 10, 45, _sketchText);
				}
			}
		}

		private bool _isSketchDirty = false;
		private int _dirtyCount = 0;
		private int _uiBlitCount = 0;

		#endregion


		#region "-- make results visible --"
		protected override void OnUpdate(float timeStep)
		{
			base.OnUpdate(timeStep);

			_sketchOpacity += (timeStep * _sketchOpacityIncr);
			if (_sketchOpacity >= 1f)
			{
				_sketchOpacity = 1f;
				_sketchOpacityIncr = -_sketchOpacityIncr;
			}
			else if (_sketchOpacity <= 0.75f)
			{
				_sketchOpacity = 0.75f;
				_sketchOpacityIncr = -_sketchOpacityIncr;
			}
			_sketchPad.Opacity = _sketchOpacity;

			if (!MakeChangesInBackground)
				MakeChanges();

			lock (_canvasLock)
			{
				if (_isSketchDirty)
				{
					_isSketchDirty = false;
					_uiBlitCount++;
					_UpdateSketch();
				}
			}
			if (windowTitle == null)
				windowTitle = window.GetChild("WindowTitle", true) as Text;

			windowTitle.Value = "DRAG#" + _dragCount;

			_UpdateScene(timeStep);
		}

		private float _sketchOpacity = 1f;
		private float _sketchOpacityIncr = 1f;

		private unsafe void _UpdateSketch()
		{
			try
			{
				//_sketchImg.SetData((byte*)_skiaPtr0);
				//_sketchTexture.SetData(_sketchImg, true);
				_sketchTexture.SetData(0, 0, 0, _sketchTexture.Width, _sketchTexture.Height, (void*)_skiaPtr0);
			}
			catch (System.Exception ex)
			{
				var msg = ex.Message;
			}
		}
		#endregion

	}
}
