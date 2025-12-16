using System.Windows;
using System.Windows.Controls;

namespace ChatClient
{
    public partial class CreateChatWindow : Window
    {
        public string ChatName { get; private set; } = string.Empty;
        public string CipherAlgorithm { get; private set; } = "RC6";
        public string CipherMode { get; private set; } = "CBC";
        public string PaddingMode { get; private set; } = "PKCS7";

        public CreateChatWindow()
        {
            InitializeComponent();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ChatNameTextBox.Text))
            {
                ChatName = ChatNameTextBox.Text;
                CipherAlgorithm = ((ComboBoxItem)AlgorithmComboBox.SelectedItem).Content.ToString() ?? "RC6";
                CipherMode = ((ComboBoxItem)ModeComboBox.SelectedItem).Content.ToString() ?? "CBC";
                PaddingMode = ((ComboBoxItem)PaddingComboBox.SelectedItem).Content.ToString() ?? "PKCS7";
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Please enter a chat name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
