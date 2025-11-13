using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

public class MapControllerSaveIntegration : MonoBehaviour
{
    [Header("Auto Loading Settings")]
    [SerializeField] private bool _autoLoadOnStart = true;
    [SerializeField] private float _loadDelay = 0f; 
    
    [FormerlySerializedAs("pinMarkerPrefab")]
    [Header("Pin Creation Settings")]
    [SerializeField] private GameObject _pinMarkerPrefab; 
    
    private MapController _mapController;
    private bool _isLoadingData = false;
    
    private void Awake()
    {
        _mapController = GetComponent<MapController>();
        
        if (_mapController == null)
        {
            Debug.LogError("MapControllerSaveIntegration requires MapController component!");
        }
    }
    
    private void Start()
    {
        // Подписываемся на события SaveManager
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnPinsLoaded += OnPinsLoadedFromSave;
        }
        
        // 🔧 Убеждаемся что контейнер маркеров создан
        if (_mapController != null)
        {
            _mapController.GetPinContainer(); // Создает контейнер если его еще нет
        }
        
        // Автоматически загружаем данные при старте
        if (_autoLoadOnStart)
        {
            StartCoroutine(AutoLoadPins());
        }
    }
    
    private void OnDestroy()
    {
        // Отписываемся от событий
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnPinsLoaded -= OnPinsLoadedFromSave;
        }
    }
    
    /// <summary>
    /// Автоматически загружает сохраненные маркеры с небольшой задержкой
    /// </summary>
    private IEnumerator AutoLoadPins()
    {
        // Ждем немного чтобы все объекты успели инициализироваться
        yield return new WaitForSeconds(_loadDelay);
        
        if (SaveManager.Instance != null)
        {
            List<PinData> savedPins = SaveManager.Instance.LoadAllPins();
            CreatePinsFromData(savedPins);
        }
    }
    
    /// <summary>
    /// Создает маркеры из загруженных данных
    /// </summary>
    private void CreatePinsFromData(List<PinData> pinDataList)
    {
        if (_isLoadingData) return;
        _isLoadingData = true;
        
        if (pinDataList == null || pinDataList.Count == 0)
        {
            Debug.Log("No saved pins to load.");
            _isLoadingData = false;
            return;
        }
        
        Debug.Log($"Creating {pinDataList.Count} pins from saved data...");
        
        foreach (var pinData in pinDataList)
        {
            if (pinData != null && !string.IsNullOrEmpty(pinData.name))
            {
                CreatePinFromData(pinData);
            }
        }
        
        Debug.Log("All saved pins loaded successfully.");
        _isLoadingData = false;
    }
    
    /// <summary>
    /// Создает один маркер из данных
    /// </summary>
    private void CreatePinFromData(PinData pinData)
    {
        if (_pinMarkerPrefab == null)
        {
            Debug.LogError("Pin marker prefab not assigned!");
            return;
        }
        
        // Создаем новый маркер
        Transform pinContainer = _mapController != null ? _mapController.GetPinContainer() : transform;
        GameObject newPinObj = Instantiate(_pinMarkerPrefab, pinContainer);
        newPinObj.name = $"Pin_{pinData.name}";
        
        // Устанавливаем позицию
        RectTransform pinRect = newPinObj.GetComponent<RectTransform>();
        if (pinRect != null)
        {
            pinRect.anchoredPosition = pinData.mapPosition;
        }
        
        // Получаем компонент PinMarker и инициализируем его
        PinMarker pinMarker = newPinObj.GetComponent<PinMarker>();
        if (pinMarker != null)
        {
            pinMarker.Initialize(pinData, false, false); 
            pinMarker.SetMapController(_mapController);
        }
        else
        {
            Debug.LogError("PinMarker component not found on prefab!");
            Destroy(newPinObj);
        }
    }
    
    /// <summary>
    /// Обработчик события загрузки данных из SaveManager
    /// </summary>
    private void OnPinsLoadedFromSave(List<PinData> loadedPins)
    {
        Debug.Log($"Received {loadedPins.Count} pins from SaveManager event.");
        
        // Очищаем текущие маркеры (опционально)
        // ClearExistingPins();
        
        // Создаем маркеры из загруженных данных
        CreatePinsFromData(loadedPins);
    }
    
    /// <summary>
    /// Очищает существующие маркеры (опционально)
    /// </summary>
    private void ClearExistingPins()
    {
        PinMarker[] existingPins = FindObjectsOfType<PinMarker>();
        foreach (PinMarker pin in existingPins)
        {
            Destroy(pin.gameObject);
        }
    }
    
    /// <summary>
    /// Публичный метод для ручной загрузки сохраненных данных
    /// </summary>
    [ContextMenu("Load Saved Pins")]
    public void LoadSavedPins()
    {
        if (SaveManager.Instance != null)
        {
            List<PinData> savedPins = SaveManager.Instance.LoadAllPins();
            CreatePinsFromData(savedPins);
        }
        else
        {
            Debug.LogError("SaveManager not found!");
        }
    }
    
    /// <summary>
    /// Публичный метод для принудительного сохранения всех текущих маркеров
    /// </summary>
    [ContextMenu("Save All Current Pins")]
    public void SaveAllCurrentPins()
    {
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveAllPins();
        }
        else
        {
            Debug.LogError("SaveManager not found!");
        }
    }
}

/// <summary>
/// Дополнительные утилиты для работы с сохранением
/// </summary>
public static class SaveUtils
{
    /// <summary>
    /// Проверяет существование файла сохранения и возвращает информацию о нем
    /// </summary>
    public static SaveFileInfo GetSaveFileInfo()
    {
        return SaveManager.Instance?.GetSaveFileInfo();
    }
}