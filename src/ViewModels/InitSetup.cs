using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Komorebi.ViewModels
{
    public class InitSetup : Popup
    {
        public InitSetup()
        {
            _selectedLocale = Preferences.DetectedLocale;
        }

        public string SelectedLocale
        {
            get => _selectedLocale;
            set
            {
                if (SetProperty(ref _selectedLocale, value))
                    App.SetLocale(value);
            }
        }

        [Required]
        public string DefaultCloneDir
        {
            get => _defaultCloneDir;
            set => SetProperty(ref _defaultCloneDir, value, true);
        }

        public override Task<bool> Sure()
        {
            Preferences.Instance.Locale = _selectedLocale;
            Preferences.Instance.GitDefaultCloneDir = _defaultCloneDir;
            return Task.FromResult(true);
        }

        private string _selectedLocale;
        private string _defaultCloneDir = string.Empty;
    }
}
