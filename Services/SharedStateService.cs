using Operation_Control_System.Infrastructure;
using Operation_Control_System.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;

namespace Operation_Control_System.Services
{
    /// <summary>
    /// ✅ ViewModel 간 공용 상태를 저장·공유하는 서비스
    /// (예: BBox 목록, Heading, FireReady, 로봇 상태 등)
    /// </summary>
    public class SharedStateService : BaseViewModel
    {
        // --------------------------------------------------------
        // 🎯 [1] 객체 감지 정보 (BBox)
        // --------------------------------------------------------
        private ObservableCollection<BBoxViewModel> _bboxes = new();
        public ObservableCollection<BBoxViewModel> BBoxes
        {
            get => _bboxes;
            set => SetProperty(ref _bboxes, value);
        }

        // --------------------------------------------------------
        // 🧭 [2] 로봇 방향 / 헤딩
        // --------------------------------------------------------
        private double _heading;
        public double Heading
        {
            get => _heading;
            set
            {
                if (SetProperty(ref _heading, value))
                    HeadingChanged?.Invoke(value);
            }
        }

        public event Action<double>? HeadingChanged;


        public void ResetAll()
        {
            BBoxes.Clear();
        }
    }
}