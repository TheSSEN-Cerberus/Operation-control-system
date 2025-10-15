using Operation_Control_System.Infrastructure;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Media;

namespace Operation_Control_System.ViewModels
{
    public class MapViewModel : BaseViewModel
    {
        private double _startLatitude;
        public double StartLatitude
        {
            get => _startLatitude;
            set => SetProperty(ref _startLatitude, value);
        }

        private double _startLongitude;
        public double StartLongitude
        {
            get => _startLongitude;
            set => SetProperty(ref _startLongitude, value);
        }

        private double _currentLatitude;
        public double CurrentLatitude
        {
            get => _currentLatitude;
            set => SetProperty(ref _currentLatitude, value);
        }

        private double _currentLongitude;
        public double CurrentLongitude
        {
            get => _currentLongitude;
            set => SetProperty(ref _currentLongitude, value);
        }

        public ObservableCollection<MapMarker> KilledObjects { get; } = new();

        public ICommand SetStartPositionCommand { get; }

        public MapViewModel()
        {
            SetStartPositionCommand = new RelayCommand(_ => SetStartPosition());
        }

        private void SetStartPosition()
        {
            // TODO: 실제 위치 초기화 로직
            CurrentLatitude = StartLatitude;
            CurrentLongitude = StartLongitude;
        }
    }

    public class MapMarker
    {
        public (double X, double Y) MiniMapPos { get; set; }
        public Brush MarkerColor { get; set; } = Brushes.Red;
    }
}
