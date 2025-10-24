using GMap.NET;
using Operation_Control_System.Infrastructure;
using Operation_Control_System.Services;
using System.Diagnostics;
using System.Windows;

namespace Operation_Control_System.ViewModels
{
    public class MapViewModel : BaseViewModel
    {
        private readonly NetworkService _networkService;
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
        // ========== 현재 지도 위경도 ==========
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


        private PointLatLng _mapCenter = new PointLatLng(37.4778193, 126.8794248); // 기본: 서울
        public PointLatLng MapCenter
        {
            get => _mapCenter;
            set => SetProperty(ref _mapCenter, value);
        }

        private double _zoom = 5;
        public double Zoom
        {
            get => _zoom;
            set => SetProperty(ref _zoom, value);
        }

        // 명령: 지도 위치 업데이트
        public RelayCommand SetStartPositionCommand { get; }

        public MapViewModel(NetworkService networkService)
        {
            _networkService = networkService;
            SetStartPositionCommand = new RelayCommand(OnSetStartPosition);
            Debug.WriteLine(Zoom);
        }

        private void OnSetStartPosition()
        {
            MapCenter = new PointLatLng(StartLatitude, StartLongitude);
            CurrentLatitude = StartLatitude;
            CurrentLongitude = StartLongitude;
            Zoom = 19;
            Debug.WriteLine($"[Map] Set Center → {StartLatitude}, {StartLongitude}");
        }

    }
}