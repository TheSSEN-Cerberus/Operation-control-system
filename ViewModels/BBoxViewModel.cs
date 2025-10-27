using Operation_Control_System.Infrastructure;
using Operation_Control_System.Models;
using System.Windows.Input;
using System.Windows.Media;

namespace Operation_Control_System.ViewModels
{
    /// <summary>
    /// UI 표시용 BBox (BBox 모델을 변환한 ViewModel)
    /// </summary>
    public class BBoxViewModel : BaseViewModel
    {
        public int Id { get; }

        private double _x;
        public double X
        {
            get => _x;
            set => SetProperty(ref _x, value);
        }

        private double _y;
        public double Y
        {
            get => _y;
            set => SetProperty(ref _y, value);
        }

        private double _width;
        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value);
        }

        private double _height;
        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value);
        }

        public string ClassLabel { get; }
        public Brush BoxColor { get; }

        public ICommand ClickCommand { get; }
        public event Action<int>? Clicked;

        public BBoxViewModel(DetectedObject model, int frameWidth, int frameHeight)
        {
            Id = model.Id;
            ClassLabel = model.Class;
            ClickCommand = new RelayCommand(() => Clicked?.Invoke(Id));

            // 초기 위치 설정
            Update(model, frameWidth, frameHeight);

            // 신뢰도 기반 색상
            if (model.Confidence >= 0.8f)
                BoxColor = Brushes.LimeGreen;
            else if (model.Confidence >= 0.5f)
                BoxColor = Brushes.Yellow;
            else
                BoxColor = Brushes.Red;
        }

        /// <summary>
        /// 동일 ID의 BBox 좌표만 업데이트 (매 프레임 호출)
        /// </summary>
        public void Update(DetectedObject model, int frameWidth, int frameHeight)
        {
            // 좌표 변환: (x1, y1, x2, y2)
            X = model.BBox[0];
            Y = model.BBox[1];
            Width = model.BBox[2] - model.BBox[0];
            Height = model.BBox[3] - model.BBox[1];
        }
    }
}