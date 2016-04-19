using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
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

namespace ThreadedProgram
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            Title = string.Format("PID = {0}", Process.GetCurrentProcess().Id);
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Task.Factory.StartNew(Crunch);
        }

        private static void Crunch()
        {
            int count = 0;
            int primes = 0;
            Parallel.For(0, int.MaxValue, 
                new ParallelOptions() {MaxDegreeOfParallelism = Environment.ProcessorCount - 1 },
                i =>
                {
                    Interlocked.Add(ref count, i);
                    if (IsPrime(i))
                    {
                        Interlocked.Increment(ref primes);
                    }
                });
        }

        private static bool IsPrime(int i)
        {
            if (i <= 1) return false;
            if (i == 2) return true;
            for (int k = 2; k < Math.Sqrt(i); ++k)
            {
                Thread.Sleep(5);
                if (i % k == 0)
                    return false;
            }
            return true;
        }
    }
}
