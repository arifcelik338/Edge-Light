using System.Drawing;
using System.Drawing.Drawing2D;
using Microsoft.Win32;
using System.Runtime.InteropServices;
namespace Edge_Light
{
    public partial class Form1 : Form
    {
        // --- 1. SETTINGS AND VARIABLES ---
        private int lightThickness = 60;      // Border thickness (controlled by slider)
        private Color lightColor = Color.White; // Current color of the edge light
        private int cornerRadius = 40;        // Level of corner roundness
        private int edgeMargin = 55;          // Margin from the screen boundaries
        private bool isLightVisible = true;   // Variable for toggle hide/show logic

        // --- 2. SYSTEM AND PERFORMANCE CONSTANTS ---
        private const int WS_EX_TRANSPARENT = 0x20; // Pass-through click code
        private const int WS_EX_LAYERED = 0x80000;    // Layered window support
        private const int WS_EX_TOOLWINDOW = 0x80;   // Hide from Alt+Tab menu
        NotifyIcon icon = new NotifyIcon();         // Create icon
                                                   // Context Men Design
        ContextMenuStrip contextMenu = new ContextMenuStrip();


        private Bitmap bufferImage;                  // Image stored in RAM to prevent CPU flickering
        private ColorDialog colorDialog = new ColorDialog(); // Standard dialog that remembers custom colors

        public Form1()
        {
            InitializeComponent();

            this.ShowInTaskbar = false;

            // --- 1. UPLOAD ICON ---
            string ikonDosyasi = "logo.ico";

            if (System.IO.File.Exists(ikonDosyasi))
            {
                // Select form icon
                this.Icon = new Icon(ikonDosyasi);
            }
            icon.Visible = true;
            icon.Text = "Edge Light";
            icon.ContextMenuStrip = contextMenu;

            // --- 2. SYSTEM TRAY SETTINGS ---

            // if icon loading is succesfuly use icon else icon not found use windwos form icon
            icon.Icon = (this.Icon != null) ? this.Icon : SystemIcons.Application;

            icon.Visible = true;
            icon.Text = "Edge Light";
            // --- PERFORMANCE OPTIMIZATION ---
            this.DoubleBuffered = true; // Renders in memory before painting to screen
            this.SetStyle(ControlStyles.OptimizedDoubleBuffer | ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint, true);
            this.UpdateStyles();

            // --- BASE FORM CONFIGURATION ---
            this.WindowState = FormWindowState.Maximized; // Cover the whole screen
            this.FormBorderStyle = FormBorderStyle.None; // Remove borders
            this.TopMost = true;                         // Stay above all other windows
            this.BackColor = Color.Magenta;               // Chroma key for transparency
            this.TransparencyKey = Color.Magenta;        // Every magenta pixel becomes transparent
            this.ShowInTaskbar = false;                  // Hide from the taskbar

            // A) COLOR WHEEL
            contextMenu.Items.Add(new ToolStripMenuItem("Color Picker:"));
            CustomColorWheel colorWheel = new CustomColorWheel();
            colorWheel.ColorSelected += (s, selectedColor) =>
            {
                lightColor = selectedColor;
                CaptureToBuffer(); // Update the RAM buffer with new color
                this.Refresh();     // Force UI refresh
            };
            contextMenu.Items.Add(new ToolStripControlHost(colorWheel));
            contextMenu.Items.Add(new ToolStripSeparator());

            // B) QUICK COLORS
            ToolStripMenuItem quickColorsMenu = new ToolStripMenuItem("Quick Colors");

            Color[] palette = new Color[] {
                Color.White, Color.Silver, Color.Gray, Color.Black,
                Color.Red, Color.Maroon, Color.Yellow, Color.Olive,
                Color.Lime, Color.Green, Color.Aqua, Color.Teal,
                Color.Blue, Color.Navy, Color.Pink, Color.Purple,
                Color.Orange, Color.Gold, Color.DeepPink,
                Color.Brown, Color.SandyBrown, Color.Turquoise, Color.RoyalBlue
            };

            foreach (var color in palette)
            {
                // Create small circular icons for the menu
                Bitmap icon = new Bitmap(16, 16);
                using (Graphics g = Graphics.FromImage(icon))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    using (SolidBrush brush = new SolidBrush(color)) g.FillEllipse(brush, 0, 0, 15, 15);
                }

                ToolStripMenuItem colorItem = new ToolStripMenuItem("", icon);
                colorItem.Click += (s, e) => { lightColor = color; CaptureToBuffer(); this.Refresh(); };
                quickColorsMenu.DropDownItems.Add(colorItem);
            }

            contextMenu.Items.Add(quickColorsMenu);

            // C) THICKNESS AND MARGIN SLIDERS
            contextMenu.Items.Add(new ToolStripMenuItem("Light Thickness:"));
            CustomSlider thicknessSlider = new CustomSlider { Minimum = 5, Maximum = 150, Value = lightThickness, Width = 150 };
            thicknessSlider.ValueChanged += (s, e) => { lightThickness = thicknessSlider.Value; CaptureToBuffer(); this.Refresh(); };
            contextMenu.Items.Add(new ToolStripControlHost(thicknessSlider));

            contextMenu.Items.Add(new ToolStripMenuItem("Edge Margin:"));
            CustomSlider marginSlider = new CustomSlider { Minimum = 0, Maximum = 150, Value = edgeMargin, Width = 150 };
            marginSlider.ValueChanged += (s, e) => { edgeMargin = marginSlider.Value; CaptureToBuffer(); this.Refresh(); };
            contextMenu.Items.Add(new ToolStripControlHost(marginSlider));

            // D) TOGGLE VISIBILITY AND EXIT
            contextMenu.Items.Add(new ToolStripSeparator());

            ToolStripMenuItem toggleVisibility = new ToolStripMenuItem("Show/Hide Light");
            toggleVisibility.Click += (s, e) =>
            {
                if (isLightVisible)
                {
                    this.Opacity = 0;
                    isLightVisible = false;
                }
                else
                {
                    this.Opacity = 1;
                    isLightVisible = true;
                }
            };
            contextMenu.Items.Add(toggleVisibility);

            ToolStripMenuItem exitButton = new ToolStripMenuItem("Exit Application");
            exitButton.Click += (s, e) => Application.Exit();
            contextMenu.Items.Add(exitButton);
        }

        // --- DRAWING ENGINE ---
        private void CaptureToBuffer()
        {
            // Re-create the bitmap if size changes or it's null
            if (bufferImage == null || bufferImage.Width != this.Width)
            {
                if (bufferImage != null) bufferImage.Dispose();
                bufferImage = new Bitmap(this.Width, this.Height);
            }

            using (Graphics g = Graphics.FromImage(bufferImage))
            {
                g.Clear(Color.Magenta); // Clear background with chroma key
                g.SmoothingMode = SmoothingMode.AntiAlias;

                using (Pen pen = new Pen(lightColor, lightThickness))
                {
                    int halfThickness = lightThickness / 2;
                    int x = edgeMargin + halfThickness;
                    int y = edgeMargin + halfThickness;
                    int w = this.Width - (lightThickness + (edgeMargin * 2));
                    int h = this.Height - (lightThickness + (edgeMargin * 2));

                    if (w > 0 && h > 0)
                    {
                        Rectangle rect = new Rectangle(x, y, w, h);
                        using (GraphicsPath path = CreateRoundedRectangle(rect, cornerRadius))
                        {
                            g.DrawPath(pen, path); // Draw the light onto the buffer
                        }
                    }
                }
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (bufferImage == null) CaptureToBuffer();
            e.Graphics.DrawImage(bufferImage, 0, 0); // Fast render from RAM
        }

        private GraphicsPath CreateRoundedRectangle(Rectangle r, int radius)
        {
            int diameter = radius * 2;
            GraphicsPath path = new GraphicsPath();
            path.AddArc(r.X, r.Y, diameter, diameter, 180, 90);
            path.AddArc(r.Right - diameter, r.Y, diameter, diameter, 270, 90);
            path.AddArc(r.Right - diameter, r.Bottom - diameter, diameter, diameter, 0, 90);
            path.AddArc(r.X, r.Bottom - diameter, diameter, diameter, 90, 90);
            path.CloseFigure();
            return path;
        }

        // Passes the click events through the window to applications below
        protected override CreateParams CreateParams
        {
            get
            {
                CreateParams cp = base.CreateParams;
                cp.ExStyle |= WS_EX_LAYERED | WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW;
                return cp;
            }
        }

        // --- CUSTOM MODERN SLIDER CONTROL ---
        public class CustomSlider : Control
        {
            public int Minimum = 0, Maximum = 100, Value = 50;
            public event EventHandler ValueChanged;
            public CustomSlider() { this.DoubleBuffered = true; this.Height = 30; this.Cursor = Cursors.Hand; }

            protected override void OnPaint(PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                int cy = this.Height / 2 - 2;
                e.Graphics.FillRectangle(Brushes.LightGray, 10, cy, this.Width - 20, 4);

                float ratio = (float)(Value - Minimum) / (Maximum - Minimum);
                int filledWidth = (int)((this.Width - 20) * ratio);

                Color accentColor = Color.DodgerBlue;
                try
                {
                    int c = (int)Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "ColorizationColor", 0);
                    accentColor = Color.FromArgb(255, Color.FromArgb(c));
                }
                catch { }

                using (SolidBrush brush = new SolidBrush(accentColor))
                {
                    e.Graphics.FillRectangle(brush, 10, cy, filledWidth, 4);
                    e.Graphics.FillEllipse(Brushes.White, 10 + filledWidth - 8, this.Height / 2 - 8, 16, 16);
                }
            }
            protected override void OnMouseDown(MouseEventArgs e)
            {
                Value = Minimum + (int)((Maximum - Minimum) * Math.Max(0, Math.Min(1, (float)(e.X - 10) / (this.Width - 20))));
                ValueChanged?.Invoke(this, EventArgs.Empty);
                this.Invalidate();
            }
            protected override void OnMouseMove(MouseEventArgs e) { if (e.Button == MouseButtons.Left) OnMouseDown(e); }
        }

        // --- CUSTOM COLOR WHEEL CONTROL ---
        public class CustomColorWheel : Control
        {
            public event EventHandler<Color> ColorSelected;
            private Bitmap wheelBitmap;
            public CustomColorWheel() { this.Size = new Size(150, 150); this.DoubleBuffered = true; DrawWheel(); }
            private void DrawWheel()
            {
                wheelBitmap = new Bitmap(150, 150);
                using (Graphics g = Graphics.FromImage(wheelBitmap))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    for (int i = 0; i < 360; i++)
                    {
                        using (Pen p = new Pen(HsvToRgb(i, 1, 1), 3))
                        {
                            double rad = i * Math.PI / 180;
                            g.DrawLine(p, 75, 75, 75 + (float)(Math.Cos(rad) * 70), 75 + (float)(Math.Sin(rad) * 70));
                        }
                    }
                }
            }
            private Color HsvToRgb(float h, float s, float v)
            {
                float pos = h / 60f;
                int x = (int)(255 * (1 - Math.Abs(pos % 2 - 1)));
                if (pos < 1) return Color.FromArgb(255, x, 0);
                if (pos < 2) return Color.FromArgb(x, 255, 0);
                if (pos < 3) return Color.FromArgb(0, 255, x);
                if (pos < 4) return Color.FromArgb(0, x, 255);
                if (pos < 5) return Color.FromArgb(x, 0, 255);
                return Color.FromArgb(255, 0, x);
            }
            protected override void OnPaint(PaintEventArgs e) { e.Graphics.DrawImage(wheelBitmap, 0, 0); }
            protected override void OnMouseDown(MouseEventArgs e)
            {
                double dx = e.X - 75, dy = e.Y - 75;
                float ang = (float)(Math.Atan2(dy, dx) * 180 / Math.PI);
                if (ang < 0) ang += 360;
                ColorSelected?.Invoke(this, HsvToRgb(ang, 1, 1));
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }
    }
}