using System.Windows;

namespace ChatClient
{
    public partial class CreateChatWindow : Window
    {
        public string ChatName { get; private set; } = string.Empty;

        public CreateChatWindow()
        {
            InitializeComponent();
        }

        private void CreateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ChatNameTextBox.Text))
            {
                ChatName = ChatNameTextBox.Text;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Please enter a chat name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
