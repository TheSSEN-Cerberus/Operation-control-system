using Operation_Control_System.Infrastructure;
using Operation_Control_System.Models;
using System.Windows.Input;
using System.Windows.Media;
using System.ComponentModel;

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

        public float Confidence { get; }
        public int Priority { get; }

        private bool _isTracked;
        public bool IsTracked
        {
            get => _isTracked;
            set
            {
                if (SetProperty(ref _isTracked, value))
                    UpdateColor();
            }
        }
        public Brush _boxColor = Brushes.LimeGreen;
        public Brush BoxColor
        {
            get => _boxColor;
            set => SetProperty(ref _boxColor, value);
        }

        public ICommand ClickCommand { get; }
        public event Action<int>? Clicked;

        public BBoxViewModel(DetectedObject model, int frameWidth, int frameHeight)
        {
            Id = model.Id;
            ClassLabel = model.Class;
            Confidence = model.Confidence;
            Priority = model.Priority;
            ClickCommand = new RelayCommand(() => Clicked?.Invoke(Id));

            // 초기 위치, 색상 설정
            UpdatePos(model, frameWidth, frameHeight);
            UpdateColor();

        }

        /// <summary>
        /// 동일 ID의 BBox 좌표만 업데이트 (매 프레임 호출)
        /// </summary>
        public void UpdatePos(DetectedObject model, int frameWidth, int frameHeight)
        {

            Width = model.BBox[2];
            Height = model.BBox[3];
            X = model.BBox[0] - (Width / 2.0);
            Y = model.BBox[1] - (Height / 2.0);
        }

        public void UpdateColor()
        {
            if (IsTracked)
                BoxColor = Brushes.Red;
            else
                BoxColor = Priority == 1 ? Brushes.Yellow : Brushes.LimeGreen;
        }
    }
}