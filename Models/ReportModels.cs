using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace DQEHelper.Models
{
    public abstract class ReportNode : INotifyPropertyChanged
    {
        private bool _isChecked = true;
        public string Title { get; set; } = string.Empty;

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    OnPropertyChanged();
                    OnCheckedChanged();
                }
            }
        }

        protected virtual void OnCheckedChanged() { }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    // 🚀 Лист дерева (комната)
    public class RoomNode : ReportNode
    {
        public int RoomIndex { get; set; }
    }

    // 🚀 Снэп теперь содержит комнаты
    public class SnapNode : ReportNode
    {
        public int SnapIndex { get; set; }
        public ObservableCollection<RoomNode> Rooms { get; } = new();

        protected override void OnCheckedChanged()
        {
            foreach (var room in Rooms) room.IsChecked = IsChecked;
        }
    }

    // 🚀 Ошибка теперь содержит снэпы
    public class ErrorGroupNode : ReportNode
    {
        public ObservableCollection<SnapNode> Snaps { get; } = new();

        protected override void OnCheckedChanged()
        {
            foreach (var snap in Snaps) snap.IsChecked = IsChecked;
        }
    }

    // 🚀 Провайдер содержит группы ошибок
    public class ProviderNode : ReportNode
    {
        public ObservableCollection<ErrorGroupNode> Errors { get; } = new();
        
        protected override void OnCheckedChanged()
        {
            foreach (var error in Errors) error.IsChecked = IsChecked;
        }
    }
}