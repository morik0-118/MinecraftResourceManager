using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace MinecraftResourceManager
{
    public partial class InstanceDetectionWindow : Window
    {
        public List<InstanceCandidate> Candidates { get; }

        public InstanceDetectionWindow(List<InstanceCandidate> candidates)
        {
            InitializeComponent();

            Candidates = candidates;

            DataContext = this;
        }

        private void AddSelected_Click(object sender, RoutedEventArgs e)
        {
            if (!Candidates.Any(candidate => candidate.IsSelected))
            {
                MessageBox.Show(
                    "追加するインスタンスを1つ以上選択してください。",
                    "インスタンス検出",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                return;
            }

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}