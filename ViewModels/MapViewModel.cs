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
        private bool _isSet;
        public bool IsSet
        {
            get => _isSet;
            set => SetProperty(ref _isSet, value);
        }

        private readonly SharedStateService _shared;
        private readonly NetworkService _networkService;
        private GMapControl? _mapControl;

        // --- 기본 위치 / 상태 ---
        private double _latitude;
        public double Latitude
        {
            get => _latitude;
            set => SetProperty(ref _latitude, value);
        }

        private double _longitude;
        public double Longitude
        {
            get => _longitude;
            set => SetProperty(ref _longitude, value);
        }

        private double _altitude;
        public double Altitude
        {
            get => _altitude;
            set => SetProperty(ref _altitude, value);
        }



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
        private GMapPolygon? _fovSector;
        private readonly Dictionary<int, GMapMarker> _targetMarkers = new();


        private double _robotHeading;
        private bool _headingInitialized = false;

        public RelayCommand SetStartPositionCommand { get; }

        public MapViewModel(NetworkService networkService, SharedStateService shared)
        {
            // 가산 어반워크2
            //Latitude = 37.4807667;
            //Longitude = 126.8778012;

            // 넥스원 판교하우스
            Latitude = 37.4049098;
            Longitude = 127.1113313;


            _shared = shared;
            _networkService = networkService;
            _networkService.StatusReceived += OnStatusReceived;
            _networkService.FireReadyReceived += OnFireReadyReceived;
            _networkService.FireDoneReceived += OnFireDoneReceived;

            MapCenter = new PointLatLng(Latitude, Longitude);
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


        public void UpdateRobotPosition(double distanceCm)
        {
            double distanceM = distanceCm / 100.0;
            const double EarthRadius = 6378137.0; // m

            double headingRad = _robotHeading * Math.PI / 180.0;
            double dLat = (distanceM * Math.Cos(headingRad)) / EarthRadius;
            double dLon = (distanceM * Math.Sin(headingRad)) / (EarthRadius * Math.Cos(Latitude * Math.PI / 180.0));

            Latitude += dLat * 180.0 / Math.PI;
            Longitude += dLon * 180.0 / Math.PI;

            MapCenter = new PointLatLng(Latitude, Longitude);
            Debug.WriteLine($"[Map] 위치 갱신 → {Latitude:F6}, {Longitude:F6}");
        }

        private void UpdateMapCenter()
        {
            if (_mapControl != null)
                _mapControl.Position = MapCenter;
        }

        private void OnFireDoneReceived(FireDoneData data)
        {
            Application.Current?.Dispatcher?.Invoke(() =>
            {
                if (_targetMarkers.TryGetValue(data.TargetId, out var circle))
                {
                    // circle 제거
                    _mapControl.Markers.Remove(circle);
                    _targetMarkers.Remove(data.TargetId);

                    var point = circle.Position;

                    // X 마커 생성
                    var xMarker = new GMapMarker(point)
                    {
                        Shape = CreateXShape(),
                        Offset = new Point(-6, -6),
                        ZIndex = 11
                    };

                    _mapControl.Markers.Add(xMarker);

                    Debug.WriteLine($"[Map] ❌ Target {data.TargetId} DONE at {point.Lat}, {point.Lng}");
                }
                else
                {
                    Debug.WriteLine($"[Map] ⚠ No READY circle found for {data.TargetId}");
                }
            });
        }
        //private void OnFireDoneReceived(FireDoneData data)
        //{
        //    Application.Current?.Dispatcher?.BeginInvoke(() =>
        //    {
        //        var bbox = _shared.BBoxes.FirstOrDefault(b => b.Id == data.TargetId);
        //        if (bbox == null || bbox.TargetLat == null || bbox.TargetLon == null)
        //        {
        //            Debug.WriteLine($"[Map] ⚠ Target {data.TargetId} has no saved position");
        //            return;
        //        }

        //        double lat = bbox.TargetLat.Value;
        //        double lon = bbox.TargetLon.Value;

        //        // X 마커 새로 생성
        //        var xMarker = new GMapMarker(new PointLatLng(lat, lon))
        //        {
        //            Shape = CreateXShape(),
        //            Offset = new System.Windows.Point(-6, -6),
        //            ZIndex = 10
        //        };

        //        _mapControl.Markers.Add(xMarker);
        //        Debug.WriteLine($"[Map] ❌ Target {data.TargetId} marked at {lat},{lon}");
        //    });
        //}

        private void OnStatusReceived(StatusData data)
        {
            double yaw = data.Yaw;    // -180 ~ +180

            // 1) -180~180 -> 0~360 (East=0, CCW)
            double yawE = (yaw + 360.0) % 360.0;

            // 2) CCW -> CW (좌우 반전)
            double yawCW = (360.0 - yawE) % 360.0;

            double pitch = data.Pitch;
            _shared.Pitch = pitch;   

            // 3) East=0 -> North=0 (지도 기준으로 회전축 변환)
            _shared.Heading = (yawCW + 90.0) % 360.0;
            if (!_headingInitialized)
            {
                _headingInitialized = true;
                _robotHeading = _shared.Heading;
                Debug.WriteLine("[Init] heading: ", _robotHeading, " yaw:", data.Yaw);

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
            // BBoxes에서 priority 가져오기
            var bbox = _shared.BBoxes.FirstOrDefault(b => b.Id == data.TargetId);
            int priority = bbox?.Priority ?? 0;
            double heading = _shared.Heading;
            double pitch = _shared.Pitch;

            var (lat, lon, alt) = CalculateTargetPosition3D(Latitude, Longitude, Altitude, heading, pitch, data.Distance);
            Debug.WriteLine($"pitch: {pitch}, dist: {data.Distance}");

            Application.Current?.Dispatcher?.Invoke(() =>
            {
                if(bbox!= null)
                {
                    bbox.TargetLat = lat;
                    bbox.TargetLon = lon;
                    bbox.TargetAlt = alt;
                    UpdateTargetMarker(lat, lon, data.TargetId);
                }
            });

        }

        // --- 마커 업데이트 ---

        public (double lat, double lon, double alt) CalculateTargetPosition3D(
        double startLat, double startLon, double startAlt,
        double headingDeg, double pitchDeg,
        double distance)
        {
            // pitch 정의 보정: 위(+), 아래(-)로 변환 
            double pitchRad = (-pitchDeg) * Math.PI / 180.0;
            double distanceCm = distance / 100;
            // 수평 거리
            double horizontal = distance * Math.Cos(pitchRad);

            // 고도 차이
            double dz = distanceCm * Math.Sin(pitchRad);
            double alt = startAlt + dz;

            // 기존 lat/lon 계산 재사용
            var (lat, lon) = CalculateTargetPosition(startLat, startLon, headingDeg, horizontal);

            return (lat, lon, alt);
        }


        public void UpdateTargetMarker(double lat, double lon, int id)
        {
            var point = new PointLatLng(lat, lon);

            // 기존 마커 업데이트
            if (_targetMarkers.TryGetValue(id, out var marker))
            {
                marker.Position = point;   // ⭐ 위치만 갱신
                return;
            }

            // 새 마커 생성
            var newMarker = new GMapMarker(point)
            {
                Shape = new Ellipse
                {
                    Width = 12,
                    Height = 12,
                    Stroke = Brushes.Red,
                    StrokeThickness = 2,
                    Fill = Brushes.Red
                },
                Offset = new System.Windows.Point(-6, -6),
                ZIndex = 2
            };

            _targetMarkers[id] = newMarker;
            _mapControl?.Markers.Add(newMarker);
        }
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
            const double rangeMeters = 100.0; // 부채꼴 반경 (m)
            const int steps = 24;            // 곡선 해상도

            double startAngle = _shared.Heading - fovHalf;
            double endAngle = _shared.Heading + fovHalf;

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

        public (double lat, double lon) CalculateTargetPosition(double startLat, double startLon, double headingDeg, double distance)
        {
            const double EarthRadius = 6378137.0; // meters
            double distanceCm = distance / 10; // cm 기준 10배로
            double bearing = headingDeg * Math.PI / 180.0;
            double lat1 = startLat * Math.PI / 180.0;
            double lon1 = startLon * Math.PI / 180.0;

            double lat2 = Math.Asin(Math.Sin(lat1) * Math.Cos(distanceCm / EarthRadius) +
                                    Math.Cos(lat1) * Math.Sin(distanceCm / EarthRadius) * Math.Cos(bearing));

            double lon2 = lon1 + Math.Atan2(Math.Sin(bearing) * Math.Sin(distanceCm / EarthRadius) * Math.Cos(lat1),
                                            Math.Cos(distanceCm / EarthRadius) - Math.Sin(lat1) * Math.Sin(lat2));

            return (lat2 * 180.0 / Math.PI, lon2 * 180.0 / Math.PI);
        }


        private FrameworkElement CreateXShape()
        {
            return new Path
            {
                Stroke = Brushes.Red,
                StrokeThickness = 3,
                Data = Geometry.Parse("M -6 -6 L 6 6 M -6 6 L 6 -6")
            };
        }

        private void OnSetStartPosition()
        {
            MapCenter = new PointLatLng(Latitude, Longitude);
            Zoom = 19;
            IsSet = true;
        }
    }
}
