using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace LSystemFractal
{
    public partial class MainWindow : Window
    {
        private Canvas drawingCanvas;
        private TextBox iterationsTextBox;
        private TextBox angleTextBox;
        private TextBox startXTextBox;
        private TextBox startYTextBox;
        private Button drawButton;
        private Button animateButton;
        private Button clearButton;
        private Button resetViewButton;
        private CheckBox stochasticCheckBox;

        // L-System
        private string axiom = "X";
        private Dictionary<char, List<(string, double)>> rules = new();
        private double angleDeg = 25;
        private int iterations = 5;
        private Point startPoint = new Point(400, 600);
        private double baseLength = 10;
        private double lengthVariation = 0.5;
        private double minX = double.MaxValue, minY = double.MaxValue;
        private double maxX = double.MinValue, maxY = double.MinValue;
        private const double PaddingFactor = 0.98;

        // Turtle state
        private Stack<(Point pos, double angle)> turtleStack = new();

        // Анимация
        private DispatcherTimer animationTimer;
        private string currentLString = "";
        private int drawIndex = 0;
        private Point currentPos;
        private double currentAngle;
        private double currentStepLength;
        private Random rand = new Random();

        // Zoom & Pan
        private ScaleTransform scaleTransform = new ScaleTransform(1, 1);
        private TranslateTransform translateTransform = new TranslateTransform(0, 0);
        private TransformGroup transformGroup = new TransformGroup();
        private Point? dragStart;
        private const double ZoomFactor = 1.15;

        public MainWindow()
        {
            InitializeComponent();
            SetupUI();
            SetupLSystem();

            // Подготовка трансформаций для zoom/pan
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(translateTransform);
            drawingCanvas.RenderTransform = transformGroup;

            // Таймер для анимации
            animationTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(8) };
            animationTimer.Tick += AnimationTimer_Tick;
        }

        private void UpdateBounds(Point p)
        {
            minX = Math.Min(minX, p.X);
            minY = Math.Min(minY, p.Y);
            maxX = Math.Max(maxX, p.X);
            maxY = Math.Max(maxY, p.Y);
        }

        private void FitToCanvas()
        {
            if (double.IsInfinity(minX) || double.IsNaN(minX) ||
                maxX <= minX + 1e-6 || maxY <= minY + 1e-6)
            {
                scaleTransform.ScaleX = scaleTransform.ScaleY = 1.0;
                translateTransform.X = translateTransform.Y = 0;
                return;
            }

            double fractalWidth = maxX - minX;
            double fractalHeight = maxY - minY;

            double canvasWidth = drawingCanvas.ActualWidth > 10 ? drawingCanvas.ActualWidth : 800;
            double canvasHeight = drawingCanvas.ActualHeight > 10 ? drawingCanvas.ActualHeight : 600;

            double scaleX = canvasWidth / fractalWidth * PaddingFactor;
            double scaleY = canvasHeight / fractalHeight * PaddingFactor;
            double newScale = Math.Min(scaleX, scaleY);

            newScale = Math.Clamp(newScale, 0.05, 50.0);

            scaleTransform.ScaleX = scaleTransform.ScaleY = newScale;

            double offsetX = (canvasWidth - fractalWidth * newScale) / 2 - minX * newScale;
            double offsetY = (canvasHeight - fractalHeight * newScale) / 2 - minY * newScale;

            translateTransform.X = offsetX;
            translateTransform.Y = offsetY;
        }

        private void SetupUI()
        {
            this.Title = "L-System Fractal Simulator";
            this.Width = 1400;
            this.Height = 1000;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            var mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var controls = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(controls, 0);

            var fitButton = new Button { Content = "Fit", Width = 90, Margin = new Thickness(5) };
            fitButton.Click += (s, e) => FitToCanvas();
            controls.Children.Add(fitButton);

            var iterPanel = CreateLabeledTextBox("Iterations:", "5");
            var anglePanel = CreateLabeledTextBox("Angle:", "25");
            var xPanel = CreateLabeledTextBox("Start X:", "400");
            var yPanel = CreateLabeledTextBox("Start Y:", "600");

            iterationsTextBox = (TextBox)iterPanel.Children[1];
            angleTextBox = (TextBox)anglePanel.Children[1];
            startXTextBox = (TextBox)xPanel.Children[1];
            startYTextBox = (TextBox)yPanel.Children[1];

            stochasticCheckBox = new CheckBox
            {
                Content = "Stochastic",
                IsChecked = true,
                Margin = new Thickness(15, 0, 15, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            drawButton = new Button { Content = "Draw", Width = 90, Margin = new Thickness(5) };
            animateButton = new Button { Content = "Animate", Width = 90, Margin = new Thickness(5) };
            clearButton = new Button { Content = "Clear", Width = 90, Margin = new Thickness(5) };
            resetViewButton = new Button { Content = "Reset view", Width = 90, Margin = new Thickness(5) };

            resetViewButton.Click += ResetViewButton_Click;
            drawButton.Click += DrawButton_Click;
            animateButton.Click += AnimateButton_Click;
            clearButton.Click += ClearButton_Click;

            controls.Children.Add(resetViewButton);
            controls.Children.Add(iterPanel);
            controls.Children.Add(anglePanel);
            controls.Children.Add(xPanel);
            controls.Children.Add(yPanel);
            controls.Children.Add(stochasticCheckBox);
            controls.Children.Add(drawButton);
            controls.Children.Add(animateButton);
            controls.Children.Add(clearButton);

            drawingCanvas = new Canvas
            {
                Background = Brushes.WhiteSmoke,
                ClipToBounds = true
            };
            drawingCanvas.MouseWheel += DrawingCanvas_MouseWheel;
            drawingCanvas.MouseLeftButtonDown += DrawingCanvas_MouseLeftButtonDown;
            drawingCanvas.MouseMove += DrawingCanvas_MouseMove;
            drawingCanvas.MouseLeftButtonUp += DrawingCanvas_MouseLeftButtonUp;

            Grid.SetRow(drawingCanvas, 1);
            mainGrid.Children.Add(controls);
            mainGrid.Children.Add(drawingCanvas);

            this.Content = mainGrid;
        }

        private StackPanel CreateLabeledTextBox(string label, string defValue)
        {
            var p = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(8, 0, 8, 0) };
            p.Children.Add(new Label { Content = label, Padding = new Thickness(0, 0, 4, 0) });
            p.Children.Add(new TextBox { Text = defValue, Width = 60 });
            return p;
        }

        private void SetupLSystem()
        {
            rules.Clear();
            rules['X'] = new List<(string, double)>
            {
                ("F[+X][-X]FX", 0.50),
                ("F[+X]F[-X]+X", 0.35),
                ("FF[+X][-X]X",  0.15)
            };
            rules['F'] = new List<(string, double)>
            {
                ("FF", 0.75),
                ("F",  0.25)
            };
        }

        private string GenerateLSystem(int n)
        {
            string s = axiom;
            for (int i = 0; i < n; i++)
            {
                var next = "";
                foreach (char c in s)
                {
                    if (rules.TryGetValue(c, out var opts))
                    {
                        double p = rand.NextDouble();
                        double sum = 0;
                        foreach (var (rep, prob) in opts)
                        {
                            sum += prob;
                            if (p <= sum) { next += rep; break; }
                        }
                    }
                    else next += c;
                }
                s = next;
            }
            return s;
        }

        private void DrawButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateParams();
            currentLString = GenerateLSystem(iterations);
            DrawLSystem(currentLString, animate: false);
        }

        private void AnimateButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateParams();
            currentLString = GenerateLSystem(iterations);
            StartAnimation();
        }

        private void StartAnimation()
        {
            drawingCanvas.Children.Clear();
            turtleStack.Clear();

            minX = minY = double.MaxValue;
            maxX = maxY = double.MinValue;

            currentPos = startPoint;
            currentAngle = -90;
            drawIndex = 0;

            UpdateBounds(currentPos);

            animationTimer.Start();
        }

        private void AnimationTimer_Tick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(currentLString) || drawIndex >= currentLString.Length)
            {
                animationTimer.Stop();
                // FitToCanvas();
                return;
            }

            char cmd = currentLString[drawIndex];
            drawIndex++;

            double step = baseLength;
            if (stochasticCheckBox.IsChecked == true)
                step *= 0.8 + rand.NextDouble() * 0.4;

            switch (cmd)
            {
                case 'F':
                    var nextPos = CalcPos(currentPos, step, currentAngle);

                    var line = new Line
                    {
                        X1 = currentPos.X,
                        Y1 = currentPos.Y,
                        X2 = nextPos.X,
                        Y2 = nextPos.Y,
                        Stroke = Brushes.DarkGreen,
                        StrokeThickness = 0.9 + rand.NextDouble() * 0.5
                    };
                    drawingCanvas.Children.Add(line);

                    currentPos = nextPos;
                    UpdateBounds(currentPos);
                    break;

                case '+':
                    currentAngle += angleDeg;
                    break;

                case '-':
                    currentAngle -= angleDeg;
                    break;

                case '[':
                    turtleStack.Push((currentPos, currentAngle));
                    break;

                case ']':
                    if (turtleStack.Count > 0)
                    {
                        var (pos, ang) = turtleStack.Pop();
                        currentPos = pos;
                        currentAngle = ang;
                        UpdateBounds(currentPos); 
                    }
                    break;
            }
        }

        private void DrawLSystem(string lstring, bool animate)
        {
            drawingCanvas.Children.Clear();

            if (animate)
            {
                currentLString = lstring;
                StartAnimation();
                return;
            }

            turtleStack.Clear();
            currentPos = startPoint;
            currentAngle = -90;

            // Сброс bounding box для мгновенного режима
            minX = minY = double.MaxValue;
            maxX = maxY = double.MinValue;
            UpdateBounds(startPoint);

            double step = baseLength;

            foreach (char c in lstring)
            {
                if (stochasticCheckBox.IsChecked == true)
                    step = baseLength * (0.8 + rand.NextDouble() * 0.4);

                switch (c)
                {
                    case 'F':
                        var np = CalcPos(currentPos, step, currentAngle);
                        drawingCanvas.Children.Add(new Line
                        {
                            X1 = currentPos.X,
                            Y1 = currentPos.Y,
                            X2 = np.X,
                            Y2 = np.Y,
                            Stroke = Brushes.DarkGreen,
                            StrokeThickness = 1.0
                        });
                        currentPos = np;
                        UpdateBounds(currentPos);
                        break;

                    case '+': currentAngle += angleDeg; break;
                    case '-': currentAngle -= angleDeg; break;
                    case '[': turtleStack.Push((currentPos, currentAngle)); break;
                    case ']':
                        if (turtleStack.Count > 0)
                        {
                            var (pos, ang) = turtleStack.Pop();
                            currentPos = pos;
                            currentAngle = ang;
                            UpdateBounds(currentPos);
                        }
                        break;
                }
            }

            // FitToCanvas();
        }

        private Point CalcPos(Point p, double len, double ang)
        {
            double r = ang * Math.PI / 180;
            return new Point(p.X + len * Math.Cos(r), p.Y + len * Math.Sin(r));
        }

        private void UpdateParams()
        {
            int.TryParse(iterationsTextBox.Text, out iterations);
            double.TryParse(angleTextBox.Text, out angleDeg);
            double.TryParse(startXTextBox.Text, out double sx);
            double.TryParse(startYTextBox.Text, out double sy);
            startPoint = new Point(sx, sy);

            baseLength = 10.0 / Math.Pow(1.35, iterations - 1); 

            iterations = Math.Clamp(iterations, 1, 10);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            drawingCanvas.Children.Clear();
            animationTimer.Stop();
            scaleTransform.ScaleX = scaleTransform.ScaleY = 1.0;
            translateTransform.X = translateTransform.Y = 0;
        }

        // ────────────────────────────── Zoom & Pan ──────────────────────────────

        private void DrawingCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (Keyboard.Modifiers != ModifierKeys.None) return;

            Point mousePos = e.GetPosition(drawingCanvas);

            double oldScale = scaleTransform.ScaleX;
            double newScale = e.Delta > 0 ? oldScale * ZoomFactor : oldScale / ZoomFactor;

            double deltaScale = newScale / oldScale;
            double offsetX = mousePos.X * (1 - deltaScale);
            double offsetY = mousePos.Y * (1 - deltaScale);

            translateTransform.X = translateTransform.X * deltaScale + offsetX;
            translateTransform.Y = translateTransform.Y * deltaScale + offsetY;

            scaleTransform.ScaleX = newScale;
            scaleTransform.ScaleY = newScale;

            newScale = Math.Clamp(newScale, 0.5, 20.0); 

            e.Handled = true;
        }

        private void DrawingCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (Keyboard.Modifiers == ModifierKeys.Control)
            {
                dragStart = e.GetPosition(this);
                drawingCanvas.CaptureMouse();
                e.Handled = true;
            }
        }

        private void DrawingCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            if (dragStart.HasValue)
            {
                Point current = e.GetPosition(this);
                Vector delta = current - dragStart.Value;

                translateTransform.X += delta.X;
                translateTransform.Y += delta.Y;

                dragStart = current;
                e.Handled = true;
            }
        }

        private void DrawingCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (dragStart.HasValue)
            {
                drawingCanvas.ReleaseMouseCapture();
                dragStart = null;
                e.Handled = true;
            }
        }

        private void ResetViewButton_Click(object sender, RoutedEventArgs e)
        {
            // Сброс масштаба
            scaleTransform.ScaleX = 1.0;
            scaleTransform.ScaleY = 1.0;

            // Сброс смещения
            translateTransform.X = 0;
            translateTransform.Y = 0;

        }
    }
}