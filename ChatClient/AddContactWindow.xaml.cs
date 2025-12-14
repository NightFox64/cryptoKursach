using System.Windows;

namespace ChatClient
{
    public partial class AddContactWindow : Window
    {
        public string ContactLogin { get; private set; }

        public AddContactWindow()
        {
            InitializeComponent();
        }

        private void AddButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(ContactLoginTextBox.Text))
            {
                ContactLogin = ContactLoginTextBox.Text;
                DialogResult = true;
            }
            else
            {
                MessageBox.Show("Contact Login cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
