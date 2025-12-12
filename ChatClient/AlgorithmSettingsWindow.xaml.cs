using ChatClient.Services;
using System.Windows;
using System.Windows.Controls;

namespace ChatClient
{
    /// <summary>
    /// Interaction logic for AlgorithmSettingsWindow.xaml
    /// </summary>
    public partial class AlgorithmSettingsWindow : Window
    {
        private readonly IEncryptionService _encryptionService;

        public AlgorithmSettingsWindow(IEncryptionService encryptionService)
        {
            InitializeComponent();
            _encryptionService = encryptionService;

            // Set initial selections based on current settings
            CipherAlgorithmComboBox.SelectedItem = _encryptionService.CurrentAlgorithm;
            EncryptionModeComboBox.SelectedItem = _encryptionService.CurrentMode;
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (CipherAlgorithmComboBox.SelectedItem is ComboBoxItem selectedAlgorithmItem &&
                EncryptionModeComboBox.SelectedItem is ComboBoxItem selectedModeItem)
            {
                if (System.Enum.TryParse<CipherAlgorithm>(selectedAlgorithmItem.Content.ToString(), out var algorithm))
                {
                    _encryptionService.SetAlgorithm(algorithm);
                }
                if (System.Enum.TryParse<CipherMode>(selectedModeItem.Content.ToString(), out var mode))
                {
                    _encryptionService.SetMode(mode);
                }
                MessageBox.Show("Settings saved!");
                Close();
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
