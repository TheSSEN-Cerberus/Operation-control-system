using System.Windows.Media;
using Operation_Control_System.Models;
using Operation_Control_System.Infrastructure;

namespace Operation_Control_System.ViewModels
{
    /// <summary>
    /// UI 표시용 BBox (BBox 모델을 변환한 ViewModel)
    /// </summary>
    public class BBoxViewModel : BaseViewModel
    {
        public int Id { get; }
        public double X { get; }
        public double Y { get; }
        public double Width { get; }
        public double Height { get; }
        public string ClassLabel { get; }
        public Brush BoxColor { get; }

        public BBoxViewModel(DetectedObject model, int frameWidth, int frameHeight)
        {
            Id = model.Id;
            ClassLabel = model.Class;

            // 좌표 변환: (x1,y1,x2,y2) → WPF 픽셀 단위
            X = model.BBox[0];
            Y = model.BBox[1];
            Width = model.BBox[2] - model.BBox[0];
            Height = model.BBox[3] - model.BBox[1];

            // 신뢰도 기반 색상 (예시)
            if (model.Confidence >= 0.8f)
                BoxColor = Brushes.LimeGreen;
            else if (model.Confidence >= 0.5f)
                BoxColor = Brushes.Yellow;
            else
                BoxColor = Brushes.Red;
        }
    }
}