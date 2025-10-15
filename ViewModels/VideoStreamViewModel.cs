using Operation_Control_System.ViewModels;
using System.Windows.Media.Imaging;

namespace Operation_Control_System.ViewModels
{
    public class VideoStreamViewModel : BaseViewModel
    {
        private BitmapImage? _currentFrame;
        public BitmapImage? CurrentFrame
        {
            get => _currentFrame;
            set => SetProperty(ref _currentFrame, value);
        }

        private string _selectedBBoxInfo = "객체 정보 없음";
        public string SelectedBBoxInfo
        {
            get => _selectedBBoxInfo;
            set => SetProperty(ref _selectedBBoxInfo, value);
        }
    }
}
