﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using IsingModern.ViewModel;
using OxyPlot;
using OxyPlot.Annotations;
using LineAnnotation = OxyPlot.Wpf.LineAnnotation;


namespace IsingModern.ViewPages {
    /// <summary>
    /// Interaction logic for LatticeOutput.xaml
    /// </summary>

    public partial class IsingRender : UserControl {
        public static IsingRender Current;
        private IsingRenderModel _viewmodel;

        private bool _periodicBoundary = true;
        private bool _ferromagnetic = true;
        private bool _singleFlip = true;
        private const double _tempMax = 5.0;
        private const double _magnMax = 0.5;

        private int sliderMin = 0, sliderMax = 2;

        public int SliderMax {
            get { return sliderMax; }
            set {
                sliderMax = value;
                SizeSlider.Maximum = sliderMax;
                SizeSlider.Value = Math.Min(SizeSlider.Value, value);
                ThreadedAction(ScaleLattice);
            }
        }


        private int CurrentN {
            get { return 25 * (1 << (int)SizeSlider.Value); }
        }
        private const int MaximalN = 200, MinimalN = 25; //both should divide Pixels. 
        public const int Pixels = 800;



        #region Initialization

        public IsingRender() {
            InitializeComponent();
            _viewmodel = new IsingRenderModel(CurrentN, _periodicBoundary);
            Plotinit(); //test
            Current = this;
            ModelParentElement.Children.Add(_viewmodel);
            LatticeSize.Content = "Size: " + CurrentN.ToString();
            SizeSlider.Maximum = SliderMax;
            Reset();

            _worker = worker_Init();
            StatusText.Text = "0";
            _worker.RunWorkerAsync();
            run.WaitOne();
        }

        #endregion

        #region Lattice Manipulation
        private void maingrid_KeyDown(object sender, KeyEventArgs e) {
            if(e.Key == Key.N) {
                Start_Click(null, null);
                e.Handled = true;
            }
        }


        private void RandomizeClick(object sender, RoutedEventArgs e) {
            ThreadedAction(RandomizeLattice);
            e.Handled = true;
        }

        private void Reset_Click(object sender, RoutedEventArgs e) {
            Reset();
        }

        private void ToggleBoundary_Click(object sender = null, RoutedEventArgs e = null) {
            if(sender != null) _periodicBoundary = !_periodicBoundary;
            ThreadedAction(Boundary);
        }

        private void Coupling_Click(object sender = null, RoutedEventArgs e = null) {
            if(sender != null) _ferromagnetic = !_ferromagnetic;
            ChangeCoupling();
            if(e != null) e.Handled = true;
        }

        private void Algorithm_Click(object sender, RoutedEventArgs e) {
            if(sender != null) _singleFlip = !_singleFlip;
            ChangeAlgorithm();
            e.Handled = true;
        }

        #endregion

        #region Temperature & Magnetization
        private const double SnappingTolerance = 0.04;
        private const double TempMax = 5, FieldMax = 0.5;
        private const double ThumbRadius = 5;

        private double magneticfield = 0.0;
        private double temperature = 1.0;
        private void UpdateParameters(double x, double y) {
            var temp = (x + ThumbRadius) / (TempMagField.ActualWidth) * TempMax;
            var field = FieldMax - (y + ThumbRadius) / (TempMagField.ActualHeight) * (2 * FieldMax);
            if(_snapping && Math.Abs(field) < SnappingTolerance) { //snapping to 0 field
                field = 0;
                UpdateThumb(temp, field);
            }
            if(!_fixedTemperature) {
                temperature = temp;
                _viewmodel.ChangeTemperature(temp);
                TemperatureTextBox.Text = temp.ToString("0.00");
            }
            if(!_fixedMagnfield) {
                magneticfield = field;
                _viewmodel.ChangeField(field);
                MagnFieldTextBox.Text = field.ToString("0.00");
            }
        }

        private void UpdateThumb(double temp, double field) {
            var w = TempMagField.ActualWidth;
            var h = TempMagField.ActualHeight;

            if(w <= 0 || h <= 0) {
                w = TempMagField.Width;
                h = TempMagField.Height;
            }

            var x = temp * (w) / TempMax - ThumbRadius;
            var y = -(field - FieldMax) * (h) - ThumbRadius;
            if(!_fixedTemperature)
                Canvas.SetLeft(FieldThumb, x);
            if(!_fixedMagnfield)
                Canvas.SetTop(FieldThumb, y);
        }

        private bool _fixedTemperature = false;
        private bool _fixedMagnfield = false;
        private bool _snapping = true;
        private void FixTemperature_Checked(object sender, RoutedEventArgs e) {
            _fixedTemperature = ((CheckBox)sender).IsChecked ?? false;
            e.Handled = true;
        }
        private void FixMagneticField_Checked(object sender, RoutedEventArgs e) {
            _fixedMagnfield = ((CheckBox)sender).IsChecked ?? false;
            e.Handled = true;
        }
        private void Toggle_Snapping(object sender, RoutedEventArgs e) {
            _snapping = ((CheckBox)sender).IsChecked ?? false;
        }

        private void Temperature_TextChanged(object sender, KeyEventArgs e) //TextChangedEventArgs e)
        {
            if(e.Key == Key.Enter) {
                double temp;
                if(Double.TryParse(TemperatureTextBox.Text, out temp)) {
                    temp = temp.Bound(0, _tempMax);
                    _viewmodel.ChangeTemperature(temp);
                    temperature = temp;
                    TemperatureTextBox.Text = temperature.ToString("0.00");
                } else {
                    TemperatureTextBox.Text = temperature.ToString("0.00");
                }
                UpdateThumb(temperature, magneticfield);
            }

        }

        private void MagnField_TextChanged(object sender, KeyEventArgs e) {
            if(e.Key == Key.Enter) {
                double magn;
                if(Double.TryParse(MagnFieldTextBox.Text, out magn)) {
                    magn = magn.Bound(-_magnMax, _magnMax);
                    _viewmodel.ChangeField(magn);
                    magneticfield = magn;
                    MagnFieldTextBox.Text = magn.ToString("0.00");
                } else {
                    MagnFieldTextBox.Text = magneticfield.ToString("0.00");
                }
                UpdateThumb(temperature, magneticfield);
            }

        }

        private void TempMagField_OnMouseMove(object sender, MouseEventArgs e) {
            if(e.LeftButton == MouseButtonState.Pressed) {
                var pos = e.GetPosition(TempMagField);
                var x = pos.X.Bound(-ThumbRadius, TempMagField.ActualWidth - ThumbRadius);
                var y = pos.Y.Bound(-ThumbRadius, TempMagField.ActualHeight - ThumbRadius);
                UpdateParameters(x, y);
                UpdateThumb(temperature, magneticfield);
            }
        }

        private void TempMagField_OnMouseDown(object sender, MouseButtonEventArgs e) {
            TempMagField.CaptureMouse();
        }

        private void TempMagField_OnMouseUp(object sender, MouseButtonEventArgs e) {
            TempMagField.ReleaseMouseCapture();
        }


        #endregion

        #region Lattice Size
        private void LatticeSize_Click(object sender, RoutedEventArgs e) {
            ThreadedAction(ScaleLattice);
            e.Handled = true;
        }


        private void SizeSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
            if(LatticeSize != null) LatticeSize.Content = "Size: " + CurrentN.ToString();


        }
        private void SizeSliderDragCompleted(object sender, DragCompletedEventArgs e) {
            ThreadedAction(ScaleLattice);
            e.Handled = true;
        }

        private void LatticeSize_KeyDown(object sender, KeyEventArgs e) {
            if(e.Key == Key.Left) {
                _changeLatticeSize(false);
                e.Handled = true;
            } else if(e.Key == Key.Right) {
                _changeLatticeSize(true);
                e.Handled = true;
            }
        }

        private void LatticeSize_MouseWheel(object sender, MouseWheelEventArgs e) {
            _changeLatticeSize(e.Delta > 0);
            e.Handled = true;
        }

        //if using scrollwheel increase/decrase to next divisor of Pixel (800) (to avoid ugly rendering) - can be finetuned with left/right keys if necessary
        private void _changeLatticeSize(bool bigger) {
            SizeSlider.Value += bigger ? 1 : -1;
            ThreadedAction(ScaleLattice);
        }


        #endregion

        #region Rendering
        static internal void RefreshRender() {
            Current._viewmodel.Refresh();
        }

        #endregion

        #region Plotting

        private LineAnnotation line, mlineM, mlineP;
        public string Title { get; private set; }

        public IList<DataPoint> EnergyPoints { get; private set; }
        public IList<DataPoint> MagnetizationPoints { get; private set; }


        private void Plotinit() {
            EnergyPlot.ItemsSource = EnergyPoints = new List<DataPoint>();
            MagnetizationPlot.ItemsSource = MagnetizationPoints = new List<DataPoint>();
            EnergyPlot.Color = Colors.Red;
            MagnetizationPlot.Color = Colors.DeepSkyBlue;


            line = new LineAnnotation() { Type = LineAnnotationType.Vertical, X = -10, StrokeThickness = 10, LineStyle = LineStyle.Solid, Color = lineColors[0] };

            var mcol = MagnetizationPlot.Color.SetTransparency(120);
            mlineM = new LineAnnotation() { Intercept = -1, StrokeThickness = 1, Color = mcol };
            mlineP = new LineAnnotation() { Intercept = 1, StrokeThickness = 1, Color = mcol };
            var ecol = EnergyPlot.Color.SetTransparency(120);
            Plot.Annotations.Add(mlineP);
            Plot.Annotations.Add(mlineM);
            Plot.Annotations.Add(line);
            Plot.IsLegendVisible = true;

            EnergyPlot.TrackerFormatString = MagnetizationPlot.TrackerFormatString = "{0}: {4:0.00}";
            //setup tick/background etc color (matching the dark theme)
            SwitchTheme(true);
            for(int i = 0; i < _plotDataMax; i++) {
                EnergyPoints.Add(new DataPoint(i, 0));
                MagnetizationPoints.Add(new DataPoint(i, 0));
            }
        }

        #endregion

        #region Threading
        private bool _running = false;
        private BackgroundWorker _worker;
        private void Start_Click(object sender, RoutedEventArgs e) {
            _running = !_running;
            ToggleSimulation.Content = _running ? "Stop" : "Start";
            if(_running)
                run.Release();
            else
                run.WaitOne();
        }

        private BackgroundWorker worker_Init() {
            var worker = new BackgroundWorker() { WorkerReportsProgress = true };
            worker.DoWork += worker_Work;
            worker.ProgressChanged += worker_Progress;
            return worker;
        }

        private void worker_Work(object sender, DoWorkEventArgs e) {
            int i = 0;
            while(true) {
                run.WaitOne();
                sem.WaitOne();
                var data = _viewmodel.Sweep();
                var backgroundWorker = sender as BackgroundWorker;
                if(backgroundWorker != null) backgroundWorker.ReportProgress(i++, data);
                sem.Release();
                run.Release();
            }
        }

        long _timerefresh;

        private int _plotDataMax = 500;
        private int _plotIndex = 0;

        private void worker_Progress(object sender, ProgressChangedEventArgs e) {
            long time = DateTime.Now.Ticks / TimeSpan.TicksPerMillisecond;
            if(time - _timerefresh > 40) {
                StatusText.Text = e.ProgressPercentage.ToString();
                var data = (Tuple<double, double>)e.UserState; //not checking for null due to performance reasons.
                EnergyPoints[_plotIndex] = new DataPoint(_plotIndex, data.Item1);
                MagnetizationPoints[_plotIndex] = new DataPoint(_plotIndex, data.Item2);
                _plotIndex = (_plotIndex + 1) % _plotDataMax;
                line.X = _plotIndex;
                _viewmodel.Refresh();
                _timerefresh = time;
                Plot.InvalidatePlot();
            }
        }

        #endregion


    }
}
