using MoraeGames.Ability;

namespace MoraeGames.UI
{
    public static class AbilityPresentation
    {
        public static int Symbol(AbilityDataSO skill) => skill is AreaScanData ? 1 : skill is CCProjectileData ? 2 : skill is SmokeBombData ? 3 : skill is DisguiseData ? 4 : skill is DecoyCloneData ? 5 : skill is EscapeTrapData ? 6 : 0;
        public static string Name(AbilityDataSO skill) => skill is SpeedBoostData ? "긴급 추격" : skill is AreaScanData ? "수색 파동" :
            skill is CCProjectileData ? "전기 포획탄" : skill is SmokeBombData ? "연막 탈출" : skill is DisguiseData ? "위장 잠입" :
            skill is DecoyCloneData ? "미끼 분신" : skill is EscapeTrapData ? "추격 방해 덫" : skill is SpeedBurstData ? "전력 질주" : skill != null ? skill.abilityName : "스킬 없음";

        public static string Description(AbilityDataSO skill) => skill is SpeedBoostData ? "이동 속도 상승 · 도둑에게 붙어 체포하세요" :
            skill is AreaScanData ? "주변 도둑을 표시 · 반복 파동으로 위치를 확인하세요" :
            skill is CCProjectileData ? "가까운 도둑을 자동 조준 · 적중하면 이동 속도 감소" :
            skill is SmokeBombData ? "현재 위치에 연막 · AI 탐지와 자동 조준을 피하세요" :
            skill is DisguiseData ? "잠시 AI의 추적을 회피 · 다른 플레이어에게는 보입니다" :
            skill is DecoyCloneData ? "이동하는 분신으로 AI를 유인 · 반대 방향으로 탈출하세요" :
            skill is EscapeTrapData ? "바닥에 덫 설치 · 밟은 경찰을 느리게 만듭니다" :
            skill is SpeedBurstData ? "짧고 빠른 질주 · 갈림길에서 추격을 따돌리세요" : "";
    }
}
