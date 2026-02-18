using UnityEngine;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// LanguageManager handles multi-language support
/// Supports English, Chinese, Japanese, and Korean
/// </summary>
public class LanguageManager : MonoBehaviour
{
    public static LanguageManager Instance { get; private set; }
    
    [Header("Language Settings")]
    [SerializeField] private SystemLanguage currentLanguage = SystemLanguage.English;
    
    // Language data
    private Dictionary<string, Dictionary<SystemLanguage, string>> translations;
    
    // Supported languages
    public enum Language
    {
        English,
        Chinese,
        Japanese,
        Korean
    }
    
    void Awake()
    {
        // Singleton pattern
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeTranslations();
        }
        else
        {
            Destroy(gameObject);
        }
    }
    
    /// <summary>
    /// Initialize translation data
    /// </summary>
    private void InitializeTranslations()
    {
        translations = new Dictionary<string, Dictionary<SystemLanguage, string>>();
        
        // Main Menu
        AddTranslation("game_title", "Gravity Shift", "重力偏移大作战", "重力シフトバトル", "중력 이동 대전");
        AddTranslation("start_game", "Start Game", "开始游戏", "ゲーム開始", "게임 시작");
        AddTranslation("settings", "Settings", "设置", "設定", "설정");
        AddTranslation("quit", "Quit", "退出", "終了", "종료");
        AddTranslation("difficulty", "Difficulty", "难度", "難易度", "난이도");
        AddTranslation("easy", "Easy", "简单", "イージー", "쉬움");
        AddTranslation("normal", "Normal", "普通", "ノーマル", "보통");
        AddTranslation("hard", "Hard", "困难", "ハード", "어려움");
        
        // HUD
        AddTranslation("energy", "Energy", "能量", "エネルギー", "에너지");
        AddTranslation("crystals", "Crystals", "水晶", "クリスタル", "크리스탈");
        AddTranslation("time", "Time", "时间", "時間", "시간");
        AddTranslation("gravity", "Gravity", "重力", "重力", "중력");
        
        // Gravity Directions
        AddTranslation("gravity_down", "Down", "向下", "下", "아래");
        AddTranslation("gravity_up", "Up", "向上", "上", "위");
        AddTranslation("gravity_left", "Left", "向左", "左", "왼쪽");
        AddTranslation("gravity_right", "Right", "向右", "右", "오른쪽");
        AddTranslation("gravity_forward", "Forward", "向前", "前", "앞");
        AddTranslation("gravity_back", "Back", "向后", "後", "뒤");
        
        // Pause Menu
        AddTranslation("paused", "Paused", "暂停", "一時停止", "일시정지");
        AddTranslation("resume", "Resume", "继续", "再開", "재개");
        AddTranslation("restart", "Restart Level", "重新开始", "リスタート", "재시작");
        AddTranslation("main_menu", "Main Menu", "主菜单", "メインメニュー", "메인 메뉴");
        
        // End Level
        AddTranslation("level_complete", "Level Complete!", "关卡完成!", "ステージクリア!", "레벨 완료!");
        AddTranslation("level_failed", "Level Failed", "关卡失败", "ステージ失敗", "레벨 실패");
        AddTranslation("score", "Score", "得分", "スコア", "점수");
        AddTranslation("rating", "Rating", "评级", "評価", "등급");
        AddTranslation("time_taken", "Time Taken", "用时", "クリア時間", "소요 시간");
        AddTranslation("crystals_collected", "Crystals Collected", "收集水晶", "収集クリスタル", "수집한 크리스탈");
        AddTranslation("deaths", "Deaths", "死亡次数", "死亡回数", "사망 횟수");
        AddTranslation("next_level", "Next Level", "下一关", "次のステージ", "다음 레벨");
        AddTranslation("retry", "Retry", "重试", "リトライ", "재시도");
        
        // Settings
        AddTranslation("language", "Language", "语言", "言語", "언어");
        AddTranslation("audio", "Audio", "音频", "オーディオ", "오디오");
        AddTranslation("master_volume", "Master Volume", "主音量", "マスターボリューム", "마스터 볼륨");
        AddTranslation("sfx_volume", "SFX Volume", "音效音量", "効果音", "효과음 볼륨");
        AddTranslation("music_volume", "Music Volume", "音乐音量", "音楽", "음악 볼륨");
        AddTranslation("camera_sensitivity", "Camera Sensitivity", "镜头灵敏度", "カメラ感度", "카메라 감도");
        AddTranslation("camera_rotation", "Camera Rotation", "镜头旋转", "カメラ回転", "카메라 회전");
        
        // Game Messages
        AddTranslation("checkpoint_activated", "Checkpoint Activated", "检查点已激活", "チェックポイント起動", "체크포인트 활성화");
        AddTranslation("barrier_unlocked", "Barrier Unlocked", "屏障已解锁", "バリア解除", "장벽 해제");
        AddTranslation("low_energy", "Low Energy!", "能量不足!", "エネルギー不足!", "에너지 부족!");
        AddTranslation("no_energy", "No Energy to Switch Gravity", "能量不足无法切换重力", "重力切替不可", "중력 전환 불가");
        
        // Level Names
        AddTranslation("level_1", "Tutorial Zone", "教学区域", "チュートリアルゾーン", "튜토리얼 구역");
        AddTranslation("level_2", "Platform Challenges", "平台挑战", "プラットフォームチャレンジ", "플랫폼 도전");
        AddTranslation("level_3", "Hazard Navigation", "危险导航", "ハザードナビゲーション", "위험 탐색");
        AddTranslation("level_4", "Mechanism Puzzles", "机关谜题", "メカニズムパズル", "메커니즘 퍼즐");
        AddTranslation("level_5", "Final Escape", "最终逃离", "最終脱出", "최종 탈출");
        
        // Story Text
        AddTranslation("story_intro", "Dr. Ding's gravity research facility has malfunctioned. Repair the system and escape!", 
                      "丁博士的重力研究设施发生故障。修复系统并逃离!", 
                      "丁博士の重力研究施設が故障しました。システムを修復して脱出せよ!", 
                      "딩 박사의 중력 연구 시설이 오작동했습니다. 시스템을 수리하고 탈출하세요!");
        
        AddTranslation("story_level2", "Gravity stabilizers offline. Proceed with caution.", 
                      "重力稳定器离线。小心前进。", 
                      "重力安定装置がオフライン。注意して進め。", 
                      "중력 안정기 오프라인. 주의하여 진행하세요.");
        
        AddTranslation("story_level3", "Warning: Patrol drones active in this sector.", 
                      "警告:本区域巡逻无人机已激活。", 
                      "警告:このセクターでパトロールドローンが稼働中。", 
                      "경고: 이 구역에서 순찰 드론이 활성화되었습니다.");
        
        AddTranslation("story_level4", "Core access requires full authorization. Collect all crystals.", 
                      "核心访问需要完全授权。收集所有水晶。", 
                      "コアアクセスには完全な認証が必要です。すべてのクリスタルを集めよ。", 
                      "코어 접근에는 완전한 권한이 필요합니다. 모든 크리스탈을 수집하세요.");
        
        AddTranslation("story_level5", "Facility self-destruct initiated! Escape immediately!", 
                      "设施自毁程序已启动!立即逃离!", 
                      "施設自爆開始!すぐに脱出せよ!", 
                      "시설 자폭 시작! 즉시 탈출하세요!");
        
        Debug.Log($"LanguageManager initialized with {translations.Count} translation keys");
    }
    
    /// <summary>
    /// Add a translation entry
    /// </summary>
    private void AddTranslation(string key, string english, string chinese, string japanese, string korean)
    {
        translations[key] = new Dictionary<SystemLanguage, string>
        {
            { SystemLanguage.English, english },
            { SystemLanguage.Chinese, chinese },
            { SystemLanguage.Japanese, japanese },
            { SystemLanguage.Korean, korean }
        };
    }
    
    /// <summary>
    /// Get translated text for current language
    /// </summary>
    public string GetText(string key)
    {
        if (translations.ContainsKey(key))
        {
            if (translations[key].ContainsKey(currentLanguage))
            {
                return translations[key][currentLanguage];
            }
            else
            {
                // Fallback to English
                return translations[key][SystemLanguage.English];
            }
        }
        
        Debug.LogWarning($"Translation key not found: {key}");
        return key;
    }
    
    /// <summary>
    /// Set current language
    /// </summary>
    public void SetLanguage(SystemLanguage language)
    {
        currentLanguage = language;
        
        // Notify all UI elements to update
        UIManager uiManager = FindObjectOfType<UIManager>();
        if (uiManager != null)
        {
            uiManager.RefreshAllText();
        }
        
        Debug.Log($"Language changed to: {language}");
    }
    
    /// <summary>
    /// Set language by index (for UI dropdown)
    /// </summary>
    public void SetLanguageByIndex(int index)
    {
        switch (index)
        {
            case 0:
                SetLanguage(SystemLanguage.English);
                break;
            case 1:
                SetLanguage(SystemLanguage.Chinese);
                break;
            case 2:
                SetLanguage(SystemLanguage.Japanese);
                break;
            case 3:
                SetLanguage(SystemLanguage.Korean);
                break;
        }
    }
    
    /// <summary>
    /// Get current language
    /// </summary>
    public SystemLanguage GetCurrentLanguage()
    {
        return currentLanguage;
    }
    
    /// <summary>
    /// Get current language index (for UI dropdown)
    /// </summary>
    public int GetCurrentLanguageIndex()
    {
        switch (currentLanguage)
        {
            case SystemLanguage.English:
                return 0;
            case SystemLanguage.Chinese:
                return 1;
            case SystemLanguage.Japanese:
                return 2;
            case SystemLanguage.Korean:
                return 3;
            default:
                return 0;
        }
    }
}
