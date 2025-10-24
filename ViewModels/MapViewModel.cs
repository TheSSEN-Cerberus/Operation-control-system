using GMap.NET;
using Operation_Control_System.Infrastructure;
using Operation_Control_System.Services;

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


        private PointLatLng _mapCenter = new PointLatLng(37.5665, 126.9780); // 기본: 서울
        public PointLatLng MapCenter
        {
            get => _mapCenter;
            set => SetProperty(ref _mapCenter, value);
        }

        private double _zoom = 18; // 최대 줌 정도로 설정
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
        }

        private void OnSetStartPosition()
        {
            MapCenter = new PointLatLng(StartLatitude, StartLongitude);
            Zoom = 20;
            System.Diagnostics.Debug.WriteLine($"[Map] Set Center → {StartLatitude}, {StartLongitude}");
        }
    }
}