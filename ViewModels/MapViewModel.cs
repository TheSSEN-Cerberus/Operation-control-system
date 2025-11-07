using GMap.NET;
using GMap.NET.WindowsPresentation;
using Operation_Control_System.Infrastructure;
using Operation_Control_System.Models;
using Operation_Control_System.Services;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace Operation_Control_System.ViewModels
{
    public class MapViewModel : BaseViewModel
    {
        private readonly NetworkService _networkService;
        private GMapControl? _mapControl;

        // --- 기본 위치 / 상태 ---
        public double StartLatitude { get; set; } = 37.4807667;
        public double StartLongitude { get; set; } = 126.8778012;

        private PointLatLng _mapCenter;
        public PointLatLng MapCenter
        {
            get => _mapCenter;
            set
            {
                if (SetProperty(ref _mapCenter, value))
                    UpdateMapCenter();
            }
        }

        private double _zoom = 5;
        public double Zoom
        {
            get => _zoom;
            set
            {
                if (SetProperty(ref _zoom, value) && _mapControl != null)
                    _mapControl.Zoom = value;
            }
        }

        private GMapMarker? _robotMarker;
        private GMapMarker? _targetMarker;
        private GMapPolygon? _fovSector;

        private double _heading;
        public double Heading
        {
            get => _heading;
            set => SetProperty(ref _heading, value);
        }

        public RelayCommand SetStartPositionCommand { get; }

        public MapViewModel(NetworkService networkService)
        {
            _networkService = networkService;
            _networkService.StatusReceived += OnStatusReceived;
            _networkService.FireReadyReceived += OnFireReadyReceived;

            MapCenter = new PointLatLng(StartLatitude, StartLongitude);
            SetStartPositionCommand = new RelayCommand(OnSetStartPosition);
        }

        // ✅ GMapControl을 View에서 주입
        public void Initialize(GMapControl mapControl)
        {
            _mapControl = mapControl;
            _mapControl.Position = MapCenter;
            _mapControl.Zoom = Zoom;
            _mapControl.CanDragMap = false;
            _mapControl.MouseWheelZoomType = MouseWheelZoomType.MousePositionAndCenter;
            _mapControl.ShowCenter = false;
            GMaps.Instance.Mode = AccessMode.ServerAndCache;
        }


        private void UpdateMapCenter()
        {
            if (_mapControl != null)
                _mapControl.Position = MapCenter;
        }

        private void OnStatusReceived(StatusData data)
        {
            Debug.WriteLine(data.Yaw);
            if(data.Yaw >= 0)
                Heading = data.Yaw;
            else
            {
                Heading = 360 + data.Yaw;
            }

            Application.Current?.Dispatcher?.BeginInvoke(() =>
            {
                UpdateHeadingMarker();
                UpdateRadarSector(); // ✅ 헤딩 업데이트 후 시야 부채꼴 갱신
            });
        }
        private void OnFireReadyReceived(FireReadyData data)
        {
            if (!data.IsReady) return;
            Application.Current?.Dispatcher?.BeginInvoke(() => UpdateTargetMarker(data.Distance));
        }

        // --- 마커 업데이트 ---
        private void UpdateHeadingMarker()
        {
            if (_mapControl == null) return;

            // ✅ 로봇 중심 마커 생성
            if (_robotMarker == null)
            {
                _robotMarker = new GMapMarker(MapCenter)
                {
                    Shape = new Ellipse
                    {
                        Width = 10,
                        Height = 10,
                        Fill = Brushes.LimeGreen,
                        RenderTransform = new TranslateTransform(-5, -5)
                    }
                };
                _mapControl.Markers.Add(_robotMarker);
            }
            else
            {
                _robotMarker.Position = MapCenter;
            }
        }
        private void UpdateRadarSector()
        {
            if (_mapControl == null || _robotMarker == null)
                return;

            const double fovHalf = 25.0;     // 좌우 ±25도
            const double rangeMeters = 80.0; // 부채꼴 반경 (m)
            const int steps = 24;            // 곡선 해상도

            double startAngle = Heading - fovHalf;
            double endAngle = Heading + fovHalf;

            // 중심점
            var center = _robotMarker.Position;

            // 부채꼴 좌표 계산
            var points = new List<PointLatLng>();
            points.Add(center);

            for (int i = 0; i <= steps; i++)
            {
                double a = startAngle + i * (endAngle - startAngle) / steps;
                points.Add(CalculateTargetPoint(center, a, rangeMeters));
            }

            points.Add(center); // 닫기

            // 기존 부채꼴 갱신 or 생성
            if (_fovSector == null)
            {
                _fovSector = new GMapPolygon(points)
                {
                    Shape = new System.Windows.Shapes.Path
                    {
                        Fill = new SolidColorBrush(Color.FromArgb(80, 255, 0, 0)), // 반투명 빨강
                        Stroke = Brushes.Red,
                        StrokeThickness = 1
                    }
                };

                _mapControl.RegenerateShape(_fovSector);
                _mapControl.Markers.Add(_fovSector);
            }
            else
            {
                _fovSector.Points.Clear();
                foreach (var p in points)
                    _fovSector.Points.Add(p);

                _mapControl.RegenerateShape(_fovSector);
            }
        }

        private void UpdateTargetMarker(double distance)
        {
            if (_mapControl == null) return;

            var targetPoint = CalculateTargetPoint(MapCenter, Heading, distance);

            if (_targetMarker == null)
            {
                _targetMarker = new GMapMarker(targetPoint)
                {
                    Shape = new Ellipse
                    {
                        Width = 12,
                        Height = 12,
                        Stroke = Brushes.Yellow,
                        StrokeThickness = 2,
                        Fill = Brushes.Transparent
                    }
                };
                _mapControl.Markers.Add(_targetMarker);
            }

            _targetMarker.Position = targetPoint;
        }

        private PointLatLng CalculateTargetPoint(PointLatLng origin, double headingDeg, double distanceMeters)
        {
            const double EarthRadius = 6378137.0; // m
            double headingRad = headingDeg * Math.PI / 180.0;
            double lat1 = origin.Lat * Math.PI / 180.0;
            double lon1 = origin.Lng * Math.PI / 180.0;

            double lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(distanceMeters / EarthRadius)
                        + Math.Cos(lat1) * Math.Sin(distanceMeters / EarthRadius) * Math.Cos(headingRad));
            double lon2 = lon1 + Math.Atan2(Math.Sin(headingRad) * Math.Sin(distanceMeters / EarthRadius) * Math.Cos(lat1),
                                            Math.Cos(distanceMeters / EarthRadius) - Math.Sin(lat1) * Math.Sin(lat2));

            return new PointLatLng(lat2 * 180.0 / Math.PI, lon2 * 180.0 / Math.PI);
        }

        private void OnSetStartPosition()
        {
            MapCenter = new PointLatLng(StartLatitude, StartLongitude);
            Zoom = 19;
        }
    }
}
