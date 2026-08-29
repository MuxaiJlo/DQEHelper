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

    public class ErrorNode : ReportNode { }

    // 🚀 НОВЫЙ КЛАСС: Узел Комнаты
    public class RoomNode : ReportNode
    {
        public ObservableCollection<ErrorNode> Errors { get; } = new();
        public int RoomIndex { get; set; }

        protected override void OnCheckedChanged()
        {
            foreach (var error in Errors) error.IsChecked = IsChecked;
        }
    }

    // 🚀 ИЗМЕНЕНО: Snap теперь содержит Комнаты (Rooms)
    public class SnapNode : ReportNode
    {
        public ObservableCollection<RoomNode> Rooms { get; } = new();
        public int SnapIndex { get; set; }

        protected override void OnCheckedChanged()
        {
            foreach (var room in Rooms) room.IsChecked = IsChecked;
        }
    }

    public class ProviderNode : ReportNode
    {
        public ObservableCollection<SnapNode> Snaps { get; } = new();
        
        protected override void OnCheckedChanged()
        {
            foreach (var snap in Snaps) snap.IsChecked = IsChecked;
        }
    }
}