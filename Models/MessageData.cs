
namespace Operation_Control_System.Models
{
    /// <summary>
    /// CSU 통신용 메시지 데이터의 공통 추상 클래스.
    /// 실제 데이터는 StatusData, FireReadyData 등 파생 클래스에서 정의됨.
    /// </summary>
    public abstract class MessageData
    {
        // 공통 필드 없음 — 구조 통합용 베이스 클래스
        public static implicit operator MessageData(int v)
        {
            throw new NotImplementedException();
        }
    }
}
