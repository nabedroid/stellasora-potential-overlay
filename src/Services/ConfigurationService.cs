using System.IO;
using System.Text.Json;
using StellasoraPotentialOverlay.Models;

namespace StellasoraPotentialOverlay.Services;

/// <summary>
/// 設定ファイルの読み書きサービス
/// </summary>
public class ConfigurationService
{
    private const string ConfigFileName = "config.json";
    private readonly string _configFilePath;

    public ConfigurationService()
    {
        // 実行ファイルと同じディレクトリに設定ファイルを配置
        var exeDirectory = AppDomain.CurrentDomain.BaseDirectory;
        _configFilePath = Path.Combine(exeDirectory, ConfigFileName);
    }

    /// <summary>
    /// 設定を読み込み
    /// </summary>
    public AppConfig LoadConfig()
    {
        if (!File.Exists(_configFilePath))
        {
            return CreateDefaultConfig();
        }

        try
        {
            var json = File.ReadAllText(_configFilePath);
            var config = JsonSerializer.Deserialize<AppConfig>(json);
            return config ?? CreateDefaultConfig();
        }
        catch
        {
            return CreateDefaultConfig();
        }
    }

    /// <summary>
    /// 設定を保存
    /// </summary>
    public bool SaveConfig(AppConfig config)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };

            var json = JsonSerializer.Serialize(config, options);
            File.WriteAllText(_configFilePath, json);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// デフォルト設定を作成
    /// </summary>
    private AppConfig CreateDefaultConfig()
    {
        return new AppConfig
        {
            TargetWindowTitle = "StellaSora",
            CaptureIntervalMs = 1000,
            BorderColor = "#FF00FF00",
            BorderThickness = 2,
            TextSize = 14,
            LeftX = 0.119,
            CenterX = 0.387,
            RightX = 0.656,
            X2LeftX = 0.253,
            X2RightX = 0.5225,
            CharacterTargets = new List<CharacterTarget>(),
            DebugMode = false,
            CommonY = 0.504,
            CommonWidth = 0.227,
            CommonHeight = 0.139,
            ColorThreshold = 185,
            UseColorThreshold = true
        };
    }

    /// <summary>
    /// 設定ファイルのパスを取得
    /// </summary>
    public string GetConfigFilePath() => _configFilePath;
}
