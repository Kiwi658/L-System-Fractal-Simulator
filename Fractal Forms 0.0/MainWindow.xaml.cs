using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

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
        private Button clearButton;
        private Button animateButton;
        private CheckBox stochasticCheckBox;

        // L-System parameters
        private string axiom = "X";
        private Dictionary<char, List<(string, double)>> rules = new Dictionary<char, List<(string, double)>>();
        private double angle = 25;
        private int iterations = 5;
        private Point startPoint = new Point(400, 600);
        private double length = 10;
        private double lengthScale = 0.5;

        private Stack<(Point, double)> stack = new Stack<(Point, double)>();
        private Point currentPosition;
        private double currentAngle;

        public MainWindow()
        {
            InitializeComponent();
            SetupUI();
            SetupLSystem();
        }

        private void SetupUI()
        {
            this.Title = "L-System Fractal Simulator";
            this.Width = 1000;
            this.Height = 850;
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;

            Grid mainGrid = new Grid();
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // Панель управления
            StackPanel controlsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(10),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(controlsPanel, 0);

            // Создаём поля с метками
            var iterationsPanel = CreateLabeledTextBox("Iterations:", "5");
            var anglePanel = CreateLabeledTextBox("Angle (deg):", "25");
            var startXPanel = CreateLabeledTextBox("Start X:", "400");
            var startYPanel = CreateLabeledTextBox("Start Y:", "600");

            // Сохраняем ссылки на TextBox
            iterationsTextBox = (TextBox)iterationsPanel.Children[1];
            angleTextBox = (TextBox)anglePanel.Children[1];
            startXTextBox = (TextBox)startXPanel.Children[1];
            startYTextBox = (TextBox)startYPanel.Children[1];

            stochasticCheckBox = new CheckBox
            {
                Content = "Stochastic mode",
                IsChecked = true,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(15, 0, 15, 0)
            };

            drawButton = new Button { Content = "Draw", Width = 90, Margin = new Thickness(5) };
            drawButton.Click += DrawButton_Click;

            animateButton = new Button { Content = "Animate", Width = 90, Margin = new Thickness(5) };
            animateButton.Click += AnimateButton_Click;

            clearButton = new Button { Content = "Clear", Width = 90, Margin = new Thickness(5) };
            clearButton.Click += ClearButton_Click;

            // Добавляем всё в панель управления
            controlsPanel.Children.Add(iterationsPanel);
            controlsPanel.Children.Add(anglePanel);
            controlsPanel.Children.Add(startXPanel);
            controlsPanel.Children.Add(startYPanel);
            controlsPanel.Children.Add(stochasticCheckBox);
            controlsPanel.Children.Add(drawButton);
            controlsPanel.Children.Add(animateButton);
            controlsPanel.Children.Add(clearButton);

            // Холст для рисования
            drawingCanvas = new Canvas
            {
                Background = Brushes.WhiteSmoke,
                ClipToBounds = true
            };
            Grid.SetRow(drawingCanvas, 1);

            mainGrid.Children.Add(controlsPanel);
            mainGrid.Children.Add(drawingCanvas);

            this.Content = mainGrid;
        }

        private StackPanel CreateLabeledTextBox(string labelText, string defaultValue)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(8, 0, 8, 0),
                VerticalAlignment = VerticalAlignment.Center
            };

            var label = new Label
            {
                Content = labelText,
                VerticalContentAlignment = VerticalAlignment.Center,
                Padding = new Thickness(0, 0, 4, 0)
            };

            var textBox = new TextBox
            {
                Text = defaultValue,
                Width = 60,
                VerticalContentAlignment = VerticalAlignment.Center
            };

            panel.Children.Add(label);
            panel.Children.Add(textBox);

            return panel;
        }

        private void SetupLSystem()
        {
            rules.Clear();

            // Пример стохастической системы (кустарник/дерево)
            rules['X'] = new List<(string, double)>
            {
                ("F[+X][-X]FX", 0.55),
                ("F[+X]F[-X]+X", 0.30),
                ("FF[+X][-X]X",  0.15)
            };

            rules['F'] = new List<(string, double)>
            {
                ("FF", 0.70),
                ("F",  0.30)
            };
        }

        private string GenerateLSystem(int n)
        {
            string current = axiom;
            var rand = new Random();

            for (int i = 0; i < n; i++)
            {
                string next = "";
                foreach (char c in current)
                {
                    if (rules.TryGetValue(c, out var ruleList))
                    {
                        double p = rand.NextDouble();
                        double sum = 0;

                        foreach (var (replacement, prob) in ruleList)
                        {
                            sum += prob;
                            if (p <= sum)
                            {
                                next += replacement;
                                break;
                            }
                        }
                    }
                    else
                    {
                        next += c;
                    }
                }
                current = next;
            }
            return current;
        }

        private void DrawLSystem(string lsystem, bool animate = false)
        {
            drawingCanvas.Children.Clear();
            stack.Clear();

            currentPosition = startPoint;
            currentAngle = -90;           // вверх (в WPF Y растёт вниз)
            double stepLength = length;

            var rand = new Random();

            foreach (char c in lsystem)
            {
                // Небольшая случайность длины шага при стохастике
                double currentStep = stepLength;
                if (stochasticCheckBox.IsChecked == true)
                {
                    currentStep *= 0.85 + rand.NextDouble() * 0.3; // 85%–115%
                }

                switch (c)
                {
                    case 'F':
                        var newPos = CalculateNewPosition(currentPosition, currentStep, currentAngle);
                        var line = new Line
                        {
                            X1 = currentPosition.X,
                            Y1 = currentPosition.Y,
                            X2 = newPos.X,
                            Y2 = newPos.Y,
                            Stroke = Brushes.DarkGreen,
                            StrokeThickness = 1.8 + (rand.NextDouble() - 0.5) * 0.6
                        };
                        drawingCanvas.Children.Add(line);
                        currentPosition = newPos;
                        break;

                    case '+':
                        currentAngle += angle;
                        break;

                    case '-':
                        currentAngle -= angle;
                        break;

                    case '[':
                        stack.Push((currentPosition, currentAngle));
                        break;

                    case ']':
                        if (stack.Count > 0)
                            (currentPosition, currentAngle) = stack.Pop();
                        break;
                }
            }

            // Пока без настоящей анимации — можно доработать позже
            if (animate)
            {
                // Здесь можно реализовать поэтапное рисование (DispatcherTimer)
            }
        }

        private Point CalculateNewPosition(Point pos, double len, double angDeg)
        {
            double rad = angDeg * Math.PI / 180.0;
            return new Point(
                pos.X + len * Math.Cos(rad),
                pos.Y + len * Math.Sin(rad)
            );
        }

        private void UpdateParameters()
        {
            int.TryParse(iterationsTextBox.Text, out iterations);
            double.TryParse(angleTextBox.Text, out angle);
            double.TryParse(startXTextBox.Text, out double sx);
            double.TryParse(startYTextBox.Text, out double sy);

            startPoint = new Point(sx, sy);

            // Защита от слишком больших значений
            iterations = Math.Clamp(iterations, 1, 9);
        }

        private void DrawButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateParameters();
            string lstring = GenerateLSystem(iterations);
            DrawLSystem(lstring);
        }

        private void AnimateButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("Пока реализована только мгновенная отрисовка.\nАнимация по шагам — в планах на доработку.");
            DrawButton_Click(sender, e);
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            drawingCanvas.Children.Clear();
        }

        // Заглушка для InitializeComponent (если нет XAML)
        /*
        private void InitializeComponent()
        {
            // Если используете чистый код — оставляем пустым
        }
        */
    }
}