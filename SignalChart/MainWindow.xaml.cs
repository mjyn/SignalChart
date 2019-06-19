using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LiveCharts;
using LiveCharts.Configurations;
using LiveCharts.Defaults;
using LiveCharts.Wpf;
using System.IO;
using System.IO.Ports;

namespace SignalChart
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        public MainWindow()
        {
            InitializeComponent();

            var mapper = Mappers.Xy<MeasureModel>()
                .X(model => model.DateTime.Ticks)   //use DateTime.Ticks as X
                .Y(model => model.Value);           //use the value property as Y

            //lets save the mapper globally.
            Charting.For<MeasureModel>(mapper);


            SeriesCollection = new SeriesCollection();
            SeriesCollectionDelta = new SeriesCollection();
            var series1 = new LineSeries
            {
                Title = "Series 1",
                Values = new ChartValues<MeasureModel>()
            };
            var series2 = new LineSeries
            {
                Title = "Series 2",
                Values = new ChartValues<MeasureModel>()
            };
            var seriesd = new LineSeries
            {
                Title = "Series D",
                Values = new ChartValues<MeasureModel>()
            };
            series1.Fill = Brushes.Transparent;
            series2.Fill = Brushes.Transparent;
            seriesd.Fill = Brushes.Transparent;
            SeriesCollection.Add(series1);
            SeriesCollection.Add(series2);
            SeriesCollectionDelta.Add(seriesd);

            DateTimeFormatter = value => new DateTime((long)value).ToString("mm:ss");

            AxisStep = TimeSpan.FromSeconds(1).Ticks;
            AxisUnit = TimeSpan.TicksPerSecond;
            SetAxisLimits(DateTime.Now);

            Value1 = 0;
            Value2 = 0;

            indata = "";
            DataContext = this;
        }
        private void SetAxisLimits(DateTime now)
        {
            AxisMax = now.Ticks + TimeSpan.FromSeconds(1).Ticks; // lets force the axis to be 1 second ahead
            AxisMin = now.Ticks - TimeSpan.FromSeconds(8).Ticks; // and 8 seconds behind
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="series">0-index</param>
        /// <param name="value"></param>
        private void AddData(int seriesindex, double value, DateTime dateTime)
        {
            if (seriesindex == 0)
                Value1 = value;
            if (seriesindex == 1)
                Value2 = value;
            var series = SeriesCollection[seriesindex] as LineSeries;
            var seriesd = SeriesCollectionDelta[0] as LineSeries;
            var values = series.Values as ChartValues<MeasureModel>;
            var valuesd = seriesd.Values as ChartValues<MeasureModel>;
            var newmodel = new MeasureModel() { DateTime = dateTime, Value = value };

            values.Add(newmodel);
            var newmodeld = new MeasureModel() { DateTime = dateTime };

            {
                var series0 = SeriesCollection[0] as LineSeries;
                var series1 = SeriesCollection[1] as LineSeries;
                var values0 = series0.Values as ChartValues<MeasureModel>;
                var values1 = series1.Values as ChartValues<MeasureModel>;
                double v0, v1;
                if (values0.Count == 0)
                    v0 = 0;
                else
                    v0 = values0.Last().Value;
                if (values1.Count == 0)
                    v1 = 0;
                else
                    v1 = values1.Last().Value;

                var delta = v0 - v1;
                newmodeld.Value = delta;
            }

            if (valuesd.Count != 0)
                if ((dateTime - valuesd.Last().DateTime) < TimeSpan.FromMilliseconds(50))
                    valuesd.Remove(valuesd.Last());

            valuesd.Add(newmodeld);
            if (values.Count > 20)
                values.RemoveAt(0);

            if (valuesd.Count > 20)
                valuesd.RemoveAt(0);
            SetAxisLimits(dateTime);
        }

        private double _axisMax;
        private double _axisMin;
        public double AxisMax
        {
            get { return _axisMax; }
            set
            {
                _axisMax = value;
                OnPropertyChanged("AxisMax");
            }
        }
        public double AxisMin
        {
            get { return _axisMin; }
            set
            {
                _axisMin = value;
                OnPropertyChanged("AxisMin");
            }
        }
        public double AxisStep { get; set; }
        public double AxisUnit { get; set; }
        public SeriesCollection SeriesCollection { get; set; }
        public SeriesCollection SeriesCollectionDelta { get; set; }
        public Func<double, string> DateTimeFormatter { get; set; }

        private double _value1;
        private double _value2;
        public double Value1
        {
            get { return _value1; }
            set
            {
                _value1 = value;
                OnPropertyChanged("Value1");
            }
        }
        public double Value2
        {
            get { return _value2; }
            set
            {
                _value2 = value;
                OnPropertyChanged("Value2");
            }
        }
        private SerialPort _serialPort { get; set; }

        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            var r = new Random();
            AddData(0, r.Next(100), DateTime.Now);
            AddData(1, r.Next(100), DateTime.Now);
        }

        private void StartBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _serialPort = new SerialPort(PortBox.Text, int.Parse(BaudrateBox.Text));
                _serialPort.DataReceived += new SerialDataReceivedEventHandler(DataReceivedHandler);
                _serialPort.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }
        }
        private void StopBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _serialPort.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }
        }

        string indata;
        private void DataReceivedHandler(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                SerialPort sp = (SerialPort)sender;
                indata += sp.ReadExisting();

                //Action<TextBlock, String> updateAction = new Action<TextBlock, string>(UpdateTb);
                //TextBox.Dispatcher.BeginInvoke(updateAction, TextBox, indata);
                //MessageBox.Show(indata);

                while (indata.Contains("\n"))
                {
                    var cmd = indata.Split('\n')[0];
                    var index = indata.IndexOf('\n');
                    //double att = double.Parse(indata.Remove(0, index + 15));
                    indata = indata.Remove(0, index + 1); //remove "abc\n"
                    ProcessCmd(cmd);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }

        }

        private void ProcessCmd(string cmd)
        {
            Prompt(cmd);
            if (cmd.StartsWith("Attention1 is: "))
            {
                double att = double.Parse(cmd.Remove(0, 15));
                AddData(0, att, DateTime.Now);
            }
            if (cmd.StartsWith("Attention2 is: "))
            {
                double att = double.Parse(cmd.Remove(0, 15));
                AddData(1, att, DateTime.Now);
            }
        }

        private void Prompt(string text)
        {
            this.Dispatcher.Invoke(new Action(() =>
            {
                if (LogBox.Text.Length > 200)
                    LogBox.Text = LogBox.Text.Remove(0, 100);
                LogBox.Text += text;
                LogBox.ScrollToEnd();
            }));
        }




        #region INotifyPropertyChanged implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName = null)
        {
            if (PropertyChanged != null)
                PropertyChanged.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }


        #endregion


    }
}
