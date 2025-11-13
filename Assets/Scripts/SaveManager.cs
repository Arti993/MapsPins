using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using Newtonsoft.Json;

public class SaveManager : MonoBehaviour
{
    [Header("Save Settings")]
    [SerializeField] private string saveFileName = "map_pins_data.json";
    [SerializeField] private bool autoSave = true;
    [SerializeField] private float autoSaveInterval = 30f; // Автосохранение каждые 30 секунд
    
    // События для оповещения других компонентов
    public event Action<List<PinData>> OnPinsLoaded;
    public event Action OnPinsSaved;
    
    private string SavePath => Path.Combine(Application.persistentDataPath, saveFileName);
    private float lastAutoSaveTime;
    
    // Синглтон паттерн для удобного доступа
    public static SaveManager Instance { get; private set; }
    
    private void Awake()
    {
        // Синглтон
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
        
        // Убеждаемся что папка существует
        string directory = Path.GetDirectoryName(SavePath);
        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }
    
    private void Update()
    {
        // Автосохранение по времени
        if (autoSave && Time.time - lastAutoSaveTime >= autoSaveInterval)
        {
            SaveAllPins();
            lastAutoSaveTime = Time.time;
        }
    }
    
    /// <summary>
    /// Сохраняет данные всех маркеров в JSON файл
    /// </summary>
    public void SaveAllPins()
    {
        try
        {
            // Собираем все активные маркеры
            List<PinMarker> allMarkers = FindObjectsOfType<PinMarker>().ToList();
            List<PinData> pinsToSave = new List<PinData>();
            
            foreach (PinMarker marker in allMarkers)
            {
                PinData pinData = marker.GetPinData();
                if (pinData != null && !string.IsNullOrEmpty(pinData.name))
                {
                    pinsToSave.Add(pinData);
                }
            }
            
            // Конвертируем PinData в SerializablePinData для JSON
            List<SerializablePinData> serializablePins = new List<SerializablePinData>();
            foreach (PinData pinData in pinsToSave)
            {
                serializablePins.Add(new SerializablePinData(pinData));
            }

            // Сериализуем в JSON
            var saveData = new MapPinsSaveData
            {
                pins = serializablePins,
                saveTimestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                saveDate = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss")
            };
            
            string jsonData = JsonConvert.SerializeObject(saveData, Formatting.Indented);
            
            // Записываем в файл
            File.WriteAllText(SavePath, jsonData);
            
            Debug.Log($"Saved {pinsToSave.Count} pins to: {SavePath}");
            OnPinsSaved?.Invoke();
            
            lastAutoSaveTime = Time.time;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to save pins: {e.Message}");
        }
    }
    
    /// <summary>
    /// Загружает данные маркеров из JSON файла
    /// </summary>
    public List<PinData> LoadAllPins()
    {
        try
        {
            // Проверяем существование файла
            if (!File.Exists(SavePath))
            {
                Debug.Log("Save file not found. No pins to load.");
                return new List<PinData>();
            }
            
            // Читаем и десериализуем JSON
            string jsonData = File.ReadAllText(SavePath);
            MapPinsSaveData saveData = JsonConvert.DeserializeObject<MapPinsSaveData>(jsonData);
            
            if (saveData == null || saveData.pins == null)
            {
                Debug.LogWarning("Invalid save data format.");
                return new List<PinData>();
            }
            
            // Конвертируем обратно в PinData
            List<PinData> loadedPins = new List<PinData>();
            foreach (SerializablePinData serializableData in saveData.pins)
            {
                loadedPins.Add(serializableData.ToPinData());
            }
            
            Debug.Log($"Loaded {loadedPins.Count} pins from save file");
            OnPinsLoaded?.Invoke(loadedPins);
            
            return loadedPins;
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to load pins: {e.Message}");
            return new List<PinData>();
        }
    }
    
    /// <summary>
    /// Проверяет существует ли файл сохранения
    /// </summary>
    public bool SaveFileExists()
    {
        return File.Exists(SavePath);
    }
    
    /// <summary>
    /// Удаляет файл сохранения
    /// </summary>
    public void DeleteSaveFile()
    {
        try
        {
            if (File.Exists(SavePath))
            {
                File.Delete(SavePath);
                Debug.Log($"Deleted save file: {SavePath}");
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Failed to delete save file: {e.Message}");
        }
    }
    
    /// <summary>
    /// Принудительно сохраняет все маркеры (для кнопок и т.д.)
    /// </summary>
    [ContextMenu("Save All Pins")]
    public void ForceSaveAllPins()
    {
        SaveAllPins();
    }
    
    /// <summary>
    /// Принудительно загружает все маркеры (для кнопок и т.д.)
    /// </summary>
    [ContextMenu("Load All Pins")]
    public void ForceLoadAllPins()
    {
        LoadAllPins();
    }
    
    /// <summary>
    /// Информация о файле сохранения
    /// </summary>
    public SaveFileInfo GetSaveFileInfo()
    {
        try
        {
            if (!File.Exists(SavePath))
                return null;
            
            FileInfo fileInfo = new FileInfo(SavePath);
            var saveData = JsonConvert.DeserializeObject<MapPinsSaveData>(File.ReadAllText(SavePath));
            
            return new SaveFileInfo
            {
                filePath = SavePath,
                fileSize = fileInfo.Length,
                lastModified = fileInfo.LastWriteTime,
                pinCount = saveData?.pins?.Count ?? 0,
                saveDate = saveData?.saveDate ?? "Unknown"
            };
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Класс для сериализации данных карты с маркерами
/// </summary>
[System.Serializable]
public class MapPinsSaveData
{
    [JsonProperty("pins")]
    public List<SerializablePinData> pins { get; set; }
    
    [JsonProperty("saveTimestamp")]
    public long saveTimestamp { get; set; }
    
    [JsonProperty("saveDate")]
    public string saveDate { get; set; }
    
    [JsonProperty("version")]
    public string version { get; set; } = "1.0.0";
}

/// <summary>
/// Сериализуемая версия PinData
/// </summary>
[System.Serializable]
public class SerializablePinData
{
    [JsonProperty("name")]
    public string name { get; set; }
    
    [JsonProperty("description")]
    public string description { get; set; }
    
    [JsonProperty("mapPosition")]
    public Vector2Serializable mapPosition { get; set; }
    
    [JsonProperty("imagePath")]
    public string imagePath { get; set; } // Для сохранения пути к изображению
    
    // Конструкторы для сериализации
    public SerializablePinData() { }
    
    public SerializablePinData(PinData pinData)
    {
        name = pinData.name;
        description = pinData.description;
        mapPosition = new Vector2Serializable(pinData.mapPosition);
        imagePath = ""; // TODO: Реализовать сохранение изображений
    }
    
    /// <summary>
    /// Конвертирует обратно в PinData
    /// </summary>
    public PinData ToPinData()
    {
        var pinData = new PinData(name, description, null, mapPosition.ToVector2());
        // TODO: Загрузить изображение по imagePath
        return pinData;
    }
}

/// <summary>
/// Сериализуемая версия Vector2
/// </summary>
[System.Serializable]
public struct Vector2Serializable
{
    [JsonProperty("x")]
    public float x;
    
    [JsonProperty("y")]
    public float y;
    
    public Vector2Serializable(float x, float y)
    {
        this.x = x;
        this.y = y;
    }
    
    public Vector2Serializable(Vector2 vector)
    {
        x = vector.x;
        y = vector.y;
    }
    
    public Vector2 ToVector2()
    {
        return new Vector2(x, y);
    }
    
    public static Vector2Serializable FromVector2(Vector2 vector)
    {
        return new Vector2Serializable(vector);
    }
}

/// <summary>
/// Информация о файле сохранения
/// </summary>
public class SaveFileInfo
{
    public string filePath;
    public long fileSize;
    public DateTime lastModified;
    public int pinCount;
    public string saveDate;
}