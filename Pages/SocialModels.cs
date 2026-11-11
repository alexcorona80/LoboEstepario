using System.ComponentModel;

namespace LoboEstepario.Pages
{
    /// <summary>Fila de la lista de destinatarios de Redes sociales (con casilla de selección).</summary>
    public sealed class SocialRecipientRow : INotifyPropertyChanged
    {
        private bool selected;
        public string Id { get; set; }
        public string Name { get; set; }
        public string Email { get; set; }
        public string Location { get; set; }

        public bool Selected
        {
            get { return selected; }
            set
            {
                if (selected == value) return;
                selected = value;
                var handler = PropertyChanged;
                if (handler != null) handler(this, new PropertyChangedEventArgs("Selected"));
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
    }
}
